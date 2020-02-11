using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeamSpeak3QueryApi.Net.Specialized;

namespace PermacallBridge
{
    public class Bridge : IHostedService, IDisposable
    {
        private bool shouldStop = false;

        private readonly CommandService discordCommands;
        private readonly DiscordSocketClient discordClient;
        private readonly IConfiguration configuration;

        private List<string> previousDiscordUsers = new List<string>();
        private List<string> previousTeamspeakUsers = new List<string>();

        private List<string> discordUsers = new List<string>();
        private List<string> teamspeakUsers = new List<string>();

        public Bridge(CommandService commands, DiscordSocketClient client, IConfiguration configuration)
        {
            this.discordCommands = commands;
            this.discordClient = client;
            this.configuration = configuration;
        }

        public async Task Loop()
        {
            var test = AnyoneInDiscord();
            var test2 = await AnyoneInTeamspeak();
            await Task.Delay(1000);
            Log("Ready");
            while (true)
            {
                try
                {
                    CheckDiscord();
                    Thread.Sleep(10000);

                }
                catch (Exception e)
                {
                    Log(e.Message);
                    Log(e.StackTrace);
                }
                try
                {
                    CheckTeamspeak();
                    Thread.Sleep(10000);
                }
                catch (Exception e)
                {
                    Log(e.Message);
                    Log(e.StackTrace);
                }
            }
        }

        private const string discordProcessName = "Discord";
        public bool DiscordIsRunning
        {
            get
            {
                Process[] pname = Process.GetProcessesByName(discordProcessName);
                return !(pname.Length == 0);
            }
        }
        private void CheckDiscord()
        {
            Log("Checking for discord");
            bool isRunning = TeamspeakIsRunning;
            bool areUsersOnline = AnyoneInDiscord();

            Log("Teamspeak " + (isRunning ? "is" : "isn't") + " running and there " + (areUsersOnline ? "are" : "aren't") + " users online on Discord");

            if (!isRunning && areUsersOnline)
            {
                Log("Starting Teamspeak");
                RunTeamspeak();
            }

            if (isRunning && !areUsersOnline)
            {
                Log("Stopping Teamspeak");
                QuitTeamspeak();
            }
        }
        private void RunDiscord()
        {
            Process proc = new Process();
            proc.StartInfo.FileName = @"C:\servers\teamspeak\PermacallBridge\Discord.lnk";
            proc.StartInfo.UseShellExecute = true;
            proc.Start();
        }
        private void QuitDiscord()
        {
            try
            {
                foreach (var process in Process.GetProcessesByName(discordProcessName))
                {
                    process.Kill();
                }
            }
            catch (Exception e)
            {
                Log(e.Message);
                Log(e.StackTrace);
            }
        }



        private const string teamspeakProcessName = "ts3client_win64";
        public bool TeamspeakIsRunning
        {
            get
            {
                Process[] pname = Process.GetProcessesByName(teamspeakProcessName);
                return !(pname.Length == 0);
            }
        }
        private async Task CheckTeamspeak()
        {
            Log("Checking for teamspeak");
            bool isRunning = DiscordIsRunning;
            bool areUsersOnline = await AnyoneInTeamspeak();

            Log("Discord " + (isRunning ? "is" : "isn't") + " running and there " + (areUsersOnline ? "are" : "aren't") + " users online on Teamspeak");

            if (!isRunning && areUsersOnline)
            {
                Log("Starting Discord");
                RunDiscord();
            }

            if (isRunning && !areUsersOnline)
            {
                Log("Stopping Discord");
                QuitDiscord();
            }
        }
        private void RunTeamspeak()
        {
            Process proc = new Process();
            proc.StartInfo.FileName = @"C:\servers\teamspeak\PermacallBridge\TeamSpeakClient.lnk";
            proc.StartInfo.UseShellExecute = true;
            proc.Start();
        }
        private void QuitTeamspeak()
        {
            try
            {
                foreach (var process in Process.GetProcessesByName(teamspeakProcessName))
                {
                    process.CloseMainWindow();
                }
            }
            catch (Exception e)
            {
                Log(e.Message);
                Log(e.StackTrace);
            }
        }

