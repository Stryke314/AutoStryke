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

    /// <summary>Set once at startup from config.json's "pythonInterpreter" field.
    /// Takes priority over the PRO_COMP_PYTHON environment variable, which is
    /// kept only as a fallback since it proved unreliable across terminal
    /// sessions.</summary>
    public static string PythonInterpreter { get; set; }

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
                FileName = PythonInterpreter
                    ?? Environment.GetEnvironmentVariable("PRO_COMP_PYTHON")
                    ?? "python",
                WorkingDirectory = DataDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            Console.WriteLine($"[updateprocomps] Using interpreter: '{startInfo.FileName}' (config pythonInterpreter = '{PythonInterpreter ?? "(not set)"}', PRO_COMP_PYTHON env var = '{Environment.GetEnvironmentVariable("PRO_COMP_PYTHON") ?? "(not set)"}')");
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

    public record ScoredComp(string Team, string Map, string Comp, List<string> Links, double Similarity);

    /// <summary>
    /// Shared lookup used by /findprocomp, /findmycomp, and the "Show all"
    /// button handler. Fetches every comp (optionally filtered to one map),
    /// merges duplicate (team, map, comp) rows from separate matches into
    /// one entry with all their links, then scores them one of two ways:
    ///
    /// - 5 agents given: ranked by similarity, same as before.
    /// - 1-4 agents given ("core" search): every comp that CONTAINS all the
    ///   given agents matches, regardless of the other agents in it. No
    ///   percentage is meaningful here, so Similarity is set to -1 as a
    ///   sentinel meaning "core match, don't show a percentage".
    ///
    /// mapName null/empty means "search every map".
    /// </summary>
    public static async Task<(List<ScoredComp> Scored, string? Error)> GetScoredComps(string[] queryAgents, string? mapName)
    {
        mapName = string.IsNullOrWhiteSpace(mapName) ? null : mapName;

        var rows = new List<(string Team, string Map, string Comp, string MatchLink)>();

        try
        {
            await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT team, map, comp, match_link
                FROM compositions
                WHERE ($map IS NULL OR lower(map) = lower($map))
                """;
            command.Parameters.AddWithValue("$map", (object?)mapName ?? DBNull.Value);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                rows.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? "" : reader.GetString(3)));
        }
        catch (Exception exception)
        {
            return (new List<ScoredComp>(), $"Something went wrong reading the database: {exception.Message}");
        }

        // Merge every match of the same (team, map, comp) pairing into one
        // row, keeping every distinct match link so they can all be shown.
        var merged = rows
            .GroupBy(r => (r.Team, r.Map, r.Comp))
            .Select(g => new
            {
                g.Key.Team,
                g.Key.Map,
                g.Key.Comp,
                Links = g.Select(r => r.MatchLink).Where(l => !string.IsNullOrWhiteSpace(l)).Distinct().ToList(),
                CompAgents = g.Key.Comp.Split(" / ", StringSplitOptions.None),
            });

        List<ScoredComp> scored;

        if (queryAgents.Length == 5)
        {
            const double SimilarityThreshold = 0.4;

            scored = merged
                .Select(c => new ScoredComp(c.Team, c.Map, c.Comp, c.Links, CompSimilarity.Compute(queryAgents, c.CompAgents)))
                .Where(c => c.Similarity >= SimilarityThreshold)
                .OrderByDescending(c => c.Similarity)
                .ThenBy(c => c.Team)
                .ToList();
        }
        else
        {
            // Core search: every comp that contains all the given agents,
            // regardless of what else is in it.
            scored = merged
                .Where(c => queryAgents.All(a => c.CompAgents.Contains(a, StringComparer.OrdinalIgnoreCase)))
                .Select(c => new ScoredComp(c.Team, c.Map, c.Comp, c.Links, -1))
                .OrderBy(c => c.Team)
                .ThenBy(c => c.Map)
                .ToList();
        }

        return (scored, null);
    }

    public static DiscordEmbedBuilder BuildEmbed(List<ScoredComp> allScored, string[] queryAgents, string mapName, int displayLimit)
    {
        var composition = string.Join(" / ", queryAgents.OrderBy(a => a, StringComparer.OrdinalIgnoreCase));

        const int maxLinksShown = 3;
        const int discordDescriptionLimit = 4096;

        var header = $"**{composition}** on {mapName}\n\n";
        var lines = new List<string>();
        int shownCount = 0;

        // Reserve room for a closing "showing X of Y" footer, whose exact
        // length we won't know until after we've decided how many lines fit -
        // a fixed buffer avoids the chicken-and-egg problem cheaply.
        const int footerBuffer = 80;
        int budget = discordDescriptionLimit - header.Length - footerBuffer;
        int used = 0;

        foreach (var c in allScored)
        {
            if (shownCount >= displayLimit)
                break;

            string linkPart = "";
            if (c.Links.Count > 0)
            {
                var linkTexts = c.Links.Take(maxLinksShown).Select((link, i) => $"[game {i + 1}]({link})");
                linkPart = " — " + string.Join(" ", linkTexts);
                if (c.Links.Count > maxLinksShown)
                    linkPart += $" (+{c.Links.Count - maxLinksShown} more)";
            }

            var line = c.Similarity >= 0.999
                ? $"✅ **{c.Team}** — {c.Map} — 100%{linkPart}"
                : c.Similarity < 0
                    ? $"• **{c.Team}** — {c.Map} — {c.Comp}{linkPart}"
                    : $"• **{c.Team}** — {c.Map} — {c.Similarity:P0} — {c.Comp}{linkPart}";

            if (used + line.Length + 1 > budget)
                break;

            lines.Add(line);
            used += line.Length + 1;
            shownCount++;
        }

        var results = string.Join("\n", lines);

        var footer = allScored.Count > shownCount
            ? $"\n\nShowing {shownCount} of {allScored.Count}."
            : "";

        return new DiscordEmbedBuilder()
            .WithTitle("Pro teams using this composition")
            .WithDescription($"{header}{results}{footer}")
            .WithColor(DiscordColor.Blurple);
    }

    private const int InitialDisplayLimit = 10;

    public static string BuildShowAllCustomId(string[] queryAgents, string mapName) =>
        $"findprocomp_showall|{string.Join(",", queryAgents)}|{mapName}";

    public static string BuildSaveCustomId(string[] queryAgents, string mapName) =>
        $"findprocomp_save|{string.Join(",", queryAgents)}|{mapName}";

    [SlashCommand("findprocomp", "Find pro teams using a comp (1-5 agents; map is optional)")]
    public async Task FindProComp(
        InteractionContext ctx,
        [Option("agent1", "First agent"), Autocomplete(typeof(AgentAutocompleteProvider))] string agent1,
        [Option("agent2", "Second agent (optional)"), Autocomplete(typeof(AgentAutocompleteProvider))] string? agent2 = null,
        [Option("agent3", "Third agent (optional)"), Autocomplete(typeof(AgentAutocompleteProvider))] string? agent3 = null,
        [Option("agent4", "Fourth agent (optional)"), Autocomplete(typeof(AgentAutocompleteProvider))] string? agent4 = null,
        [Option("agent5", "Fifth agent (optional)"), Autocomplete(typeof(AgentAutocompleteProvider))] string? agent5 = null,
        [Option("map", "Map to search (optional - leave blank to search every map)")] ProCompMap? map = null)
    {
        var agentInputs = new[] { agent1, agent2, agent3, agent4, agent5 }
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .ToArray();

        if (agentInputs.Distinct(StringComparer.OrdinalIgnoreCase).Count() != agentInputs.Length)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Don't repeat the same agent twice.")
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

        var mapName = map?.ToString();
        var mapLabel = mapName ?? "any map";

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
                .WithContent($"No pro comps on {mapLabel} match **{string.Join(" / ", agentInputs)}**."));
            return;
        }

        var embed = BuildEmbed(scored, agentInputs, mapLabel, InitialDisplayLimit);
        var builder = new DiscordWebhookBuilder().AddEmbed(embed);

        if (scored.Count > InitialDisplayLimit)
        {
            builder.AddComponents(
                new DiscordButtonComponent(ButtonStyle.Secondary, BuildShowAllCustomId(agentInputs, mapName ?? ""), $"Show all {scored.Count}"),
                new DiscordButtonComponent(ButtonStyle.Success, BuildSaveCustomId(agentInputs, mapName ?? ""), "💾 Save to team"));
        }
        else
        {
            builder.AddComponents(
                new DiscordButtonComponent(ButtonStyle.Success, BuildSaveCustomId(agentInputs, mapName ?? ""), "💾 Save to team"));
        }

        await ctx.EditResponseAsync(builder);
    }

    [SlashCommand("findmycomp", "Find pro teams using one of your own saved team comps")]
    public async Task FindMyComp(
        InteractionContext ctx,
        [Option("team", "Your saved team name"), Autocomplete(typeof(TeamAutocompleteProvider))] string team,
        [Option("map", "Map (optional - uses whichever map you saved a comp for if left blank)")] ProCompMap? map = null)
    {
        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

        var db = CompsCommands.LoadDatabase();
        var requestedMap = map?.ToString();

        List<string>? savedAgents = null;
        string? savedMap = null;

        if (requestedMap != null)
        {
            if (db.TryGetValue(requestedMap, out var teamsOnMap) && teamsOnMap.TryGetValue(team, out var agents))
            {
                savedAgents = agents;
                savedMap = requestedMap;
            }
        }
        else
        {
            var match = db.FirstOrDefault(kv => kv.Value.ContainsKey(team));
            if (!EqualityComparer<KeyValuePair<string, Dictionary<string, List<string>>>>.Default.Equals(match, default))
            {
                savedMap = match.Key;
                savedAgents = match.Value[team];
            }
        }

        if (savedAgents is null || savedMap is null)
        {
            var mapPart = requestedMap != null ? $" on {requestedMap}" : "";
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"No saved comp found for **{team}**{mapPart}. Use `/addcomp` first."));
            return;
        }

        if (!File.Exists(DatabasePath))
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent("No pro-comp database has been created yet. The server owner should run `/updateprocomps` first."));
            return;
        }

        var queryAgents = savedAgents.ToArray();
        var (scored, error) = await GetScoredComps(queryAgents, savedMap);

        if (error is not null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(error));
            return;
        }

        if (scored.Count == 0)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"No pro comps on {savedMap} are close to **{team}**'s saved comp ({string.Join(" / ", queryAgents)})."));
            return;
        }

        var embed = BuildEmbed(scored, queryAgents, savedMap, InitialDisplayLimit);
        var builder = new DiscordWebhookBuilder().AddEmbed(embed);

        if (scored.Count > InitialDisplayLimit)
        {
            builder.AddComponents(new DiscordButtonComponent(
                ButtonStyle.Secondary, BuildShowAllCustomId(queryAgents, savedMap), $"Show all {scored.Count}"));
        }

        await ctx.EditResponseAsync(builder);
    }

    private static string LastLine(string text)
    {
        var line = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
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

public class TeamAutocompleteProvider : IAutocompleteProvider
{
    public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
    {
        var input = ctx.OptionValue?.ToString() ?? "";

        var matches = CompsCommands.GetAllTeamNames()
            .Where(team => team.Contains(input, StringComparison.OrdinalIgnoreCase))
            .OrderBy(team => team)
            .Take(25)
            .Select(team => new DiscordAutoCompleteChoice(team, team));

        return Task.FromResult(matches);
    }
}