// Program.cs
using AutoStrykeNew.config;
using AutoStryke.slash;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.SlashCommands;
using DSharpPlus.EventArgs;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AutoStrykeNew
{
    internal class Program
    {


        private static DiscordClient client;

        static async Task Main(string[] args)
        {
            var jsonreader = new jsonreader();
            await jsonreader.ReadJSON();

            client = BuildClient(jsonreader);

            RegisterEventHandlers(client);
            RegisterBackgroundTasks();

            var commandsNext = client.UseCommandsNext(BuildCommandsNextConfig(jsonreader));
            commandsNext.RegisterCommands<Commands.Commands>();

            var slash = client.UseSlashCommands();
            slash.RegisterCommands<slashcommandstest>();
            slash.RegisterCommands<CompsCommands>();
            slash.RegisterCommands<ProCompCommands>();

            await client.ConnectAsync();
            await Task.Delay(-1);
        }

        private static DiscordClient BuildClient(jsonreader jsonreader)
        {
            var discordConfig = new DiscordConfiguration
            {
                Intents = DiscordIntents.All,
                Token = jsonreader.token,
                TokenType = TokenType.Bot,
                AutoReconnect = true,
            };

            var newClient = new DiscordClient(discordConfig);

            newClient.UseInteractivity(new DSharpPlus.Interactivity.InteractivityConfiguration
            {
                Timeout = TimeSpan.FromMinutes(2)
            });

            return newClient;
        }

        private static CommandsNextConfiguration BuildCommandsNextConfig(jsonreader jsonreader)
        {
            return new CommandsNextConfiguration
            {
                StringPrefixes = new[] { jsonreader.prefix },
                EnableDms = true,
                EnableMentionPrefix = true,
                EnableDefaultHelp = true,
            };
        }

        private static void RegisterEventHandlers(DiscordClient discordClient)
        {
            discordClient.Ready += Client_Ready;

            discordClient.MessageCreated += async (s, e) =>
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

            discordClient.ComponentInteractionCreated += async (s, e) =>
            {
                if (!e.Interaction.Data.CustomId.StartsWith("submit_result_"))
                    return;

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
            };
        }

        private static Task Client_Ready(DiscordClient sender, ReadyEventArgs args)
        {
            Console.WriteLine("Bot is ready");
            return Task.CompletedTask;
        }

        private static void RegisterBackgroundTasks()
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await CheckForResultPrompts();
                    await Task.Delay(TimeSpan.FromMinutes(5));
                }
            });
        }

        public class ValorantComp
        {
            public string Map { get; set; } = "";
            public List<string> Agents { get; set; } = new();
        }

        public class MatchResult
        {
            public string Opponent { get; set; }
            public string Map { get; set; }
            public int OurScore { get; set; }
            public int TheirScore { get; set; }
            public DateTime Date { get; set; }
        }

        public class ScheduleEntry
        {
            public string Opponent { get; set; }
            public string Map { get; set; }
            public DateTime Date { get; set; }
            public ulong ChannelId { get; set; }
        }

        private const string compsFilePath = "comps.json";
        private const string matchResultsFilePath = "matchResults.json";
        private const string scheduleFilePath = "schedule.json";

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