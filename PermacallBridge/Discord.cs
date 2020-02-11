using Discord;
using Discord.Audio;
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
        private DateTime lastReboot;
        private readonly CommandService discordCommands;
        private readonly DiscordSocketClient discordClient;

        private readonly IConfiguration configuration;
        private readonly ILogger<Discord> logger;

        private List<string> previousDiscordUsers = new List<string>();

        public List<string> Users { get; private set; } = new List<string>();

        private const string discordProcessName = "Discord";

        public Discord(CommandService discordCommands, DiscordSocketClient discordClient, IConfiguration configuration, ILogger<Discord> logger)
        {
            this.discordCommands = discordCommands;
            this.discordClient = discordClient;
            this.configuration = configuration;
            this.logger = logger;

            lastReboot = DateTime.Now;
            InitializeAsync().Wait();
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

            Users.Clear();
            Users.AddRange(users.Select(x => x.Nickname));

            var anyoneOnline = users.Count() > 0;

            if (!anyoneOnline && (DateTime.Now - lastReboot).TotalMinutes > 10 && IsRunning)
            {
                Reboot();
            }

            return Task.FromResult(anyoneOnline);
        }

        private async Task Reboot()
        {
            await Quit();
            await Run();
        }

        public Task Quit()
        {
            try
            {
                foreach (var process in Process.GetProcessesByName(discordProcessName))
                {
#if !DEBUG
                    process.Kill();
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

        public Task Run()
        {
            Process proc = new Process();
#if !DEBUG
            proc.StartInfo.FileName = @"C:\servers\teamspeak\PermacallBridge\Discord.lnk";
            proc.StartInfo.UseShellExecute = true;
            proc.Start();
#endif

            lastReboot = DateTime.Now;
            return Task.CompletedTask;
        }

        private Task Log(LogMessage msg)
        {
            logger.LogInformation(msg.ToString());

            return Task.CompletedTask;
        }

        public async void PostNames(List<string> users)
        {
            string server = configuration.GetSection("Discord:Server").Value;
            string voiceChannel = configuration.GetSection("Discord:Voicechannel").Value;
            string username = configuration.GetSection("Discord:Username").Value;
            string discriminator = configuration.GetSection("Discord:Discriminator").Value;

            var bridgeUser = discordClient
                .Guilds.FirstOrDefault(x => x.Name == server)
                .VoiceChannels.FirstOrDefault(x => x.Name == voiceChannel)
                .Users.FirstOrDefault(x => x.Username == username && x.Discriminator == discriminator);




            if (bridgeUser == null) return;

            await bridgeUser.ModifyAsync(x=>x.Nickname = string.Join(", ", users));
        }

        Task<bool> IVoiceApp.AnyoneOnline()
        {
            throw new NotImplementedException();
        }

        void IVoiceApp.PostNames(List<string> users)
        {
            throw new NotImplementedException();
        }

        Task IVoiceApp.Quit()
        {
            throw new NotImplementedException();
        }

        Task IVoiceApp.Run()
        {
            throw new NotImplementedException();
        }
    }
}
