using DSharpPlus;
using DSharpPlus.SlashCommands;
using DSharpPlus.Entities;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public record KrillionEntry(string Username, int Score, string RawShare, DateTime SubmittedAt);

public static class KrillionStore
{
    private const string JsonFile = "krillion_results.json";
    private static readonly Regex PuzzleNumberPattern = new(@"Krillion\s*#(\d+)", RegexOptions.IgnoreCase);

    /// <summary>Parses a raw message into (puzzleNumber, score), or null if it isn't a valid Krillion share.</summary>
    public static (int PuzzleNumber, int Score)? TryParse(string text)
    {
        var puzzleMatch = PuzzleNumberPattern.Match(text);

        var lines = text
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        var scoreLine = lines.LastOrDefault(l => int.TryParse(l, out _));

        if (!puzzleMatch.Success || scoreLine is null || !int.TryParse(scoreLine, out var score))
            return null;

        return (int.Parse(puzzleMatch.Groups[1].Value), score);
    }

    // puzzleNumber -> (userId -> entry)
    public static Dictionary<int, Dictionary<ulong, KrillionEntry>> Load()
    {
        if (!File.Exists(JsonFile))
            return new Dictionary<int, Dictionary<ulong, KrillionEntry>>();

        var json = File.ReadAllText(JsonFile);
        return JsonSerializer.Deserialize<Dictionary<int, Dictionary<ulong, KrillionEntry>>>(json)
            ?? new Dictionary<int, Dictionary<ulong, KrillionEntry>>();
    }

