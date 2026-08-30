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
        // ============================================================
        // STATE
        // ============================================================

        private static DiscordClient client;

        // ============================================================
        // ENTRY POINT
        // ============================================================

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

            // ONE-TIME CLEANUP: clears leftover guild-scoped commands from
            // earlier testing so they stop duplicating the global ones.
            // Remove this block after running it once successfully.
            //await client.BulkOverwriteGuildApplicationCommandsAsync(
            //    1538210640420802662, Array.Empty<DiscordApplicationCommand>());
            //Console.WriteLine("Cleared guild-scoped commands for the test server.");

            await Task.Delay(-1);
        }

        // ============================================================
        // CLIENT / CONFIG SETUP
        // ============================================================

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

            // Reacts with the "benerd" emoji whenever a specific user posts.
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

            // Handles the "Submit Result" button by popping open a score-entry modal.
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

            // Handles the "Show all N" button under /findprocomp results by
            // re-running the same lookup (encoded in the button's custom ID)
            // without the initial 10-row display cap.
            discordClient.ComponentInteractionCreated += async (s, e) =>
            {
                if (!e.Interaction.Data.CustomId.StartsWith("findprocomp_showall|"))
                    return;

                var parts = e.Interaction.Data.CustomId.Split('|');
                if (parts.Length != 3) return;

                var queryAgents = parts[1].Split(',');
                var mapName = parts[2];

                await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);

                var (scored, error) = await ProCompCommands.GetScoredComps(queryAgents, mapName);

                if (error is not null)
                {
                    await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent(error));
                    return;
                }

                var embed = ProCompCommands.BuildEmbed(scored, queryAgents, mapName, scored.Count);

                // No components this time - the full list is now shown, so
                // there's nothing left for the button to do.
                await e.Interaction.EditOriginalResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(embed));
            };

            // Handles the "Save to team" button under /findprocomp results.
            // First asks which specific result the user wants to save, since
            // similarity matches differ from what was actually searched for.
            discordClient.ComponentInteractionCreated += async (s, e) =>
            {
                if (!e.Interaction.Data.CustomId.StartsWith("findprocomp_save|"))
                    return;

                var payload = e.Interaction.Data.CustomId.Substring("findprocomp_save|".Length);
                var parts = payload.Split('|');
                if (parts.Length != 2) return;

                var queryAgents = parts[0].Split(',');
                var mapName = parts[1];

                var (scored, error) = await ProCompCommands.GetScoredComps(queryAgents, mapName);

                if (error is not null || scored.Count == 0)
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent(error ?? "No comps to save.")
                            .AsEphemeral(true));
                    return;
                }

                var options = scored
                    .Take(25)
                    .Select((c, index) => new DiscordSelectComponentOption(
                        $"{c.Team} — {c.Similarity:P0}",
                        index.ToString(),
                        c.Comp.Length > 100 ? c.Comp.Substring(0, 100) : c.Comp))
                    .ToList();

                var select = new DiscordSelectComponent(
                    $"findprocomp_pickcomp|{payload}", "Which comp do you want to save?", options);

                var note = scored.Count > 25 ? $"\n(Showing the top 25 of {scored.Count} matches.)" : "";

                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"Which comp would you like to save?{note}")
                        .AddComponents(select)
                        .AsEphemeral(true));
            };

            // Handles picking a specific result from the dropdown above, then
            // moves on to asking which of the user's own teams to save it under.
            discordClient.ComponentInteractionCreated += async (s, e) =>
            {
                if (!e.Interaction.Data.CustomId.StartsWith("findprocomp_pickcomp|"))
                    return;

                var payload = e.Interaction.Data.CustomId.Substring("findprocomp_pickcomp|".Length);
                var parts = payload.Split('|');
                if (parts.Length != 2) return;

                var queryAgents = parts[0].Split(',');
                var mapName = parts[1];

                if (!int.TryParse(e.Interaction.Data.Values.FirstOrDefault(), out var chosenIndex))
                    return;

                // Recompute the same scored list (deterministic given an unchanged
                // database) so we know exactly which comp was picked.
                var (scored, error) = await ProCompCommands.GetScoredComps(queryAgents, mapName);

                if (error is not null || chosenIndex < 0 || chosenIndex >= scored.Count)
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                        new DiscordInteractionResponseBuilder()
                            .WithContent(error ?? "That comp couldn't be found anymore - please try again.")
                            .AsEphemeral(true));
                    return;
                }

                var chosen = scored[chosenIndex];
                var chosenAgentsCsv = string.Join(",", chosen.Comp.Split(" / ", StringSplitOptions.None));
                var nextPayload = $"{chosenAgentsCsv}|{mapName}";

                var existingTeams = CompsCommands.GetAllTeamNames();

                if (existingTeams.Count == 0)
                {
                    var modal = new DiscordInteractionResponseBuilder()
                        .WithTitle("Save this comp to a team")
                        .WithCustomId($"modal_save_comp|{nextPayload}")
                        .AddComponents(new TextInputComponent(
                            "Team name", "team_name", required: true, placeholder: "e.g. My Team", style: TextInputStyle.Short));

                    await e.Interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
                    return;
                }

                const string newTeamValue = "__new_team__";

                var teamOptions = existingTeams
                    .Take(24)
                    .Select(team => new DiscordSelectComponentOption(team, team))
                    .Append(new DiscordSelectComponentOption("➕ New team...", newTeamValue))
                    .ToList();

                var teamSelect = new DiscordSelectComponent(
                    $"findprocomp_save_select|{nextPayload}", "Which team is this for?", teamOptions);

                await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"Saving **{chosen.Comp}** ({chosen.Team} — {chosen.Similarity:P0}). Which team is this for?")
                        .AddComponents(teamSelect)
                        .AsEphemeral(true));
            };

            // Handles picking a team (or "new team") from the dropdown above.
            discordClient.ComponentInteractionCreated += async (s, e) =>
            {
                if (!e.Interaction.Data.CustomId.StartsWith("findprocomp_save_select|"))
                    return;

                var payload = e.Interaction.Data.CustomId.Substring("findprocomp_save_select|".Length);
                var choice = e.Interaction.Data.Values.FirstOrDefault() ?? "";

                if (choice == "__new_team__")
                {
                    var modal = new DiscordInteractionResponseBuilder()
                        .WithTitle("Save this comp to a team")
                        .WithCustomId($"modal_save_comp|{payload}")
                        .AddComponents(new TextInputComponent(
                            "Team name", "team_name", required: true, placeholder: "e.g. My Team", style: TextInputStyle.Short));

                    await e.Interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
                    return;
                }

                var parts = payload.Split('|');
                if (parts.Length != 2) return;

                var agents = parts[0].Split(',').ToList();
                var mapName = parts[1];

                CompsCommands.SaveCompForTeam(choice, mapName, agents);

                await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"Saved **{string.Join(" / ", agents)}** for **{choice}** on **{mapName}**. Check it with `/comps`.")
                        .AsEphemeral(true));
            };

            // Handles the team-name modal submitted when the user picks "new team",
            // actually writing the comp into comps.json (same store as /addcomp).
            discordClient.ModalSubmitted += async (s, e) =>
            {
                if (!e.Interaction.Data.CustomId.StartsWith("modal_save_comp|"))
                    return;

                var payload = e.Interaction.Data.CustomId.Substring("modal_save_comp|".Length);
                var parts = payload.Split('|');
                if (parts.Length != 2) return;

                var agents = parts[0].Split(',').ToList();
                var mapName = parts[1];
                var teamName = e.Values.TryGetValue("team_name", out var value) ? value.Trim() : "";

                if (string.IsNullOrWhiteSpace(teamName))
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("Team name can't be empty - nothing was saved.")
                            .AsEphemeral(true));
                    return;
                }

                CompsCommands.SaveCompForTeam(teamName, mapName, agents);

                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"Saved **{string.Join(" / ", agents)}** for **{teamName}** on **{mapName}**. Check it with `/comps`.")
                        .AsEphemeral(true));
            };

            // Handles picking a map to edit from /comps' dropdown - opens a
            // modal pre-filled with that team's current comp on that map.
            discordClient.ComponentInteractionCreated += async (s, e) =>
            {
                if (!e.Interaction.Data.CustomId.StartsWith("comps_edit_pick|"))
                    return;

                var team = e.Interaction.Data.CustomId.Substring("comps_edit_pick|".Length);
                var map = e.Interaction.Data.Values.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(map)) return;

                var db = CompsCommands.LoadDatabase();
                var currentAgents = (db.ContainsKey(map) && db[map].ContainsKey(team))
                    ? string.Join(", ", db[map][team])
                    : "";

                var modal = new DiscordInteractionResponseBuilder()
                    .WithTitle($"Edit {team} on {map}")
                    .WithCustomId($"modal_edit_comp|{team}|{map}")
                    .AddComponents(new TextInputComponent(
                        "Agents (comma-separated, 5 total)", "agents_input",
                        value: currentAgents, required: true, style: TextInputStyle.Short));

                await e.Interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
            };

            // Handles the edit modal submitted above, overwriting that comp.
            discordClient.ModalSubmitted += async (s, e) =>
            {
                if (!e.Interaction.Data.CustomId.StartsWith("modal_edit_comp|"))
                    return;

                var payload = e.Interaction.Data.CustomId.Substring("modal_edit_comp|".Length);
                var parts = payload.Split('|');
                if (parts.Length != 2) return;

                var team = parts[0];
                var map = parts[1];
                var agentsInput = e.Values.TryGetValue("agents_input", out var value) ? value : "";

                var agents = agentsInput.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(a => a.Trim())
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .ToList();

                if (agents.Count != 5)
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent($"That's {agents.Count} agents, not 5 - nothing was changed.")
                            .AsEphemeral(true));
                    return;
                }

                CompsCommands.SaveCompForTeam(team, map, agents);

                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"Updated **{team}** on **{map}**: {string.Join(" / ", agents)}")
                        .AsEphemeral(true));
            };
        }

        private static Task Client_Ready(DiscordClient sender, ReadyEventArgs args)
        {
            Console.WriteLine("Bot is ready");
            return Task.CompletedTask;
        }

        private static void RegisterBackgroundTasks()
        {
            // Every 5 minutes, check whether any scheduled matches have just
            // ended and need a "submit result" prompt posted.
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await CheckForResultPrompts();
                    await Task.Delay(TimeSpan.FromMinutes(5));
                }
            });

            // Every 10 minutes, check for newly completed Premier matches via
            // HenrikDev's API and auto-fill them into matchResults.json.
            _ = Task.Run(async () =>
            {
                var jsonreader = new jsonreader();
                await jsonreader.ReadJSON();

                while (true)
                {
                    try
                    {
                        var added = await PremierResultsPoller.CheckForNewResults(
                            jsonreader.henrikApiKey,
                            jsonreader.premierTeamName,
                            jsonreader.premierTeamTag,
                            jsonreader.premierRegion);

                        if (added > 0)
                            Console.WriteLine($"Added {added} new Premier result(s).");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Premier polling failed: {ex.Message}");
                    }

                    await Task.Delay(TimeSpan.FromMinutes(10));
                }
            });
        }

        // ============================================================
        // MATCH SCHEDULING / RESULTS
        // ============================================================

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

        private const string matchResultsFilePath = "matchResults.json";
        private const string scheduleFilePath = "schedule.json";

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