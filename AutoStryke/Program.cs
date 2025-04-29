using AutoStrykeNew.config;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.SlashCommands;
using DSharpPlus.EventArgs;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Collections.Generic;
using AutoStryke.slash;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;


namespace AutoStrykeNew
{
    internal class Program
    {
        public static Dictionary<ulong, Dictionary<ulong, int>> AuraPoints = new();
        private static DiscordClient client;
        private static CommandsNextExtension commands;

        public class ValorantComp
        {
            public string Map { get; set; } = "";
            public List<string> Agents { get; set; } = new();
        }

        static async Task Main(string[] args)
        {
            LoadAuraPoints();

            var jsonreader = new jsonreader();
            await jsonreader.ReadJSON();

            var discordconfig = new DiscordConfiguration()
            {
                Intents = DiscordIntents.All,
                Token = jsonreader.token,
                TokenType = TokenType.Bot,
                AutoReconnect = true,
            };

            client = new DiscordClient(discordconfig);

            client.UseInteractivity(new DSharpPlus.Interactivity.InteractivityConfiguration
            {
                Timeout = TimeSpan.FromMinutes(2)
            });

            client.Ready += Client_Ready;

            var commandsconfig = new CommandsNextConfiguration()
            {
                StringPrefixes = new string[] { jsonreader.prefix },
                EnableDms = true,
                EnableMentionPrefix = true,
                EnableDefaultHelp = true,
            };

            client.MessageCreated += async (s, e) =>
            {
                if (e.Author.IsBot) return;

                if (e.Author.Id == 791982380801327115)
                {
                    var emoji = e.Guild.Emojis.Values.FirstOrDefault(x => x.Name == "benerd");
                    if (emoji != null)
                        await e.Message.CreateReactionAsync(emoji);
                    else
                        Console.WriteLine("Emoji not found!");
                }
            };

            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await CheckForResultPrompts();
                    await Task.Delay(TimeSpan.FromMinutes(5));
                }
            });

            client.ComponentInteractionCreated += async (s, e) =>
            {
                if (e.Interaction.Data.CustomId.StartsWith("submit_result_"))
                {
                    var parts = e.Interaction.Data.CustomId.Split('_');
                    if (parts.Length < 5) return;

                    string opponent = parts[2];
                    string map = parts[3];
                    string dateStr = parts[4];

                    var modal = new DiscordInteractionResponseBuilder()
                        .WithTitle($"Result vs {opponent}")
                        .WithCustomId($"modal_result_{opponent}_{map}_{dateStr}")
                        .AddComponents(
                            new TextInputComponent("Our Score", "our_score", required: true, placeholder: "e.g. 13", style: TextInputStyle.Short),
                            new TextInputComponent("Their Score", "their_score", required: true, placeholder: "e.g. 7", style: TextInputStyle.Short)
                        );

                    await e.Interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
                }
            };

            commands = client.UseCommandsNext(commandsconfig);

            var slashcommandsconfig = client.UseSlashCommands();
            slashcommandsconfig.RegisterCommands<slashcommandstest>();

            commands.RegisterCommands<Commands.Commands>();

            await client.ConnectAsync();
            await Task.Delay(-1);
        }

        private static Task Client_Ready(DiscordClient sender, ReadyEventArgs args)
        {
            Console.WriteLine("Bot is ready");
            return Task.CompletedTask;
        }

        public static void SaveAuraPoints()
        {
            try
            {
                var json = JsonConvert.SerializeObject(AuraPoints, Formatting.Indented);
                File.WriteAllText("auraPoints.json", json);
                Console.WriteLine("[DEBUG] Aura points saved.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to save aura points: {ex.Message}");
            }
        }

        public static void LoadAuraPoints()
        {
            try
            {
                if (File.Exists("auraPoints.json"))
                {
                    var json = File.ReadAllText("auraPoints.json");
                    AuraPoints = JsonConvert.DeserializeObject<Dictionary<ulong, Dictionary<ulong, int>>>(json)
                                 ?? new();
                    Console.WriteLine("[DEBUG] Aura points loaded.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to load aura points: {ex.Message}");
                AuraPoints = new();
            }
        }

        private const string compsFilePath = "comps.json";

        public static void SaveComps(Dictionary<string, ValorantComp> comps)
        {
            var json = JsonConvert.SerializeObject(comps, Formatting.Indented);
            File.WriteAllText(compsFilePath, json);
        }

        public static Dictionary<string, ValorantComp> LoadComps()
        {
            if (!File.Exists(compsFilePath))
                return new();

            var json = File.ReadAllText(compsFilePath);
            return JsonConvert.DeserializeObject<Dictionary<string, ValorantComp>>(json) ?? new();
        }

        public class MatchResult
        {
            public string Opponent { get; set; }
            public string Map { get; set; }
            public int OurScore { get; set; }
            public int TheirScore { get; set; }
            public DateTime Date { get; set; }
        }

        private const string matchResultsFilePath = "matchResults.json";

        public static void SaveMatchResults(List<MatchResult> results)
        {
            var json = JsonConvert.SerializeObject(results, Formatting.Indented);
            File.WriteAllText(matchResultsFilePath, json);
        }

        public static List<MatchResult> LoadMatchResults()
        {
            if (!File.Exists(matchResultsFilePath))
                return new();

            var json = File.ReadAllText(matchResultsFilePath);
            return JsonConvert.DeserializeObject<List<MatchResult>>(json) ?? new();
        }

        public class ScheduleEntry
        {
            public string Opponent { get; set; }
            public string Map { get; set; }
            public DateTime Date { get; set; }
            public ulong ChannelId { get; set; }
        }

        private const string scheduleFilePath = "schedule.json";

        public static List<ScheduleEntry> LoadSchedule()
        {
            if (!File.Exists(scheduleFilePath))
                return new();

            var json = File.ReadAllText(scheduleFilePath);
            return JsonConvert.DeserializeObject<List<ScheduleEntry>>(json) ?? new();
        }

        public static async Task CheckForResultPrompts()
        {
            var schedules = LoadSchedule();
            var results = LoadMatchResults();

            foreach (var match in schedules)
            {
                bool alreadySubmitted = results.Any(r => r.Date.Date == match.Date.Date && r.Opponent == match.Opponent);
                if (alreadySubmitted) continue;

                var matchEndTime = match.Date.AddHours(1);
                if (DateTime.UtcNow >= matchEndTime && DateTime.UtcNow <= matchEndTime.AddMinutes(5))
                {
                    var channel = await client.GetChannelAsync(match.ChannelId);
                    await channel.SendMessageAsync("📝 It's time to submit the match result:");

                    await channel.SendMessageAsync(new DiscordMessageBuilder()
                        .WithContent("Click the button to submit the result")
                        .AddComponents(new DiscordButtonComponent(ButtonStyle.Primary, $"submit_result_{match.Opponent}_{match.Map}_{match.Date:yyyyMMdd}", "Submit Result")));
                }
            }
        }
    }
}
