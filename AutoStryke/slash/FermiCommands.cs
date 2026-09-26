using DSharpPlus;
using DSharpPlus.SlashCommands;
using DSharpPlus.Entities;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public record FermiEntry(string Username, double Score, string RawShare, DateTime SubmittedAt);

public static class FermiStore
{
    private const string JsonFile = "fermi_results.json";
    private static readonly Regex PuzzleNumberPattern = new(@"No\.\s*#?(\d+)", RegexOptions.IgnoreCase);
    private static readonly Regex ScorePattern = new(@"([\d.]+)\s*[×xX]\s*score", RegexOptions.IgnoreCase);

    /// <summary>Parses a raw message into (puzzleNumber, score), or null if it isn't a valid Fermi share.</summary>
    public static (int PuzzleNumber, double Score)? TryParse(string text)
    {
        var puzzleMatch = PuzzleNumberPattern.Match(text);
        var scoreMatch = ScorePattern.Match(text);

        if (!puzzleMatch.Success || !scoreMatch.Success || !double.TryParse(scoreMatch.Groups[1].Value, out var score))
            return null;

        return (int.Parse(puzzleMatch.Groups[1].Value), score);
    }

    // puzzleNumber -> (userId -> entry)
    public static Dictionary<int, Dictionary<ulong, FermiEntry>> Load()
    {
        if (!File.Exists(JsonFile))
            return new Dictionary<int, Dictionary<ulong, FermiEntry>>();

        var json = File.ReadAllText(JsonFile);
        return JsonSerializer.Deserialize<Dictionary<int, Dictionary<ulong, FermiEntry>>>(json)
            ?? new Dictionary<int, Dictionary<ulong, FermiEntry>>();
    }

    public static void Save(Dictionary<int, Dictionary<ulong, FermiEntry>> data)
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
    public static void RecordResult(int puzzleNumber, ulong userId, string username, double score, string rawShare)
    {
        var data = Load();

        if (!data.ContainsKey(puzzleNumber))
            data[puzzleNumber] = new Dictionary<ulong, FermiEntry>();

        data[puzzleNumber][userId] = new FermiEntry(username, score, rawShare, DateTime.UtcNow);

        Save(data);
    }
}

public class FermiCommands : ApplicationCommandModule
{
    public enum FermiScope
    {
        [ChoiceName("Today (latest puzzle)")] Today,
        [ChoiceName("All Time")] AllTime,
    }

    [SlashCommand("fermi", "Submit your daily Fermi result (paste the full share text)")]
    public async Task SubmitFermi(
        InteractionContext ctx,
        [Option("result", "Paste your Fermi share text here")] string resultText)
    {
        Console.WriteLine($"[FERMI] /fermi command executed by {ctx.User.Username} (ID: {ctx.User.Id})");
        var parsed = FermiStore.TryParse(resultText);

        if (parsed is null)
        {
            Console.WriteLine($"[FERMI] Invalid Fermi share format from {ctx.User.Username}");
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("That doesn't look like a Fermi share - paste the whole result, including the \"No. X\" line and the final \"...× score\" line.")
                    .AsEphemeral(true));
            return;
        }

        var (puzzleNumber, score) = parsed.Value;
        var username = ctx.User.Username;
        Console.WriteLine($"[FERMI] Parsed: Fermi No. {puzzleNumber}, Score: {score:0.##}×");

