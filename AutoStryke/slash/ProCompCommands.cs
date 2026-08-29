using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Microsoft.Data.Sqlite;

public class ProCompCommands : ApplicationCommandModule
{
    private static readonly string DataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
    private static readonly string DatabasePath = Path.Combine(DataDirectory, "comps.db");
    private static readonly string ImporterPath = Path.Combine(DataDirectory, "pro_comp_importer.py");

    public enum ProCompMap
    {
        [ChoiceName("Ascent")] Ascent,
        [ChoiceName("Haven")] Haven,
        [ChoiceName("Split")] Split,
        [ChoiceName("Bind")] Bind,
        [ChoiceName("Icebox")] Icebox,
        [ChoiceName("Pearl")] Pearl,
        [ChoiceName("Fracture")] Fracture,
        [ChoiceName("Sunset")] Sunset,
        [ChoiceName("Abyss")] Abyss,
        [ChoiceName("Lotus")] Lotus,
        [ChoiceName("Breeze")] Breeze,
        [ChoiceName("Corrode")] Corrode,
        [ChoiceName("Summit")] Summit,
    }

    private const ulong StrykeID = 889088075395923998;

    [SlashCommand("updateprocomps", "Fetch the latest VCT and VCL pro compositions")]
    public async Task UpdateProComps(InteractionContext ctx)
    {
        if (ctx.Guild is null || (ctx.User.Id != ctx.Guild.OwnerId && ctx.User.Id != StrykeID))
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Only the server owner can update the pro-comp database.")
                    .AsEphemeral(true));
            return;
        }

        if (!File.Exists(ImporterPath))
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("The pro-comp importer is missing from this bot installation.")
                    .AsEphemeral(true));
            return;
        }

        // Public (not ephemeral) so the whole server can watch progress.
        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("PRO_COMP_PYTHON") ?? "python3",
                WorkingDirectory = DataDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add(ImporterPath);
            // Make sure Python flushes output immediately rather than buffering
            // it up, so our line-by-line reader below actually sees it live.
            startInfo.Environment["PYTHONUNBUFFERED"] = "1";

            using var process = Process.Start(startInfo);
            if (process is null)
                throw new InvalidOperationException("Could not start the Python importer.");

            var outputLines = new List<string>();
            var errorTask = process.StandardError.ReadToEndAsync();

            int total = 0;
            int done = 0;
            string currentLabel = "Starting...";
            var lastEdit = DateTime.MinValue;

            while (true)
            {
                var line = await process.StandardOutput.ReadLineAsync();
                if (line is null) break;

                outputLines.Add(line);

                if (line.StartsWith("TOTAL "))
                {
                    int.TryParse(line.AsSpan(6), out total);
                }
                else if (line.StartsWith("PROGRESS "))
                {
                    var parts = line.Substring(9).Split(' ', 3);
                    if (parts.Length == 3 && int.TryParse(parts[0], out var d) && int.TryParse(parts[1], out var t))
                    {
                        done = d;
                        total = t;
                        currentLabel = parts[2].Replace(" :: ", " — ");
                    }
                }
                else
                {
                    continue; // plain log lines don't need to trigger a Discord edit
                }

                if (DateTime.UtcNow - lastEdit < TimeSpan.FromSeconds(5))
                    continue;

                lastEdit = DateTime.UtcNow;

                await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                    .WithContent(BuildProgressMessage(done, total, currentLabel)));
            }

            await process.WaitForExitAsync();
            var error = await errorTask;
            var output = string.Join('\n', outputLines);

            if (process.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(error) ? output : error;
                await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                    .WithContent($"The update failed. {LastLine(detail)}"));
                return;
            }

            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"✅ {LastLine(output)}"));
        }
        catch (Exception exception)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"The update could not start: {exception.Message}"));
        }
    }

    private static string BuildProgressMessage(int done, int total, string currentLabel)
    {
        const int barLength = 20;

        double ratio = total > 0 ? Math.Clamp((double)done / total, 0, 1) : 0;
        int filled = (int)Math.Round(ratio * barLength);

        var bar = string.Concat(Enumerable.Repeat("🟩", filled))
                + string.Concat(Enumerable.Repeat("⬜", barLength - filled));

        return $"Updating pro-comp database...\n{bar}\n**{done}/{total}** ({ratio:P0}) — {currentLabel}";
    }

    public record ScoredComp(string Team, string Comp, List<string> Links, double Similarity);

    /// <summary>
    /// Shared lookup used by both /findprocomp and the "Show all" button handler.
    /// Fetches every comp for the map, merges duplicate (team, comp) rows from
    /// separate matches into one entry with all their links, and scores each
    /// against the query agents.
    /// </summary>
    public static async Task<(List<ScoredComp> Scored, string? Error)> GetScoredComps(string[] queryAgents, string mapName)
    {
        var rows = new List<(string Team, string Comp, string MatchLink)>();

        try
        {
            await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT team, comp, match_link
                FROM compositions
                WHERE lower(map) = lower($map)
                """;
            command.Parameters.AddWithValue("$map", mapName);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                rows.Add((reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? "" : reader.GetString(2)));
        }
        catch (Exception exception)
        {
            return (new List<ScoredComp>(), $"Something went wrong reading the database: {exception.Message}");
        }

        // Merge every match of the same (team, comp) pairing into one row,
        // keeping every distinct match link so they can all be shown.
        var merged = rows
            .GroupBy(r => (r.Team, r.Comp))
            .Select(g => new ScoredComp(
                g.Key.Team,
                g.Key.Comp,
                g.Select(r => r.MatchLink).Where(l => !string.IsNullOrWhiteSpace(l)).Distinct().ToList(),
                CompSimilarity.Compute(queryAgents, g.Key.Comp.Split(" / ", StringSplitOptions.None))
            ));

        const double SimilarityThreshold = 0.4;

        var scored = merged
            .Where(c => c.Similarity >= SimilarityThreshold)
            .OrderByDescending(c => c.Similarity)
            .ThenBy(c => c.Team)
            .ToList();

        return (scored, null);
    }

    public static DiscordEmbedBuilder BuildEmbed(List<ScoredComp> allScored, string[] queryAgents, string mapName, int displayLimit)
    {
        var composition = string.Join(" / ", queryAgents.OrderBy(a => a, StringComparer.OrdinalIgnoreCase));
        var shown = allScored.Take(displayLimit).ToList();

        var results = string.Join("\n", shown.Select(c =>
        {
            const int maxLinksShown = 3;
            string linkPart = "";

            if (c.Links.Count > 0)
            {
                var linkTexts = c.Links.Take(maxLinksShown)
                    .Select((link, i) => $"[game {i + 1}]({link})");
                linkPart = " — " + string.Join(" ", linkTexts);

                if (c.Links.Count > maxLinksShown)
                    linkPart += $" (+{c.Links.Count - maxLinksShown} more)";
            }

            return c.Similarity >= 0.999
                ? $"✅ **{c.Team}** — 100%{linkPart}"
                : $"• **{c.Team}** — {c.Similarity:P0} — {c.Comp}{linkPart}";
        }));

        var footer = allScored.Count > shown.Count
            ? $"\n\nShowing {shown.Count} of {allScored.Count}."
            : "";

        return new DiscordEmbedBuilder()
            .WithTitle("Pro teams using this composition")
            .WithDescription($"**{composition}** on {mapName}\n\n{results}{footer}")
            .WithColor(DiscordColor.Blurple);
    }

    private const int InitialDisplayLimit = 10;

    public static string BuildShowAllCustomId(string[] queryAgents, string mapName) =>
        $"findprocomp_showall|{string.Join(",", queryAgents)}|{mapName}";

    public static string BuildSaveCustomId(string[] queryAgents, string mapName) =>
        $"findprocomp_save|{string.Join(",", queryAgents)}|{mapName}";

    [SlashCommand("findprocomp", "Show pro teams that have played a five-agent composition")]
    public async Task FindProComp(
        InteractionContext ctx,
        [Option("agent1", "First agent"), Autocomplete(typeof(AgentAutocompleteProvider))] string agent1,
        [Option("agent2", "Second agent"), Autocomplete(typeof(AgentAutocompleteProvider))] string agent2,
        [Option("agent3", "Third agent"), Autocomplete(typeof(AgentAutocompleteProvider))] string agent3,
        [Option("agent4", "Fourth agent"), Autocomplete(typeof(AgentAutocompleteProvider))] string agent4,
        [Option("agent5", "Fifth agent"), Autocomplete(typeof(AgentAutocompleteProvider))] string agent5,
        [Option("map", "Map to search")] ProCompMap map)
    {
        var agentInputs = new[] { agent1, agent2, agent3, agent4, agent5 };

        if (agentInputs.Any(string.IsNullOrWhiteSpace) ||
            agentInputs.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 5)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Pick five different agents, one per option.")
                    .AsEphemeral(true));
            return;
        }

        if (!File.Exists(DatabasePath))
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("No pro-comp database has been created yet. The server owner should run `/updateprocomps` first.")
                    .AsEphemeral(true));
            return;
        }

        var mapName = map.ToString();

        // Acknowledge immediately so Discord never times out, even if the
        // query below is slow or throws - we edit this response afterward.
        // Public (not ephemeral) so the whole channel sees the result.
        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

        var (scored, error) = await GetScoredComps(agentInputs, mapName);

        if (error is not null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(error));
            return;
        }

        if (scored.Count == 0)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"No pro comps on {mapName} are close to **{string.Join(" / ", agentInputs)}**."));
            return;
        }

        var embed = BuildEmbed(scored, agentInputs, mapName, InitialDisplayLimit);
        var builder = new DiscordWebhookBuilder().AddEmbed(embed);

        if (scored.Count > InitialDisplayLimit)
        {
            builder.AddComponents(
                new DiscordButtonComponent(ButtonStyle.Secondary, BuildShowAllCustomId(agentInputs, mapName), $"Show all {scored.Count}"),
                new DiscordButtonComponent(ButtonStyle.Success, BuildSaveCustomId(agentInputs, mapName), "💾 Save to team"));
        }
        else
        {
            builder.AddComponents(
                new DiscordButtonComponent(ButtonStyle.Success, BuildSaveCustomId(agentInputs, mapName), "💾 Save to team"));
        }

        await ctx.EditResponseAsync(builder);
    }

    private static string LastLine(string text)
    {
        var line = text.Split(new[] { '\r', '\n' },StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return string.IsNullOrWhiteSpace(line) ? "No details were returned." : line;
    }
}

public class AgentAutocompleteProvider : IAutocompleteProvider
{
    // Spelled to match exactly what the importer writes into comps.db (from VLR's own labels).
    private static readonly string[] Agents =
    {
        "Astra", "Breach", "Brimstone", "Chamber", "Clove", "Cypher", "Deadlock",
        "Fade", "Gekko", "Harbor", "Iso", "Jett", "Kayo", "Killjoy", "Neon",
        "Omen", "Phoenix", "Raze", "Reyna", "Sage", "Skye", "Sova", "Tejo",
        "Veto", "Viper", "Vyse", "Waylay", "Yoru", "Miks",
    };

    public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
    {
        var input = ctx.OptionValue?.ToString() ?? "";

        var matches = Agents
            .Where(agent => agent.Contains(input, StringComparison.OrdinalIgnoreCase))
            .OrderBy(agent => agent)
            .Take(25)
            .Select(agent => new DiscordAutoCompleteChoice(agent, agent));

        return Task.FromResult(matches);
    }
}