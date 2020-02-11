using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeamSpeak3QueryApi.Net.Specialized;

namespace PermacallBridge
{
    public class Teamspeak : IVoiceApp
    {
        private DateTime lastReboot;
        private readonly IConfiguration configuration;
        private readonly ILogger logger;

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

                    if (!anyoneOnline && (DateTime.Now - lastReboot).TotalMinutes > 10 && IsRunning)
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

        public async void PostNames(List<string> users)
        {
            //try
            //{
            //    using (var rc = new TeamSpeakClient("permacall.nl", 10011))
            //    {
            //        // Create rich client instance
            //        ConnectAndLogin(rc);

            //        var clients = await rc.GetClients();
            //        var bridgeUser = clients.FirstOrDefault(x => x.ChannelId == 1 && x.DatabaseId != 1045);
            //        var bridgeClientInfo = await rc.GetClientInfo(bridgeUser.Id);

            //        bridgeClientInfo.Description = String.Join("\r\n", users);

            //        await rc.Logout();
            //    }
            //}
            //catch (Exception e)
            //{
            //    logger.LogWarning(e.Message);
            //    logger.LogWarning(e.StackTrace);
            //}
        }
    }
}