        if (FermiStore.HasSubmitted(puzzleNumber, ctx.User.Id))
        {
            Console.WriteLine($"[FERMI] Duplicate submission attempt for Fermi No. {puzzleNumber} by {username}");
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent($"You've already submitted your result for Fermi No. {puzzleNumber} - only one submission per puzzle.")
                    .AsEphemeral(true));
            return;
        }

        FermiStore.RecordResult(puzzleNumber, ctx.User.Id, username, score, resultText);
        Console.WriteLine($"[FERMI] Successfully recorded Fermi No. {puzzleNumber} result for {username}: {score:0.##}×");

        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder()
                .WithContent($"🔢 Recorded **{username}**'s Fermi No. {puzzleNumber} result: **{score:0.##}×**"));
    }

    [SlashCommand("fermiboard", "Show the Fermi leaderboard")]
    public async Task FermiLeaderboard(
        InteractionContext ctx,
        [Option("scope", "Today's puzzle, or all-time averages")] FermiScope scope = FermiScope.Today)
    {
        Console.WriteLine($"[FERMI] /fermiboard command executed by {ctx.User.Username}, scope: {scope}");
        var data = FermiStore.Load();

        if (data.Count == 0)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("No Fermi results submitted yet - use `/fermi` to add one.")
                    .AsEphemeral(true));
            return;
        }

        DiscordEmbedBuilder embed;

        if (scope == FermiScope.Today)
        {
            var latestPuzzle = data.Keys.Max();
            var entries = data[latestPuzzle]
                .Values
                .OrderBy(e => e.Score) // lower multiplier = better
                .ToList();

            var rows = entries.Select((e, i) => new[] { $"{i + 1}.", e.Username, $"{e.Score:0.##}×" });
            var table = BuildTable(new[] { "#", "Player", "Score" }, rows.ToList());

            embed = new DiscordEmbedBuilder()
                .WithTitle($"🔢 Fermi No. {latestPuzzle} Leaderboard")
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
                    BestScore = g.Min(x => x.Score), // lower multiplier = better for Fermi
                    AverageScore = g.Average(x => x.Score),
                    DaysPlayed = g.Count(),
                })
                .OrderBy(x => x.AverageScore)
                .ThenBy(x => x.BestScore)
                .ToList();

            var rows = stats.Select((s, i) => new[]
            {
                $"{i + 1}.", s.Username, $"{s.BestScore:0.##}×", $"{s.AverageScore:0.##}×", s.DaysPlayed.ToString()
            });
            var table = BuildTable(new[] { "#", "Player", "Best", "Avg", "Days" }, rows.ToList());

            embed = new DiscordEmbedBuilder()
                .WithTitle("🔢 Fermi All-Time Leaderboard")
                .WithDescription(table)
                .WithColor(DiscordColor.Blurple);
        }

        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AddEmbed(embed));
    }

    [SlashCommand("fermistats", "View detailed Fermi statistics for yourself or someone else")]
    public async Task FermiStats(
        InteractionContext ctx,
        [Option("user", "Whose stats to view (defaults to you)")] DiscordUser? user = null)
    {
        Console.WriteLine($"[FERMI] /fermistats command executed by {ctx.User.Username}");
        var target = user ?? ctx.User;
        Console.WriteLine($"[FERMI] Viewing stats for: {target.Username}");
        var data = FermiStore.Load();

        var entries = data
            .Where(kv => kv.Value.ContainsKey(target.Id))
            .Select(kv => (Puzzle: kv.Key, Score: kv.Value[target.Id].Score))
            .OrderBy(x => x.Puzzle)
            .ToList();

        if (entries.Count == 0)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent($"**{target.Username}** hasn't submitted any Fermi results yet.")
                    .AsEphemeral(true));
            return;
        }

        var scores = entries.Select(e => e.Score).ToList();
        var mean = scores.Average();
        var median = Median(scores);
        var mode = Mode(scores);
        var stdDev = StdDev(scores, mean);
        var best = scores.Min(); // lower multiplier = better for Fermi
        var worst = scores.Max();

        var (currentStreak, longestStreak) = ComputeStreaks(entries.Select(e => e.Puzzle).ToList());

        var recentCount = Math.Min(5, scores.Count);
        var recentAvg = scores.TakeLast(recentCount).Average();
        var trendDiff = recentAvg - mean;
        var trend = trendDiff < -0.5 ? "📈 Improving" : trendDiff > 0.5 ? "📉 Declining" : "➡️ Steady";

        var allAverages = data
            .SelectMany(p => p.Value.Select(e => (UserId: e.Key, e.Value.Score)))
            .GroupBy(x => x.UserId)
            .Select(g => (UserId: g.Key, Avg: g.Average(x => x.Score)))
            .OrderBy(x => x.Avg)
            .ToList();
        var rank = allAverages.FindIndex(x => x.UserId == target.Id) + 1;

        var embed = new DiscordEmbedBuilder()
            .WithTitle($"🔢 {target.Username}'s Fermi Stats")
            .WithColor(DiscordColor.Orange)
            .AddField("Days Played", scores.Count.ToString(), true)
            .AddField("Best Score", $"{best:0.##}×", true)
            .AddField("Worst Score", $"{worst:0.##}×", true)
            .AddField("Mean", $"{mean:0.##}×", true)
            .AddField("Median", $"{median:0.##}×", true)
            .AddField("Mode", mode, true)
            .AddField("Std Dev", $"{stdDev:0.##}×", true)
            .AddField("Current Streak", $"{currentStreak} day{(currentStreak == 1 ? "" : "s")}", true)
            .AddField("Longest Streak", $"{longestStreak} day{(longestStreak == 1 ? "" : "s")}", true)
            .AddField("Recent Form", $"{trend} — last {recentCount}: {recentAvg:0.##}× vs overall {mean:0.##}×", false)
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
        return string.Join(", ", groups.Where(g => g.Count() == topCount).Select(g => $"{g.Key:0.##}×"));
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