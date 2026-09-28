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
                        .WithContent($"**{target.Username}** hasn't submitted any game results yet. Paste your Krillion or Fermi share text to get started!")
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

        [SlashCommand("compare", "Compare your stats with another user")]
        public async Task Compare(
            InteractionContext ctx,
            [Option("user", "User to compare with")] DiscordUser user)
        {
            Console.WriteLine($"[COMPARE] /compare command executed by {ctx.User.Username} comparing with {user.Username}");
            Console.WriteLine($"[COMPARE] User1: {ctx.User.Username} (ID: {ctx.User.Id}), User2: {user.Username} (ID: {user.Id})");
            
            var krillionData = LoadKrillionData();
            var fermiData = LoadFermiData();

            var user1 = ctx.User;
            var user2 = user;

            var user1Krillion = krillionData
                .Where(kv => kv.Value.ContainsKey(user1.Id))
                .Select(kv => (Puzzle: kv.Key, Score: kv.Value[user1.Id].Score))
                .ToList();

            var user2Krillion = krillionData
                .Where(kv => kv.Value.ContainsKey(user2.Id))
                .Select(kv => (Puzzle: kv.Key, Score: kv.Value[user2.Id].Score))
                .ToList();

            var user1Fermi = fermiData
                .Where(kv => kv.Value.ContainsKey(user1.Id))
                .Select(kv => (Puzzle: kv.Key, Score: kv.Value[user1.Id].Score))
                .ToList();

            var user2Fermi = fermiData
                .Where(kv => kv.Value.ContainsKey(user2.Id))
                .Select(kv => (Puzzle: kv.Key, Score: kv.Value[user2.Id].Score))
                .ToList();

            if (user1Krillion.Count == 0 && user1Fermi.Count == 0)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"You haven't submitted any results yet!")
                        .AsEphemeral(true));
                return;
            }

            if (user2Krillion.Count == 0 && user2Fermi.Count == 0)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"**{user2.Username}** hasn't submitted any results yet!")
                        .AsEphemeral(true));
                return;
            }

            var embed = new DiscordEmbedBuilder()
                .WithTitle($"⚔️ {user1.Username} vs {user2.Username}")
                .WithColor(DiscordColor.Orange)
                .WithTimestamp(DateTime.UtcNow);

            // Krillion Comparison
            if (user1Krillion.Count > 0 || user2Krillion.Count > 0)
            {
                var u1Avg = user1Krillion.Count > 0 ? user1Krillion.Average(x => x.Score) : 0;
                var u2Avg = user2Krillion.Count > 0 ? user2Krillion.Average(x => x.Score) : 0;
                var u1Best = user1Krillion.Count > 0 ? user1Krillion.Max(x => x.Score) : 0;
                var u2Best = user2Krillion.Count > 0 ? user2Krillion.Max(x => x.Score) : 0;
                var u1Days = user1Krillion.Count;
                var u2Days = user2Krillion.Count;

                var krillionWinner = u1Avg > u2Avg ? user1.Username : (u2Avg > u1Avg ? user2.Username : "Tie");
                var krillionWinnerEmoji = u1Avg > u2Avg ? "🏆" : (u2Avg > u1Avg ? "🏆" : "🤝");

                embed.AddField("🦐 Krillion Comparison",
                    $"**{user1.Username}:** Avg {u1Avg:0.#} | Best {u1Best} | {u1Days} days\n" +
                    $"**{user2.Username}:** Avg {u2Avg:0.#} | Best {u2Best} | {u2Days} days\n" +
                    $"**Winner:** {krillionWinnerEmoji} {krillionWinner}", true);
            }

            // Fermi Comparison
            if (user1Fermi.Count > 0 || user2Fermi.Count > 0)
            {
                var u1Avg = user1Fermi.Count > 0 ? user1Fermi.Average(x => x.Score) : 0;
                var u2Avg = user2Fermi.Count > 0 ? user2Fermi.Average(x => x.Score) : 0;
                var u1Best = user1Fermi.Count > 0 ? user1Fermi.Min(x => x.Score) : 0; // lower is better
                var u2Best = user2Fermi.Count > 0 ? user2Fermi.Min(x => x.Score) : 0;
                var u1Days = user1Fermi.Count;
                var u2Days = user2Fermi.Count;

                var fermiWinner = u1Avg < u2Avg ? user1.Username : (u2Avg < u1Avg ? user2.Username : "Tie");
                var fermiWinnerEmoji = u1Avg < u2Avg ? "🏆" : (u2Avg < u1Avg ? "🏆" : "🤝");

                embed.AddField("🔢 Fermi Comparison",
                    $"**{user1.Username}:** Avg {u1Avg:0.##}× | Best {u1Best:0.##}× | {u1Days} days\n" +
                    $"**{user2.Username}:** Avg {u2Avg:0.##}× | Best {u2Best:0.##}× | {u2Days} days\n" +
                    $"**Winner:** {fermiWinnerEmoji} {fermiWinner}", true);
            }

            // Overall Comparison
            var u1Total = user1Krillion.Count + user1Fermi.Count;
            var u2Total = user2Krillion.Count + user2Fermi.Count;
            var overallWinner = u1Total > u2Total ? user1.Username : (u2Total > u1Total ? user2.Username : "Tie");

            embed.AddField("📊 Overall",
                $"**{user1.Username}:** {u1Total} total submissions\n" +
                $"**{user2.Username}:** {u2Total} total submissions\n" +
                $"**Most Active:** {overallWinner}", false);

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().AddEmbed(embed));
            Console.WriteLine($"[COMPARE] Successfully displayed comparison between {user1.Username} and {user2.Username}");
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