    public static void Save(Dictionary<int, Dictionary<ulong, KrillionEntry>> data)
    {
        File.WriteAllText(JsonFile, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Returns true if this user has already submitted for this puzzle.</summary>
    public static bool HasSubmitted(int puzzleNumber, ulong userId)
    {
        var data = Load();
        return data.TryGetValue(puzzleNumber, out var entries) && entries.ContainsKey(userId);
    }

    /// <summary>Records a user's result for a given puzzle number. Does not check for duplicates - call HasSubmitted first.</summary>
    public static void RecordResult(int puzzleNumber, ulong userId, string username, int score, string rawShare)
    {
        var data = Load();

        if (!data.ContainsKey(puzzleNumber))
            data[puzzleNumber] = new Dictionary<ulong, KrillionEntry>();

        data[puzzleNumber][userId] = new KrillionEntry(username, score, rawShare, DateTime.UtcNow);

        Save(data);
    }
}

public class KrillionCommands : ApplicationCommandModule
{
    public enum KrillionScope
    {
        [ChoiceName("Today (latest puzzle)")] Today,
        [ChoiceName("All Time")] AllTime,
    }

    [SlashCommand("krillion", "Submit your daily Krillion result (paste the full share text)")]
    public async Task SubmitKrillion(
        InteractionContext ctx,
        [Option("result", "Paste your Krillion share text here")] string resultText)
    {
        Console.WriteLine($"[KRILLION] /krillion command executed by {ctx.User.Username} (ID: {ctx.User.Id})");
        var parsed = KrillionStore.TryParse(resultText);

        if (parsed is null)
        {
            Console.WriteLine($"[KRILLION] Invalid Krillion share format from {ctx.User.Username}");
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("That doesn't look like a Krillion share - paste the whole result, starting with \"Krillion #...\" and ending with your score.")
                    .AsEphemeral(true));
            return;
        }

        var (puzzleNumber, score) = parsed.Value;
        var username = ctx.User.Username;
        Console.WriteLine($"[KRILLION] Parsed: Krillion #{puzzleNumber}, Score: {score}");

        if (KrillionStore.HasSubmitted(puzzleNumber, ctx.User.Id))
        {
            Console.WriteLine($"[KRILLION] Duplicate submission attempt for Krillion #{puzzleNumber} by {username}");
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent($"You've already submitted your result for Krillion #{puzzleNumber} - only one submission per puzzle.")
                    .AsEphemeral(true));
            return;
        }

        KrillionStore.RecordResult(puzzleNumber, ctx.User.Id, username, score, resultText);
        Console.WriteLine($"[KRILLION] Successfully recorded Krillion #{puzzleNumber} result for {username}: {score}");

        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder()
                .WithContent($"🦐 Recorded **{username}**'s Krillion #{puzzleNumber} result: **{score}**"));
    }

    [SlashCommand("krillionboard", "Show the Krillion leaderboard")]
    public async Task KrillionLeaderboard(
        InteractionContext ctx,
        [Option("scope", "Today's puzzle, or all-time totals")] KrillionScope scope = KrillionScope.Today)
    {
        Console.WriteLine($"[KRILLION] /krillionboard command executed by {ctx.User.Username}, scope: {scope}");
        var data = KrillionStore.Load();

        if (data.Count == 0)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("No Krillion results submitted yet - use `/krillion` to add one.")
                    .AsEphemeral(true));
            return;
        }

        DiscordEmbedBuilder embed;

        if (scope == KrillionScope.Today)
        {
            var latestPuzzle = data.Keys.Max();
            var entries = data[latestPuzzle]
                .Values
                .OrderByDescending(e => e.Score)
                .ToList();

            var rows = entries.Select((e, i) => new[] { $"{i + 1}.", e.Username, e.Score.ToString() });
            var table = BuildTable(new[] { "#", "Player", "Score" }, rows.ToList());

            embed = new DiscordEmbedBuilder()
                .WithTitle($"🦐 Krillion #{latestPuzzle} Leaderboard")
                .WithDescription(table)
                .WithColor(DiscordColor.Cyan);
        }
        else
        {
            var stats = data
                .SelectMany(puzzle => puzzle.Value.Select(entry => (entry.Key, entry.Value.Username, entry.Value.Score)))
                .GroupBy(x => x.Key)
                .Select(g => new
                {
                    Username = g.First().Username,
                    BestScore = g.Max(x => x.Score),
                    AverageScore = g.Average(x => x.Score),
                    DaysPlayed = g.Count(),
                })
                .OrderByDescending(x => x.AverageScore)
                .ThenByDescending(x => x.BestScore)
                .ToList();

            var rows = stats.Select((s, i) => new[]
            {
                $"{i + 1}.", s.Username, s.BestScore.ToString(), $"{s.AverageScore:0.#}", s.DaysPlayed.ToString()
            });
            var table = BuildTable(new[] { "#", "Player", "Best", "Avg", "Days" }, rows.ToList());

            embed = new DiscordEmbedBuilder()
                .WithTitle("🦐 Krillion All-Time Leaderboard")
                .WithDescription(table)
                .WithColor(DiscordColor.Blurple);
        }

        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AddEmbed(embed));
    }

    [SlashCommand("krillionstats", "View detailed Krillion statistics for yourself or someone else")]
    public async Task KrillionStats(
        InteractionContext ctx,
        [Option("user", "Whose stats to view (defaults to you)")] DiscordUser? user = null)
    {
        Console.WriteLine($"[KRILLION] /krillionstats command executed by {ctx.User.Username}");
        var target = user ?? ctx.User;
        Console.WriteLine($"[KRILLION] Viewing stats for: {target.Username}");
        var data = KrillionStore.Load();

        var entries = data
            .Where(kv => kv.Value.ContainsKey(target.Id))
            .Select(kv => (Puzzle: kv.Key, Score: kv.Value[target.Id].Score))
            .OrderBy(x => x.Puzzle)
            .ToList();

        if (entries.Count == 0)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent($"**{target.Username}** hasn't submitted any Krillion results yet.")
                    .AsEphemeral(true));
            return;
        }

        var scores = entries.Select(e => (double)e.Score).ToList();
        var mean = scores.Average();
        var median = Median(scores);
        var mode = Mode(scores);
        var stdDev = StdDev(scores, mean);
        var best = scores.Max();
        var worst = scores.Min();

        var (currentStreak, longestStreak) = ComputeStreaks(entries.Select(e => e.Puzzle).ToList());

        var recentCount = Math.Min(5, scores.Count);
        var recentAvg = scores.TakeLast(recentCount).Average();
        var trendDiff = recentAvg - mean;
        var trend = trendDiff > 5 ? "📈 Improving" : trendDiff < -5 ? "📉 Declining" : "➡️ Steady";

        var allAverages = data
            .SelectMany(p => p.Value.Select(e => (UserId: e.Key, e.Value.Score)))
            .GroupBy(x => x.UserId)
            .Select(g => (UserId: g.Key, Avg: g.Average(x => x.Score)))
            .OrderByDescending(x => x.Avg)
            .ToList();
        var rank = allAverages.FindIndex(x => x.UserId == target.Id) + 1;

        var embed = new DiscordEmbedBuilder()
            .WithTitle($"🦐 {target.Username}'s Krillion Stats")
            .WithColor(DiscordColor.Cyan)
            .AddField("Days Played", scores.Count.ToString(), true)
            .AddField("Best Score", best.ToString("0.#"), true)
            .AddField("Worst Score", worst.ToString("0.#"), true)
            .AddField("Mean", mean.ToString("0.##"), true)
            .AddField("Median", median.ToString("0.##"), true)
            .AddField("Mode", mode, true)
            .AddField("Std Dev", stdDev.ToString("0.##"), true)
            .AddField("Current Streak", $"{currentStreak} day{(currentStreak == 1 ? "" : "s")}", true)
            .AddField("Longest Streak", $"{longestStreak} day{(longestStreak == 1 ? "" : "s")}", true)
            .AddField("Recent Form", $"{trend} — last {recentCount}: {recentAvg:0.##} vs overall {mean:0.##}", false)
            .AddField("Rank", $"#{rank} of {allAverages.Count} players (by average)", false);

        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AddEmbed(embed));
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        int n = sorted.Count;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
    }

    private static string Mode(List<double> values)
    {
        var groups = values.GroupBy(v => v).OrderByDescending(g => g.Count()).ToList();
        var topCount = groups.First().Count();
        if (topCount <= 1) return "No repeats";
        return string.Join(", ", groups.Where(g => g.Count() == topCount).Select(g => g.Key.ToString("0.##")));
    }

    private static double StdDev(List<double> values, double mean)
    {
        if (values.Count < 2) return 0;
        var sumSquares = values.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sumSquares / (values.Count - 1));
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

    /// <summary>Builds a monospace, column-aligned table wrapped in a code block.</summary>
    private static string BuildTable(string[] headers, List<string[]> rows)
    {
        var columnCount = headers.Length;
        var widths = new int[columnCount];

        for (int c = 0; c < columnCount; c++)
        {
            widths[c] = headers[c].Length;
            foreach (var row in rows)
                widths[c] = Math.Max(widths[c], row[c].Length);
        }

        string PadRow(string[] cells) =>
            string.Join("  ", cells.Select((cell, c) => cell.PadRight(widths[c])));

        var lines = new List<string> { PadRow(headers), PadRow(headers.Select(h => new string('-', h.Length)).ToArray()) };
        lines.AddRange(rows.Select(PadRow));

        return "```\n" + string.Join("\n", lines) + "\n```";
    }
}