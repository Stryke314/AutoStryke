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
        var parsed = KrillionStore.TryParse(resultText);

        if (parsed is null)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("That doesn't look like a Krillion share - paste the whole result, starting with \"Krillion #...\" and ending with your score.")
                    .AsEphemeral(true));
            return;
        }

        var (puzzleNumber, score) = parsed.Value;
        var username = ctx.User.Username;

        if (KrillionStore.HasSubmitted(puzzleNumber, ctx.User.Id))
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent($"You've already submitted your result for Krillion #{puzzleNumber} - only one submission per puzzle.")
                    .AsEphemeral(true));
            return;
        }

        KrillionStore.RecordResult(puzzleNumber, ctx.User.Id, username, score, resultText);

        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder()
                .WithContent($"🦐 Recorded **{username}**'s Krillion #{puzzleNumber} result: **{score}**"));
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

            var lines = entries.Select((e, i) => $"{Medal(i)} **{e.Username}** — {e.Score}");

            embed = new DiscordEmbedBuilder()
                .WithTitle($"🦐 Krillion #{latestPuzzle} Leaderboard")
                .WithDescription(string.Join("\n", lines))
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

            var lines = stats.Select((s, i) =>
                $"{Medal(i)} **{s.Username}** — best {s.BestScore}, avg {s.AverageScore:0.##} ({s.DaysPlayed} day{(s.DaysPlayed == 1 ? "" : "s")})");

            embed = new DiscordEmbedBuilder()
                .WithTitle("🦐 Krillion All-Time Leaderboard")
                .WithDescription(string.Join("\n", lines))
                .WithColor(DiscordColor.Blurple);
        }

        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AddEmbed(embed));
    }

    private static string Medal(int index) => index switch
    {
        0 => "🥇",
        1 => "🥈",
        2 => "🥉",
        _ => $"`#{index + 1}`",
    };
}