using DSharpPlus.SlashCommands;
using DSharpPlus;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoStrykeNew;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using DSharpPlus.Interactivity.Extensions;

namespace AutoStryke.slash
{
    public class slashcommandstest : ApplicationCommandModule
    {
        // ============================================================
        // SCRIM / MATCH SCHEDULING
        // ============================================================

        private const ulong StrykeID = 889088075395923998;
        private static string jsonFilePath = "scrims.json";

        [SlashCommand("Creatematch", "Schedule a scrim or match")]
        public async Task ScrimsCommand(InteractionContext ctx,
            [Option("type", "Is this a Scrim or Match?")]
                [Choice("Scrim", "scrim")]
                [Choice("Match", "match")]
                [Choice("VODreview", "vodreview")]
                string matchType,
            [Option("timecode", "Unix timestamp for the scrim in <t:1745914353> format")] string timecode,
            [Option("map", "Map to be played")] string map,
            [Option("enemy_team", "Name of the enemy team")] string enemyTeam,
            [Option("Apollolink", "Link to the apollo post")] string? apolloLink = null)
        {
            if (ctx.User.Id != StrykeID)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("Only Stryke can use this command.")
                                                            .AsEphemeral(true));
                return;
            }

            var match = Regex.Match(timecode, @"<t:(\d+)>");

