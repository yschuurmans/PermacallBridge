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
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
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
        private readonly string chatChannel;
        private readonly string username;
        private readonly string discriminator;
        private string currentName = "PermacallBridge";
        private bool isBridgeInChannel = false;

        private List<string> previousDiscordUsers = new List<string>();

        public List<string> Users { get; private set; } = new List<string>();

        private const string discordProcessName = "Discord";

        public Func<Task> UsersChanged;
        public Func<string, string, Task> SendChatMessageEvent;

        public Discord(CommandService discordCommands, DiscordSocketClient discordClient, IConfiguration configuration, ILogger<Discord> logger)
        {
            this.discordCommands = discordCommands;
            this.discordClient = discordClient;
            this.configuration = configuration;
            this.logger = logger;

            server = configuration.GetSection("Discord:Server").Value;
            voiceChannel = configuration.GetSection("Discord:Voicechannel").Value;
            chatChannel = configuration.GetSection("Discord:Chatchannel").Value;
            username = configuration.GetSection("Discord:Username").Value;
            discriminator = configuration.GetSection("Discord:Discriminator").Value;

            lastReboot = DateTime.Now;
        }

        //This is a replacement for Cursor.Position in WinForms
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool SetCursorPos(int x, int y);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        [DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll", EntryPoint = "SendMessage", SetLastError = true)]
        static extern IntPtr SendMessage(IntPtr hWnd, Int32 Msg, IntPtr wParam, IntPtr lParam);

        const int WM_COMMAND = 0x111;
        const int MIN_ALL = 419;
        const int MOUSEEVENTF_LEFTDOWN = 0x02;
        const int MOUSEEVENTF_LEFTUP = 0x04;

        public async Task JoinVoice(int yOffset)
        {
            //#if !DEBUG
            var xpos = 35;
            var ypos = 115;
            if (!BringMainWindowToFront(discordProcessName)) return;
            await Task.Delay(100);
            SetCursorPos(xpos, ypos);
            SetCursorPos(xpos, ypos);
            if (!BringMainWindowToFront(discordProcessName)) return;
            mouse_event(MOUSEEVENTF_LEFTDOWN, xpos, ypos, 0, 0);
            await Task.Delay(100);
            mouse_event(MOUSEEVENTF_LEFTUP, xpos, ypos, 0, 0);
            await Task.Delay(100);

            xpos = Convert.ToInt32(configuration.GetSection("Discord:Server").Value);
            ypos = yOffset + Convert.ToInt32(configuration.GetSection("Discord:Voicechannel").Value);

            if (!BringMainWindowToFront(discordProcessName)) return;
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

        public async Task Initialize()
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
            discordClient.UserVoiceStateUpdated += ClientMoved;
            discordClient.MessageReceived += MessageReceived;
        }

        public async Task ClientMoved(SocketUser user, SocketVoiceState state1, SocketVoiceState state2)
        {
            if (state1.VoiceChannel?.Name == voiceChannel || state2.VoiceChannel?.Name == voiceChannel)
            {
                await UsersChanged();
            }
        }

        public async Task SendChatMessage(string message)
        {
            try
            {
                await discordClient
                   .Guilds.FirstOrDefault(x => x.Name == server)
                   .TextChannels.FirstOrDefault(x => x.Name == chatChannel)
                   .SendMessageAsync(message);
            }
            catch (Exception e)
            {
                logger.LogWarning(e.Message);
                logger.LogWarning(e.StackTrace);
            }
        }

        private async Task MessageReceived(SocketMessage arg)
        {
            try
            {
                if (arg.Channel.Name != chatChannel) return;
                if (arg.Author.Username == discordClient.CurrentUser.Username && arg.Author.Discriminator == discordClient.CurrentUser.Discriminator) return;
                var nickname = ((SocketGuildUser)arg.Author).Nickname ?? $"{arg.Author.Username}#{arg.Author.Discriminator}";

                logger.LogInformation($"Received message from {nickname}: {arg.Content}, sending to Teamspeak...");


                if (!string.IsNullOrEmpty(arg.Content))
                    await SendChatMessageEvent(nickname, arg.Content);

                if (arg.Attachments != null)
                {
                    foreach (var attachment in arg.Attachments)
                    {
                        await SendChatMessageEvent(nickname, attachment.Url);
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogWarning(e.Message);
                logger.LogWarning(e.StackTrace);
            }
        }

        //public async Task ChatMessageReceived(IReadOnlyCollection<TextMessage> chatMessages)
        //{
        //    foreach (var msg in chatMessages)
        //    {
        //        if (msg.TargetMode != MessageTarget.Channel) continue;
        //        await SendChatMessage(msg.InvokerName, msg.Message);
        //    }
        //}

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

            if (!anyoneOnline && (DateTime.Now - lastReboot).TotalMinutes > 20 && IsRunning)
            {
                Reboot();
            }

            return Task.FromResult(anyoneOnline);
        }

        private async Task Reboot()
        {
            await Quit();
            Run();
        }

        public Task Quit()
        {
            try
            {

#if !DEBUG
                var processes = Process.GetProcessesByName(discordProcessName);
                foreach (var process in processes)
                {
                    process.Close();
                }
                processes = Process.GetProcessesByName(discordProcessName);
                foreach (var process in processes)
                {
                    process.CloseMainWindow();
                }
                processes = Process.GetProcessesByName(discordProcessName);
                foreach (var process in processes)
                {
                    process.CloseMainWindow();
                }
#endif
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
            MakeSureJoin();
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
            var newName = string.Join(", ", users).FixNickname();

            
            if (currentName.ToLower() == newName.ToLower())
            {
                logger.LogInformation($"Canceled posting, name hasn't changed");
                return;
            }

            var bridgeUser = discordClient
                .Guilds.FirstOrDefault(x => x.Name == server)
                .Users.FirstOrDefault(x => x.Username == username && x.Discriminator == discriminator);

            if (bridgeUser == null) return;

            logger.LogInformation($"Posting: {newName}");
            if (users.Count > 0)
                await bridgeUser.ModifyAsync(x => x.Nickname = newName);
            else
                await bridgeUser.ModifyAsync(x => x.Nickname = username);

            logger.LogInformation($"Done Posting");
            currentName = newName;

            await MakeSureJoin();
        }

        private async Task MakeSureJoin()
        {
#if !DEBUG
            for (int i = 0; i < 20; i++)
            {
                if (IsProcessStarted(discordProcessName)) break;
                await Task.Delay(1000);
            }

            for (int i = 0; i < 5; i++)
            {
                await CheckJoin(i * 5);
                if (isBridgeInChannel) return;
                await Task.Delay(2000);
            }
#endif
        }

        private async Task CheckJoin(int yOffset)
        {
            isBridgeInChannel = discordClient
                .Guilds.FirstOrDefault(x => x.Name == server)
                .VoiceChannels.FirstOrDefault(x => x.Name == voiceChannel)
                .Users.Any(x => x.Username == username && x.Discriminator == discriminator);
            if (!isBridgeInChannel)
                await JoinVoice(yOffset);
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

        public bool IsProcessStarted(string processName)
        {
            // get the process
            Process[] bProcess = Process.GetProcessesByName(processName);

            // check if the process is running
            return bProcess != null && bProcess.Length > 0;
        }

        public bool BringMainWindowToFront(string processName)
        {

            IntPtr lHwnd = FindWindow("Shell_TrayWnd", null);
            SendMessage(lHwnd, WM_COMMAND, (IntPtr)MIN_ALL, IntPtr.Zero);

            // get the process
            Process[] bProcess = Process.GetProcessesByName(processName);

            // check if the process is running
            if (IsProcessStarted(processName))
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
                return true;
            }
            return false;
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
