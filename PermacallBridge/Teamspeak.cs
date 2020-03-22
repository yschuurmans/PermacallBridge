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

namespace PermacallBridge
{
    public class Teamspeak : IVoiceApp
    {
        private DateTime lastReboot;
        private readonly IConfiguration configuration;
        private readonly ILogger logger;
        private string currentName = "PermacallBridge";

        private const string teamspeakProcessName = "ts3client_win64";

        private List<string> previousTeamspeakUsers = new List<string>();
        public List<string> Users { get; private set; } = new List<string>();

        public Teamspeak(IConfiguration configuration, ILogger<Teamspeak> logger)
        {
            this.configuration = configuration;
            this.logger = logger;

            lastReboot = DateTime.Now;
        }

        public bool IsRunning
        {
            get
            {
                Process[] pname = Process.GetProcessesByName(teamspeakProcessName);
                return !(pname.Length == 0);
            }
        }
        public async Task ConnectAndLogin(TeamSpeakClient rc)
        {
            string username = configuration.GetSection("Teamspeak:Username").Value;
            string password = configuration.GetSection("Teamspeak:Password").Value;
            string nickname = configuration.GetSection("Nickname").Value;

            // Create rich client instance
            await rc.Connect(); // connect to the server
            await rc.Login(username, password); // login to do some stuff that requires permission
            await rc.UseServer(1); // Use the server with id '1'
            await rc.ChangeNickName(nickname);
        }
        public async Task<bool> AnyoneOnline()
        {
            try
            {
                string server = configuration.GetSection("Teamspeak:Server").Value;
                string port = configuration.GetSection("Teamspeak:Port").Value;
                using (var rc = new TeamSpeakClient(server, Convert.ToInt32(port)))
                {
                    await ConnectAndLogin(rc);

                    var clients = await rc.GetClients();
                    var channelClients = clients.Where(x => x.Type == ClientType.FullClient && x.ChannelId == 1 && x.DatabaseId != 1045);

                    previousTeamspeakUsers.Clear();
                    previousTeamspeakUsers.AddRange(Users);
                    Users.Clear();
                    Users.AddRange(channelClients.Select(x => x.NickName));

                    var anyoneOnline = channelClients.Count() > 0;
                    await rc.Logout();

                    if (!anyoneOnline && (DateTime.Now - lastReboot).TotalMinutes > 60 && IsRunning)
                    {
                        Reboot();
                    }

                    return anyoneOnline;
                }
            }
            catch (Exception e)
            {
                logger.LogWarning(e.Message);
                logger.LogWarning(e.StackTrace);
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
        public Task Quit()
        {
            try
            {
                foreach (var process in Process.GetProcessesByName(teamspeakProcessName))
                {
#if !DEBUG
                    process.CloseMainWindow();
#endif
                }
            }
            catch (Exception e)
            {
                logger.LogWarning(e.Message);
                logger.LogWarning(e.StackTrace);
            }

            return Task.CompletedTask;
        }
        private async Task<bool> IsNicknameUsed(string nickname)
        {
            try
            {
                string server = configuration.GetSection("Teamspeak:Server").Value;
                string port = configuration.GetSection("Teamspeak:Port").Value;
                int dbid = Convert.ToInt32(configuration.GetSection("Teamspeak:DatabaseID").Value);
                using (var rc = new TeamSpeakClient(server, Convert.ToInt32(port)))
                {
                    await ConnectAndLogin(rc);

                    var clients = await rc.GetClients();
                    return clients.Any(x => x.NickName == nickname && x.DatabaseId != dbid);
                }
            }
            catch (Exception e)
            {
                logger.LogWarning(e.Message);
                logger.LogWarning(e.StackTrace);
            }
            return true;
        }

        private async Task<string> ChooseNewName(List<string> users)
        {
            string userString = string.Join(@", ", users);
            string tempName = userString.FixNickname();

            if (await IsNicknameUsed(tempName))
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
            string newName = await ChooseNewName(users);

            if (currentName.ToLower() == newName.ToLower())
            {
                logger.LogInformation($"Canceled posting, name hasn't changed");
                return;
            }

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
                            currentName = newName;
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
            }
        }
    }
}