            if (!match.Success)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("Invalid timecode format. Please use the format <t:1745914353>.")
                                                            .AsEphemeral(true));
                return;
            }

            long timestamp = long.Parse(match.Groups[1].Value);
            DateTime scrimDate = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;

            string titlePrefix = matchType.ToLower() switch
            {
                "scrim" => "📝 Scrim Scheduled",
                "match" => "⚔️ Match Scheduled",
                "vodreview" => "🎥 VOD Review Scheduled",
                _ => "❓ Unknown Type Scheduled"
            };

            var embed = new DiscordEmbedBuilder
            {
                Title = titlePrefix,
                Color = DiscordColor.Cyan,
                Description = $"**Date & Time:** <t:{timestamp}> (<t:{timestamp}:R>)\n" +
                              $"**Map:** {map}\n" +
                              $"**Enemy Team:** {enemyTeam}"
            };

            if (!string.IsNullOrWhiteSpace(apolloLink))
            {
                embed.Description += $"\n**Sign Up Here:** [Apollo Link]({apolloLink})";
            }

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().AddEmbed(embed));

            var scrimData = new ScrimData
            {
                Timecode = timestamp,
                Map = map,
                EnemyTeam = enemyTeam,
                MatchType = matchType,
                apolloLink = apolloLink
            };

            var scrims = LoadScrims();
            scrims.Add(scrimData);
            SaveScrims(scrims);
        }

        public class ScrimData
        {
            public long Timecode { get; set; }
            public string Map { get; set; }
            public string EnemyTeam { get; set; }
            public string MatchType { get; set; }
            public string? apolloLink { get; set; }
        }

        public void SaveScrims(List<ScrimData> scrims)
        {
            var json = JsonConvert.SerializeObject(scrims, Formatting.Indented);
            File.WriteAllText(jsonFilePath, json);
        }

        public List<ScrimData> LoadScrims()
        {
            if (!File.Exists(jsonFilePath))
            {
                return new List<ScrimData>();
            }

            var json = File.ReadAllText(jsonFilePath);
            return JsonConvert.DeserializeObject<List<ScrimData>>(json) ?? new List<ScrimData>();
        }

        [SlashCommand("matches", "View the next 5 upcoming scrims and matches")]
        public async Task ViewScrimsCommand(InteractionContext ctx)
        {
            var scrims = LoadScrims();

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var upcoming = scrims
                .Where(s => s.Timecode > now)
                .OrderBy(s => s.Timecode)
                .Take(5)
                .ToList();

            if (!upcoming.Any())
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("📭 No upcoming scrims found.")
                        .AsEphemeral(false));
                return;
            }

            var embed = new DiscordEmbedBuilder
            {
                Title = "📅 Upcoming Events",
                Color = DiscordColor.Azure
            };

            foreach (var scrim in upcoming.Select((value, index) => new { value, index }))
            {
                string emoji = scrim.value.MatchType.ToLower() switch
                {
                    "scrim" => "📝",
                    "match" => "⚔️",
                    "vodreview" => "🎥",
                    _ => "❓"
                };

                string fieldTitle = $"{emoji} {scrim.value.EnemyTeam} on {scrim.value.Map}";

                string fieldContent = $"**Date & Time:** <t:{scrim.value.Timecode}> (<t:{scrim.value.Timecode}:R>)\n" +
                                      $"**Map:** {scrim.value.Map}\n" +
                                      $"**Enemy Team:** {scrim.value.EnemyTeam}";

                if (!string.IsNullOrWhiteSpace(scrim.value.apolloLink))
                {
                    fieldContent += $"\n**Sign Up Here:** [Apollo Link]({scrim.value.apolloLink})";
                }

                embed.AddField(fieldTitle, fieldContent, inline: false);

                if (scrim.index < upcoming.Count() - 1)
                {
                    embed.AddField("\u200B", "\u200B", inline: false);
                }
            }

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().AddEmbed(embed));
        }

        [SlashCommand("deletematch", "Delete a match from the list")]
        public async Task DeleteScrimCommand(InteractionContext ctx)
        {
            if (ctx.User.Id != StrykeID)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("Only Stryke can use this command.")
                        .AsEphemeral(true));
                return;
            }

            var scrims = LoadScrims();

            if (!scrims.Any())
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("No scrims found.")
                        .AsEphemeral(true));
                return;
            }

            var embed = new DiscordEmbedBuilder
            {
                Title = "🗑️ Scrims List - Select a number to delete or type 'cancel' to cancel",
                Color = DiscordColor.Red
            };

            for (int i = 0; i < scrims.Count; i++)
            {
                var s = scrims[i];
                string emoji = s.MatchType.ToLower() switch
                {
                    "scrim" => "📝",
                    "match" => "⚔️",
                    "vodreview" => "🎥",
                    _ => "❓"
                };
                embed.AddField($"#{i + 1}: {emoji} {s.EnemyTeam} on {s.Map}", $"<t:{s.Timecode}>", false);
            }

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(embed)
                    .AsEphemeral(true));

            var interactivity = ctx.Client.GetInteractivity();
            var response = await interactivity.WaitForMessageAsync(
                x => x.Author.Id == ctx.User.Id &&
                     x.ChannelId == ctx.Channel.Id, TimeSpan.FromSeconds(120));

            if (!response.TimedOut)
            {
                if (response.Result.Content.ToLower() == "cancel")
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ Command canceled.")
                            .AsEphemeral(true));
                    return;
                }

                if (int.TryParse(response.Result.Content, out int index) &&
                    index >= 1 && index <= scrims.Count)
                {
                    scrims.RemoveAt(index - 1);
                    SaveScrims(scrims);

                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent($"✅ Scrim #{index} deleted.")
                            .AsEphemeral(true));
                }
                else
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent($"❌ Invalid input. Please enter a number between 1 and {scrims.Count}, or 'cancel' to cancel.")
                            .AsEphemeral(true));
                }
            }
            else
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ Timed out waiting for a response.")
                        .AsEphemeral(true));
            }
        }

        // ============================================================
        // MATCH RESULTS
        // ============================================================

        [SlashCommand("matchresults", "View previous match results.")]
        public async Task MatchResults(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var results = Program.LoadMatchResults();

            if (results == null || results.Count == 0)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("❌ No match results have been submitted yet."));
                return;
            }

            var latestResults = results.OrderByDescending(r => r.Date).Take(5);

            var embed = new DiscordEmbedBuilder()
                .WithTitle("📊 Recent Match Results")
                .WithColor(DiscordColor.Blurple);

            foreach (var result in latestResults)
            {
                string outcome = result.OurScore > result.TheirScore ? "✅ Win" :
                                 result.OurScore < result.TheirScore ? "❌ Loss" : "➖ Draw";

                embed.AddField($"{result.Map} vs {result.Opponent} — {outcome}", $"{result.OurScore} - {result.TheirScore} on {result.Date:dd MMM yyyy}", inline: false);
            }

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed));
        }
    }
}