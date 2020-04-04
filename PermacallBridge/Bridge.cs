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

        private DateTime nextDiscordCheck;
        private DateTime nextTeamspeakCheck;


        public Bridge(Teamspeak teamspeak, Discord discord, ILogger<Bridge> logger)
        {
            this.teamspeak = teamspeak;
            this.discord = discord;
            this.logger = logger;
        }

        public async Task Loop()
        {
            //await Task.Delay(3000);
            //await discord.JoinVoice();
            //await Task.Delay(10000);
            logger.LogInformation("Ready");
            await Task.Delay(5000);
            //await CheckTeamspeak();
            while (true)
            {
                if (DateTime.Now > nextDiscordCheck)
                {
                    try
                    {
                        await CheckDiscord();
                        nextDiscordCheck = DateTime.Now.AddSeconds(30);
                    }
                    catch (Exception e)
                    {
                        Log(e.Message);
                        Log(e.StackTrace);
                    }
                }
                if (DateTime.Now > nextTeamspeakCheck)
                {
                    try
                    {
                        await CheckTeamspeak();
                        nextTeamspeakCheck = DateTime.Now.AddSeconds(30);
                    }
                    catch (Exception e)
                    {
                        Log(e.Message);
                        Log(e.StackTrace);
                    }
                }
                Thread.Sleep(1000);
            }
        }

        private async Task CheckDiscord()
        {
            Log("Checking for discord");
            bool isRunning = teamspeak.IsRunning;
            bool areUsersOnline = await discord.AnyoneOnline();

            Log("Teamspeak " + (isRunning ? "is" : "isn't") + " running and there " + (areUsersOnline ? "are" : "aren't") + " users online on Discord");


            if (isRunning && areUsersOnline)
            {
                Log($"Posting: {string.Join(", ", discord.Users)}");

                await teamspeak.PostNames(discord.Users);
            }

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


            if (isRunning)
            {
                Log($"Posting names: {string.Join(", ", teamspeak.Users)}");
                await discord.PostNames(teamspeak.Users);
            }

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
        }



        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await teamspeak.Initialize();
            await discord.Initialize();
            teamspeak.UsersChanged = async () =>
            {
                nextTeamspeakCheck = DateTime.Now.AddSeconds(1);
            };
            discord.UsersChanged = async () =>
            {
                nextDiscordCheck = DateTime.Now.AddSeconds(1);
            };

            nextDiscordCheck = DateTime.Now;
            nextTeamspeakCheck = DateTime.Now.AddSeconds(15);
            await Loop();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await teamspeak.Disconnect();
            //await discord.Disconnect();
        }

        public void Dispose()
        {
        }
    }
}
