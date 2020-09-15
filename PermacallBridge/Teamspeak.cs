using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeamSpeak3QueryApi.Net.Specialized;
using PrimS.Telnet;
using System.Text.RegularExpressions;
using TeamSpeak3QueryApi.Net.Specialized.Notifications;
using TeamSpeak3QueryApi.Net.Specialized.Responses;

namespace PermacallBridge
{
    public class Teamspeak : IVoiceApp
    {
        private DateTime lastReboot;
        private readonly IConfiguration configuration;
        private readonly ILogger logger;

        private WhoAmI botClient;
        private TeamSpeakClient tsClient;
        private DateTime tsClientLifetime;


        private string previousSimpleName = "PermacallBridge";
        private string nickname;
        private const string teamspeakProcessName = "ts3client_win64";

        private List<string> previousTeamspeakUsers = new List<string>();
        public List<string> Users { get; private set; } = new List<string>();

        public Func<Task> UsersChanged;
        public Func<string, string, Task> SendChatMessageEvent;

        public Teamspeak(IConfiguration configuration, ILogger<Teamspeak> logger)
        {
            this.configuration = configuration;
            this.logger = logger;
        }

        public async Task Initialize()
        {
            lastReboot = DateTime.Now;
            tsClientLifetime = DateTime.Now;
            await ConnectAndLogin();
        }

        public async void ClientMoved(IReadOnlyCollection<ClientMoved> notifications)
        {
            await UsersChanged();
            logger.LogInformation("Client Moved!");
        }

        public async void ChatMessageReceived(IReadOnlyCollection<TextMessage> chatMessages)
        {
            try
            {
                foreach (var msg in chatMessages)
                {
                    if (msg.TargetMode != MessageTarget.Channel) continue;
                    if (msg.InvokerId == botClient.ClientId) continue;


                    logger.LogInformation($"Received message from {msg.InvokerName}: {msg.Message}, sending to Discord...");

                    await SendChatMessageEvent(msg.InvokerName, msg.Message.FixMessage());
                }
            }
            catch (Exception e)
            {
                logger.LogWarning(e.Message);
                logger.LogWarning(e.StackTrace);
            }
        }

        private int dbid;

        public async Task SendChatMessage(string message)
        {
            try
            {
                await tsClient.SendMessage(message, MessageTarget.Channel, 1);
            }
            catch (Exception e)
            {
                logger.LogWarning(e.Message);
                logger.LogWarning(e.StackTrace);
            }
        }

        public bool IsRunning
        {
            get
            {
                Process[] pname = Process.GetProcessesByName(teamspeakProcessName);
                return !(pname.Length == 0);
            }
        }

        public async Task Reconnect()
        {
            try
            {
                await Disconnect();
            }
            catch (Exception e)
            {
                logger.LogWarning(e.Message);
                logger.LogWarning(e.StackTrace);
            }

            try
            {
                await ConnectAndLogin();
            }
            catch (Exception e)
            {
                logger.LogWarning(e.Message);
                logger.LogWarning(e.StackTrace);
            }
        }
        public async Task ConnectAndLogin()
        {
            try
            {
                dbid = Convert.ToInt32(configuration.GetSection("Teamspeak:DatabaseID").Value);

                string server = configuration.GetSection("Teamspeak:Server").Value;
                string port = configuration.GetSection("Teamspeak:Port").Value;
                string username = configuration.GetSection("Teamspeak:Username").Value;
                string password = configuration.GetSection("Teamspeak:Password").Value;
                nickname = configuration.GetSection("Nickname").Value;

                tsClient = new TeamSpeakClient(server, Convert.ToInt32(port));

                // Create rich client instance
                await tsClient.Connect(); // connect to the server
                await tsClient.Login(username, password); // login to do some stuff that requires permission
                await tsClient.UseServer(1); // Use the server with id '1'
#if DEBUG
                await tsClient.ChangeNickName(nickname + "_DEBUG");
#else
                await tsClient.ChangeNickName(nickname);
#endif


                botClient = await tsClient.WhoAmI();
                await tsClient.MoveClient(botClient.ClientId, 1);

                await tsClient.RegisterChannelNotification(1);
                tsClient.Subscribe<ClientMoved>(ClientMoved);

                await tsClient.RegisterTextChannelNotification();
                tsClient.Subscribe<TextMessage>(ChatMessageReceived);

                var pChannel = await tsClient.GetChannels();

            }
            catch (Exception e)
            {
                logger.LogWarning(e.Message);
                logger.LogWarning(e.StackTrace);
            }
        }

        public async Task Disconnect()
        {
            try
            {
                tsClient.Unsubscribe<ClientMoved>(ClientMoved);
                tsClient.Unsubscribe<TextMessage>(ChatMessageReceived);
                await tsClient.Logout();
                tsClient.Dispose();
                tsClient = null;
            }
            catch (Exception e)
            {
                logger.LogWarning(e.Message);
                logger.LogWarning(e.StackTrace);
            }
        }


