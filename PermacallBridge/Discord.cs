using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PermacallBridge
{
    public class Discord : IVoiceApp
    {
        private readonly CommandService discordCommands;
        private readonly DiscordSocketClient discordClient;

        private readonly IConfiguration configuration;
        private readonly ILogger<Discord> logger;

        private List<string> previousDiscordUsers = new List<string>();
        private List<string> discordUsers = new List<string>();

        private const string discordProcessName = "Discord";

        public Discord(CommandService discordCommands, DiscordSocketClient discordClient, IConfiguration configuration, ILogger<Discord> logger)
        {
            this.discordCommands = discordCommands;
            this.discordClient = discordClient;
            this.configuration = configuration;
            this.logger = logger;

            InitializeAsync();
        }

        private async Task InitializeAsync()
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

        public bool IsRunning
        {
            get
            {
                Process[] pname = Process.GetProcessesByName(discordProcessName);
                return !(pname.Length == 0);
            }
        }

        public Task<bool> AnyoneOnline()
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

            return Task.FromResult(users.Count() > 0);
        }

        public Task Quit()
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
                logger.LogWarning(e.Message);
                logger.LogWarning(e.StackTrace);
            }

            return Task.CompletedTask;
        }

        public Task Run()
        {
            Process proc = new Process();
            proc.StartInfo.FileName = @"C:\servers\teamspeak\PermacallBridge\Discord.lnk";
            proc.StartInfo.UseShellExecute = true;
            proc.Start();

            return Task.CompletedTask;
        }

        private Task Log(LogMessage msg)
        {
            logger.LogInformation(msg.ToString());

            return Task.CompletedTask;
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
    }
}
