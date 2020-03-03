﻿using Discord;
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
using System.Threading;
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

        private readonly string server;
        private readonly string voiceChannel;
        private readonly string username;
        private readonly string discriminator;

        private List<string> previousDiscordUsers = new List<string>();

        public List<string> Users { get; private set; } = new List<string>();

        private const string discordProcessName = "Discord";

        public Discord(CommandService discordCommands, DiscordSocketClient discordClient, IConfiguration configuration, ILogger<Discord> logger)
        {
            this.discordCommands = discordCommands;
            this.discordClient = discordClient;
            this.configuration = configuration;
            this.logger = logger;

            server = configuration.GetSection("Discord:Server").Value;
            voiceChannel = configuration.GetSection("Discord:Voicechannel").Value;
            username = configuration.GetSection("Discord:Username").Value;
            discriminator = configuration.GetSection("Discord:Discriminator").Value;

            lastReboot = DateTime.Now;
            InitializeAsync().Wait();
        }

        //This is a replacement for Cursor.Position in WinForms
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool SetCursorPos(int x, int y);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        public const int MOUSEEVENTF_LEFTDOWN = 0x02;
        public const int MOUSEEVENTF_LEFTUP = 0x04;

        public async Task JoinVoice()
        {
#if !DEBUG
            var xpos = 35;
            var ypos = 115;
            BringMainWindowToFront(discordProcessName);
            await Task.Delay(100);
            SetCursorPos(xpos, ypos);
            SetCursorPos(xpos, ypos);
            mouse_event(MOUSEEVENTF_LEFTDOWN, xpos, ypos, 0, 0);
            await Task.Delay(100);
            mouse_event(MOUSEEVENTF_LEFTUP, xpos, ypos, 0, 0);
            await Task.Delay(100);

            xpos = 100;
            ypos = 263;
            BringMainWindowToFront(discordProcessName);
            SetCursorPos(xpos, ypos);
            await Task.Delay(100);
            SetCursorPos(xpos, ypos);
            await Task.Delay(100);
            mouse_event(MOUSEEVENTF_LEFTDOWN, xpos, ypos, 0, 0);
            await Task.Delay(100);
            mouse_event(MOUSEEVENTF_LEFTUP, xpos, ypos, 0, 0);
            await Task.Delay(100);
            HideWindow(discordProcessName);
#endif
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
            var users = discordClient
                .Guilds.FirstOrDefault(x => x.Name == server)
                .VoiceChannels.FirstOrDefault(x => x.Name == voiceChannel)
                .Users.Where(x => !(x.Username == username && x.Discriminator == discriminator));

            Users.Clear();
            Users.AddRange(users.Select(x => x.Nickname ?? x.Username));

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

        public async Task Run()
        {
            Process proc = new Process();
#if !DEBUG

            proc.StartInfo.FileName = @"C:\servers\teamspeak\PermacallBridge\Discord.lnk";
            proc.StartInfo.UseShellExecute = true;
            proc.Start();
#endif

            lastReboot = DateTime.Now;
        }

        private Task Log(LogMessage msg)
        {
            logger.LogInformation(msg.ToString());

            return Task.CompletedTask;
        }

        public async Task PostNames(List<string> users)
        {
            var bridgeUser = discordClient
                .Guilds.FirstOrDefault(x => x.Name == server)
                .Users.FirstOrDefault(x => x.Username == username && x.Discriminator == discriminator);

            if (bridgeUser == null) return;
            if (users.Count > 0)
                await bridgeUser.ModifyAsync(x => x.Nickname = string.Join(", ", users));
            else
                await bridgeUser.ModifyAsync(x => x.Nickname = username);

            await CheckJoin();
        }

        private async Task CheckJoin()
        {
            var isBridgeInChannel = discordClient
                .Guilds.FirstOrDefault(x => x.Name == server)
                .VoiceChannels.FirstOrDefault(x => x.Name == voiceChannel)
                .Users.Any(x => x.Username == username && x.Discriminator == discriminator);
            if (!isBridgeInChannel)
                await JoinVoice();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, ShowWindowEnum flags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetForegroundWindow(IntPtr hwnd);

        public enum ShowWindowEnum
        {
            Hide = 0,
            ShowNormal = 1, ShowMinimized = 2, ShowMaximized = 3,
            Maximize = 3, ShowNormalNoActivate = 4, Show = 5,
            Minimize = 6, ShowMinNoActivate = 7, ShowNoActivate = 8,
            Restore = 9, ShowDefault = 10, ForceMinimized = 11
        };

        public void BringMainWindowToFront(string processName)
        {
            // get the process
            Process[] bProcess = Process.GetProcessesByName(processName);

            // check if the process is running
            if (bProcess != null)
            {
                foreach (var process in bProcess)
                {
                    // check if the window is hidden / minimized
                    if (process.MainWindowHandle == IntPtr.Zero)
                    {
                        // the window is hidden so try to restore it before setting focus.
                        ShowWindow(process.Handle, ShowWindowEnum.Restore);
                        ShowWindow(process.MainWindowHandle, ShowWindowEnum.Restore);
                    }

                    ShowWindow(process.Handle, ShowWindowEnum.ShowMaximized);
                    ShowWindow(process.MainWindowHandle, ShowWindowEnum.ShowMaximized);

                    // set user the focus to the window
                    SetForegroundWindow(process.MainWindowHandle);
                }
            }
        }

        public void HideWindow(string processName)
        {
            // get the process
            Process[] bProcess = Process.GetProcessesByName(processName);

            // check if the process is running
            if (bProcess != null)
            {
                foreach (var process in bProcess)
                {
                    ShowWindow(process.MainWindowHandle, ShowWindowEnum.ShowMinimized);
                    ShowWindow(process.Handle, ShowWindowEnum.ShowMinimized);
                }
            }
        }
    }
}