        public async Task<bool> AnyoneOnline()
        {
            try
            {
                var clients = await tsClient.GetClients();
                var channelClients = clients.Where(x => x.Type == ClientType.FullClient && x.ChannelId == 1 && x.DatabaseId != 1045);

                previousTeamspeakUsers.Clear();
                previousTeamspeakUsers.AddRange(Users);
                Users.Clear();
                Users.AddRange(channelClients.Select(x => x.NickName));

                var anyoneOnline = channelClients.Count() > 0;



                if (!anyoneOnline && (DateTime.Now - lastReboot).TotalMinutes > 60 && IsRunning)
                {
                    await Reboot();
                }

                if ((DateTime.Now - tsClientLifetime).TotalMinutes > 60)
                {
                    await Reconnect();
                }


                return anyoneOnline;
            }
            catch (Exception e)
            {
                logger.LogWarning(e.Message);
                logger.LogWarning(e.StackTrace);
                await Reconnect();
            }
            return false;
        }

        private async Task Reboot()
        {
            await Quit();
            await Run();
        }
        public Task Run()
        {
            Process proc = new Process();
#if !DEBUG
            proc.StartInfo.FileName = @"C:\servers\teamspeak\PermacallBridge\TeamSpeakClient.lnk";
            proc.StartInfo.UseShellExecute = true;
            proc.Start();
#endif
            lastReboot = DateTime.Now;
            return Task.CompletedTask;
        }
        public async Task Quit()
        {
            try
            {
#if !DEBUG
                foreach (var process in Process.GetProcessesByName(teamspeakProcessName))
                {
                    process.Close();
                }
                foreach (var process in Process.GetProcessesByName(teamspeakProcessName))
                {
                    process.CloseMainWindow();
                }
                await Task.Delay(10000);
                foreach (var process in Process.GetProcessesByName(teamspeakProcessName))
                {
                    process.Kill();
                }
#endif
            }
            catch (Exception e)
            {
                logger.LogWarning(e.Message);
                logger.LogWarning(e.StackTrace);
            }
        }

        private async Task<bool> IsNicknameUsed(string nickname)
        {
            try
            {
                var clients = await tsClient.GetClients();
                return clients.Any(x => x.NickName == nickname && x.DatabaseId != dbid);
            }
            catch (Exception e)
            {
                logger.LogWarning(e.Message);
                logger.LogWarning(e.StackTrace);
                await Reconnect();
            }
            return true;
        }

        private async Task<string> ChooseNewName(List<string> users, bool checkAccountUsed = false)
        {
            string userString = string.Join(@", ", users);
            string tempName = userString.FixNickname();

            if (checkAccountUsed && await IsNicknameUsed(tempName))
            {
                if (users.Count == 1)
                {
                    if (!await IsNicknameUsed(@"[D]\s" + tempName))
                    {
                        return @"[D]\s" + tempName;
                    }
                }


                int index = 1;
                do
                {
                    tempName = userString + $"({index++})";
                } while (await IsNicknameUsed(tempName));
            }

            //tempName.FixNickname();

            return tempName.Replace(" ", @"\s");
        }

        public async Task PostNames(List<string> users)
        {
            if (previousSimpleName == string.Join(",", users)) return;

            string newName = await ChooseNewName(users, false);
            var currentName = await GetCurrentName();

            if (newName.Replace(@"\s", " ").ToLower() == currentName?.ToLower())
            {
                logger.LogInformation($"Canceled posting, name hasn't changed");
                return;
            }

            SetName(newName);
            previousSimpleName = string.Join(",", users);
        }

        private async Task<string> GetCurrentName()
        {
            try
            {
                var clients = await tsClient.GetClients();
                return clients.FirstOrDefault(x => x.DatabaseId == dbid)?.NickName;
            }
            catch (Exception e)
            {
                logger.LogWarning(e.Message);
                logger.LogWarning(e.StackTrace);
                await Reconnect();
            }
            return "";
        }


        private async void SetName(string newName)
        {
            await Task.Delay(5000);

            string clientHost = configuration.GetSection("Teamspeak:Client:Host").Value;
            int clientPort = Convert.ToInt32(configuration.GetSection("Teamspeak:Client:Port").Value);
            string clientApiKey = configuration.GetSection("Teamspeak:Client:ApiKey").Value;

            //connect to telnet
            try
            {
                logger.LogInformation("Connecting to telnet");
                using (Client client = new Client(clientHost, clientPort, new System.Threading.CancellationToken()))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        logger.LogInformation("Attempting authentication");
                        await client.WriteLine($"auth apikey={clientApiKey}");
                        string result = await client.ReadAsync(TimeSpan.FromSeconds(1));
                        if (result.Contains("ok"))
                        {
                            logger.LogInformation($"Posting: {newName}");
                            //await client.WriteLine($"auth apikey={clientApiKey}");
                            logger.LogInformation("Posting names to teamspeak");
                            await client.WriteLine($"clientupdate client_nickname={newName}");
                            return;
                        }
                        await Task.Delay(100);
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogWarning(e.Message);
                logger.LogWarning(e.StackTrace);
                await Reconnect();
            }
        }
    }
}
