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


    [SlashCommand("krillionboard", "Show the Krillion leaderboard")]
    public async Task KrillionLeaderboard(
        InteractionContext ctx,
        [Option("scope", "Today's puzzle, or all-time totals")] KrillionScope scope = KrillionScope.Today)
    {
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
                .OrderByDescending(x => x.BestScore)
                .ThenByDescending(x => x.AverageScore)
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
