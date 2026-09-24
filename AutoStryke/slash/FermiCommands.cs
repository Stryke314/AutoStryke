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




    [SlashCommand("fermiboard", "Show the Fermi leaderboard")]
    public async Task FermiLeaderboard(
        InteractionContext ctx,
        [Option("scope", "Today's puzzle, or all-time averages")] FermiScope scope = FermiScope.Today)
    {
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
                .OrderBy(x => x.BestScore)
                .ThenBy(x => x.AverageScore)
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