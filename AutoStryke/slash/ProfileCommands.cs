using DSharpPlus;
using DSharpPlus.SlashCommands;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AutoStryke.slash
{
    public record KrillionEntry(string Username, int Score, string RawShare, DateTime SubmittedAt);
    public record FermiEntry(string Username, double Score, string RawShare, DateTime SubmittedAt);

    public class ProfileCommands : ApplicationCommandModule
    {
        private static Dictionary<int, Dictionary<ulong, KrillionEntry>> LoadKrillionData()
        {
            const string JsonFile = "krillion_results.json";
            if (!File.Exists(JsonFile))
                return new Dictionary<int, Dictionary<ulong, KrillionEntry>>();

            var json = File.ReadAllText(JsonFile);
            return JsonSerializer.Deserialize<Dictionary<int, Dictionary<ulong, KrillionEntry>>>(json)
                ?? new Dictionary<int, Dictionary<ulong, KrillionEntry>>();
        }

        private static Dictionary<int, Dictionary<ulong, FermiEntry>> LoadFermiData()
        {
            const string JsonFile = "fermi_results.json";
            if (!File.Exists(JsonFile))
                return new Dictionary<int, Dictionary<ulong, FermiEntry>>();

            var json = File.ReadAllText(JsonFile);
            return JsonSerializer.Deserialize<Dictionary<int, Dictionary<ulong, FermiEntry>>>(json)
                ?? new Dictionary<int, Dictionary<ulong, FermiEntry>>();
        }

        [SlashCommand("profile", "View your complete gaming profile with stats from all games")]
        public async Task Profile(
            InteractionContext ctx,
            [Option("user", "Whose profile to view (defaults to you)")] DiscordUser? user = null)
        {
            Console.WriteLine($"[PROFILE] /profile command executed by {ctx.User.Username} (ID: {ctx.User.Id})");
            var target = user ?? ctx.User;
            Console.WriteLine($"[PROFILE] Viewing profile for: {target.Username} (ID: {target.Id})");
            
            var krillionData = LoadKrillionData();
            var fermiData = LoadFermiData();

            var krillionEntries = krillionData
                .Where(kv => kv.Value.ContainsKey(target.Id))
                .Select(kv => (Puzzle: kv.Key, Score: kv.Value[target.Id].Score))
                .OrderBy(x => x.Puzzle)
                .ToList();

            var fermiEntries = fermiData
                .Where(kv => kv.Value.ContainsKey(target.Id))
                .Select(kv => (Puzzle: kv.Key, Score: kv.Value[target.Id].Score))
                .OrderBy(x => x.Puzzle)
                .ToList();

            var hasKrillion = krillionEntries.Count > 0;
            var hasFermi = fermiEntries.Count > 0;

            if (!hasKrillion && !hasFermi)
            {
                Console.WriteLine($"[PROFILE] No game results found for {target.Username}");
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"**{target.Username}** hasn't submitted any game results yet. Use `/krillion` or `/fermi` to get started!")
                        .AsEphemeral(true));
                return;
            }

            var embed = new DiscordEmbedBuilder()
                .WithTitle($"🎮 {target.Username}'s Gaming Profile")
                .WithColor(DiscordColor.Gold)
                .WithThumbnail(target.AvatarUrl)
                .WithTimestamp(DateTime.UtcNow);

            // Krillion Stats Section
            if (hasKrillion)
            {
                var scores = krillionEntries.Select(e => (double)e.Score).ToList();
                var mean = scores.Average();
                var best = scores.Max();
                var worst = scores.Min();
                var (currentStreak, longestStreak) = ComputeStreaks(krillionEntries.Select(e => e.Puzzle).ToList());

                var allKrillionAverages = krillionData
                    .SelectMany(p => p.Value.Select(e => (UserId: e.Key, e.Value.Score)))
                    .GroupBy(x => x.UserId)
                    .Select(g => (UserId: g.Key, Avg: g.Average(x => x.Score)))
                    .OrderByDescending(x => x.Avg)
                    .ToList();
                var krillionRank = allKrillionAverages.FindIndex(x => x.UserId == target.Id) + 1;

                embed.AddField("🦐 Krillion Stats", 
                    $"**Days Played:** {scores.Count}\n" +
                    $"**Best Score:** {best:0.#}\n" +
                    $"**Average:** {mean:0.##}\n" +
                    $"**Current Streak:** {currentStreak} day{(currentStreak == 1 ? "" : "s")}\n" +
                    $"**Longest Streak:** {longestStreak} day{(longestStreak == 1 ? "" : "s")}\n" +
                    $"**Rank:** #{krillionRank} of {allKrillionAverages.Count} players", 
                    true);
            }

            // Fermi Stats Section
            if (hasFermi)
            {
                var scores = fermiEntries.Select(e => e.Score).ToList();
                var mean = scores.Average();
                var best = scores.Min(); // lower multiplier = better for Fermi
                var worst = scores.Max();
                var (currentStreak, longestStreak) = ComputeStreaks(fermiEntries.Select(e => e.Puzzle).ToList());

                var allFermiAverages = fermiData
                    .SelectMany(p => p.Value.Select(e => (UserId: e.Key, e.Value.Score)))
                    .GroupBy(x => x.UserId)
                    .Select(g => (UserId: g.Key, Avg: g.Average(x => x.Score)))
                    .OrderBy(x => x.Avg)
                    .ToList();
                var fermiRank = allFermiAverages.FindIndex(x => x.UserId == target.Id) + 1;

                embed.AddField("🔢 Fermi Stats", 
                    $"**Days Played:** {scores.Count}\n" +
                    $"**Best Score:** {best:0.##}×\n" +
                    $"**Average:** {mean:0.##}×\n" +
                    $"**Current Streak:** {currentStreak} day{(currentStreak == 1 ? "" : "s")}\n" +
                    $"**Longest Streak:** {longestStreak} day{(longestStreak == 1 ? "" : "s")}\n" +
                    $"**Rank:** #{fermiRank} of {allFermiAverages.Count} players", 
                    true);
            }

            // Combined Stats
            var totalDaysPlayed = (hasKrillion ? krillionEntries.Count : 0) + (hasFermi ? fermiEntries.Count : 0);
            var gamesPlayed = (hasKrillion ? 1 : 0) + (hasFermi ? 1 : 0);
            
            embed.AddField("📊 Overall Stats", 
                $"**Games Played:** {gamesPlayed}\n" +
                $"**Total Submissions:** {totalDaysPlayed}", 
                false);

            // Recent Activity
            var recentActivities = new List<string>();
            
            if (hasKrillion)
            {
                var recentKrillion = krillionEntries.TakeLast(3).ToList();
                foreach (var entry in recentKrillion)
                {
                    recentActivities.Add($"🦐 Krillion #{entry.Puzzle}: {entry.Score}");
                }
            }
            
            if (hasFermi)
            {
                var recentFermi = fermiEntries.TakeLast(3).ToList();
                foreach (var entry in recentFermi)
                {
                    recentActivities.Add($"🔢 Fermi No. {entry.Puzzle}: {entry.Score:0.##}×");
                }
            }

            if (recentActivities.Any())
            {
                embed.AddField("🕐 Recent Activity", 
                    string.Join("\n", recentActivities.TakeLast(5)), 
                    false);
            }

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().AddEmbed(embed));
            Console.WriteLine($"[PROFILE] Successfully displayed profile for {target.Username}");
        }

        /// <summary>Computes (current, longest) streaks of consecutive puzzle numbers from a sorted-ascending list.</summary>
        private static (int Current, int Longest) ComputeStreaks(List<int> puzzleNumbers)
        {
            if (puzzleNumbers.Count == 0) return (0, 0);

            int longest = 1, current = 1;
            for (int i = 1; i < puzzleNumbers.Count; i++)
            {
                if (puzzleNumbers[i] == puzzleNumbers[i - 1] + 1)
                    current++;
                else
                {
                    longest = Math.Max(longest, current);
                    current = 1;
                }
            }
            longest = Math.Max(longest, current);
            return (current, longest);
        }
    }
}
