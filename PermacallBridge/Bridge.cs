﻿using Discord;
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
    public class Bridge : IHostedService
    {
        private readonly Teamspeak teamspeak;
        private readonly Discord discord;
        private readonly ILogger<Bridge> logger;
        private readonly IHostApplicationLifetime appLifetime;
        private DateTime nextDiscordCheck;
        private DateTime nextTeamspeakCheck;

        private List<ChatMessage> discordMessageQueue = new List<ChatMessage>();
        private List<ChatMessage> teamspeakMessageQueue = new List<ChatMessage>();

        public Bridge(Teamspeak teamspeak, Discord discord, ILogger<Bridge> logger, IHostApplicationLifetime applicationLifetime)
        {
            this.teamspeak = teamspeak;
            this.discord = discord;
            this.logger = logger;
            appLifetime = applicationLifetime;
        }

        public async Task Loop()
        {
            //await Task.Delay(3000);
            //await discord.JoinVoice();
            //await Task.Delay(10000);
            logger.LogInformation("Ready");
            //await CheckTeamspeak();
            while (true)
            {
                try
                {
                    if (discordMessageQueue.Any())
                    {
                        var msg = string.Join("\n", discordMessageQueue
                            .Select(x => $"{x.User}: {x.Message}"));

                        logger.LogInformation($"Sending {msg}");
                        discord.SendChatMessage(msg);
                        discordMessageQueue.Clear();
                    }
                    if (teamspeakMessageQueue.Any())
                    {
                        var msg = string.Join("\n", teamspeakMessageQueue
                            .Select(x => $"{x.User}: {x.Message}"));

                        logger.LogInformation($"Sending {msg}");
                        teamspeak.SendChatMessage(msg);
                        teamspeakMessageQueue.Clear();
                    }
                }
                catch (Exception e)
                {
                    Log(e.Message);
                    Log(e.StackTrace);
                }

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
            appLifetime.ApplicationStopping.Register(() =>
            {
                Stop();
            });

            bool teamspeakInitialized = false;
            while (!teamspeakInitialized)
            {
                try
                {
                    await teamspeak.Initialize();

                    nextTeamspeakCheck = DateTime.Now;
                    teamspeakInitialized = true;
                }
                catch (Exception)
                {
                    teamspeakInitialized = false;
                }
            }


            bool discordInitialized = false;
            while (!discordInitialized)
            {
                try
                {
                    await discord.Initialize();

                    nextDiscordCheck = DateTime.Now;
                    discordInitialized = true;
                }
                catch (Exception)
                {
                    discordInitialized = false;
                }
            }


            teamspeak.UsersChanged = async ()
                => nextTeamspeakCheck = DateTime.Now.AddSeconds(1);

            teamspeak.SendChatMessageEvent = async (string username, string message)
                =>
            {
                //discordMessageQueue.Add(new ChatMessage(username, message));
            };
//            await discord.SendChatMessage(username, message);


            discord.UsersChanged = async ()
                => nextDiscordCheck = DateTime.Now.AddSeconds(1);

            discord.SendChatMessageEvent = async (string username, string message)
                =>
            {
                //teamspeakMessageQueue.Add(new ChatMessage(username, message));
            };
            //=> await teamspeak.SendChatMessage(username, message);


            await Loop();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Stop();
            //await discord.Disconnect();
        }

        private void Stop()
        {
            teamspeak.UsersChanged = null;
            discord.UsersChanged = null;

            nextDiscordCheck = DateTime.Now.AddMinutes(1);
            nextTeamspeakCheck = DateTime.Now.AddMinutes(1);

            Log("Stopping Discord...");
            discord.Quit();
            Log("Stopping Teamspeak...");
            Task tsQuit = teamspeak.Quit();
            Log("Disconnecting Teamspeak...");
            Task tsDc = teamspeak.Disconnect();

            tsQuit.Wait();
            tsDc.Wait();
        }
    }
}
