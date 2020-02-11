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
        private readonly Teamspeak teamspeak;
        private readonly Discord discord;
        private readonly ILogger<Bridge> logger;

        public Bridge(Teamspeak teamspeak, Discord discord, ILogger<Bridge> logger)
        {
            this.teamspeak = teamspeak;
            this.discord = discord;
            this.logger = logger;
        }

        public async Task Loop()
        {
            await Task.Delay(3000);
            logger.LogInformation("Ready");
            await CheckTeamspeak();
            while (true)
            {
                try
                {
                    await CheckDiscord();
                    Thread.Sleep(10000);

                }
                catch (Exception e)
                {
                    Log(e.Message);
                    Log(e.StackTrace);
                }
                try
                {
                    await CheckTeamspeak();
                    Thread.Sleep(10000);
                }
                catch (Exception e)
                {
                    Log(e.Message);
                    Log(e.StackTrace);
                }
            }
        }

        private async Task CheckDiscord()
        {
            Log("Checking for discord");
            bool isRunning = teamspeak.IsRunning;
            bool areUsersOnline = await discord.AnyoneOnline();

            Log("Teamspeak " + (isRunning ? "is" : "isn't") + " running and there " + (areUsersOnline ? "are" : "aren't") + " users online on Discord");

            if (!isRunning && areUsersOnline)
            {
                Log("Starting Teamspeak");
                await teamspeak.Run();
            }

            if (isRunning && !areUsersOnline)
            {
                Log("Stopping Teamspeak");
                await teamspeak.Quit();
            }
        }

        private void Log(string msg)
        {
            logger.LogInformation(msg);
        }

        private async Task CheckTeamspeak()
        {
            Log("Checking for teamspeak");
            bool isRunning = discord.IsRunning;
            bool areUsersOnline = await teamspeak.AnyoneOnline();

            Log("Discord " + (isRunning ? "is" : "isn't") + " running and there " + (areUsersOnline ? "are" : "aren't") + " users online on Teamspeak");

            if (!isRunning && areUsersOnline)
            {
                Log("Starting Discord");
                await discord.Run();
            }

            if (isRunning && !areUsersOnline)
            {
                Log("Stopping Discord");
                await discord.Quit();
            }

            if(isRunning)
            {
                discord.PostNames(teamspeak.Users);
            }
        }

        

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Loop();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