        private bool AnyoneInDiscord()
        {
            string server = configuration.GetSection("Discord:Server").Value;
            string voiceChannel = configuration.GetSection("Discord:Voicechannel").Value;
            string username = configuration.GetSection("Discord:Username").Value;
            string discriminator = configuration.GetSection("Discord:Discriminator").Value;

            var users = discordClient
                .Guilds.FirstOrDefault(x => x.Name == server)
                .VoiceChannels.FirstOrDefault(x => x.Name == voiceChannel)
                .Users.Where(x => !(x.Username == username && x.Discriminator == discriminator));

            discordUsers.Clear();
            discordUsers.AddRange(users.Select(x => x.Nickname));

            return users.Count() > 0;
        }

        //private bool PostTeamspeakUsersToDiscord()
        //{
        //    var users = _client
        //        .Guilds.FirstOrDefault(x => x.Name == "Permacall")
        //        .VoiceChannels.FirstOrDefault(x => x.Name == "Permacall")
        //        .Users.Where(x => x.Username != "PermacallBridge");

        //    previousDiscordUsers.Clear();
        //    previousDiscordUsers.AddRange(discordUsers);
        //    discordUsers.Clear();
        //    discordUsers.AddRange(users.Select(x => x.Nickname));

        //    return users.Count() > 0;
        //}

        private async Task<bool> AnyoneInTeamspeak()
        {
            try
            {
                string server = configuration.GetSection("Teamspeak:Server").Value;
                string port = configuration.GetSection("Teamspeak:Port").Value;
                string username = configuration.GetSection("Teamspeak:Username").Value;
                string password = configuration.GetSection("Teamspeak:Password").Value;
                string nickname = configuration.GetSection("Nickname").Value;
                using (var rc = new TeamSpeakClient(server, Convert.ToInt32(port)))
                {
                    // Create rich client instance
                    await rc.Connect(); // connect to the server
                    await rc.Login(username, password); // login to do some stuff that requires permission
                    await rc.UseServer(1); // Use the server with id '1'
                    await rc.ChangeNickName(nickname);

                    var clients = await rc.GetClients();
                    var channelClients = clients.Where(x => x.Type == ClientType.FullClient && x.ChannelId == 1 && x.DatabaseId != 1045);

                    previousTeamspeakUsers.Clear();
                    previousTeamspeakUsers.AddRange(teamspeakUsers);
                    teamspeakUsers.Clear();
                    teamspeakUsers.AddRange(channelClients.Select(x => x.NickName));

                    var result = channelClients.Count() > 0;
                    await rc.Logout();
                    return result;
                }
            }
            catch (Exception e)
            {
                Log(e.Message);
                Log(e.StackTrace);
            }
            return false;
        }

        //private async void PostDiscordUsersToTeamspeak()
        //{

        //    try
        //    {
        //        using (var rc = new TeamSpeakClient("permacall.nl", 10011))
        //        {
        //            // Create rich client instance
        //            await rc.Connect(); // connect to the server
        //            await rc.Login("serveradmin", "XUlC86oZ"); // login to do some stuff that requires permission
        //            await rc.UseServer(1); // Use the server with id '1'
        //            await rc.ChangeNickName("PermacallBridgeQuery");

        //            var clients = await rc.GetClients();
        //            var bridgeUser = clients.FirstOrDefault(x => x.ChannelId == 1 && x.DatabaseId != 1045);
        //            var bridgeClientInfo = await rc.GetClientInfo(bridgeUser.Id);

        //            bridgeClientInfo.Description = String.Join("\r\n", discordUsers);

        //            await rc.Logout();
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //    }
        //}

        private void StartBridge()
        {
            throw new NotImplementedException();
        }

        private void KillBridge()
        {
            throw new NotImplementedException();
        }



        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(1000);

            await LoginAsync();

            await Task.Delay(1000);

            await Loop();
        }

        private async Task LoginAsync()
        {
            discordClient.Log += Log;

            string token = configuration.GetSection("Discord:Token").Value;

            await discordClient.LoginAsync(TokenType.Bot, token);
            await discordClient.StartAsync();
            while (discordClient.ConnectionState == ConnectionState.Connecting)
            {
                await Task.Delay(100);
            }

            await Task.Delay(1000);
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());

            return Task.CompletedTask;
        }
        private void Log(string msg)
        {
            Console.WriteLine(msg);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            shouldStop = true;

            KillBridge();

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            shouldStop = true;
        }
    }
}
