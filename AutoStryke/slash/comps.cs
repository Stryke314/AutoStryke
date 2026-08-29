using DSharpPlus;
using DSharpPlus.SlashCommands;
using DSharpPlus.Entities;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public class CompsCommands : ApplicationCommandModule
{
    private const string JsonFile = "comps.json";

    public static Dictionary<string, Dictionary<string, List<string>>> LoadDatabase()
    {
        return File.Exists(JsonFile)
            ? JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<string>>>>(File.ReadAllText(JsonFile))
              ?? new Dictionary<string, Dictionary<string, List<string>>>()
            : new Dictionary<string, Dictionary<string, List<string>>>();
    }

    public static void SaveDatabase(Dictionary<string, Dictionary<string, List<string>>> db)
    {
        File.WriteAllText(JsonFile, JsonSerializer.Serialize(db, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Shared by /addcomp and the "Save to team" button under /findprocomp.</summary>
    public static void SaveCompForTeam(string team, string map, List<string> agents)
    {
        var db = LoadDatabase();

        if (!db.ContainsKey(map))
            db[map] = new Dictionary<string, List<string>>();

        db[map][team] = agents;

        SaveDatabase(db);
    }

    /// <summary>Every distinct team name saved anywhere in the database, for the team-select dropdown.</summary>
    public static List<string> GetAllTeamNames()
    {
        var db = LoadDatabase();
        return db.Values
            .SelectMany(teams => teams.Keys)
            .Distinct()
            .OrderBy(t => t)
            .ToList();
    }

    // ---------------- /addcomp ----------------
    [SlashCommand("addcomp", "Add a team's composition for a map")]
    public async Task AddCompCommand(InteractionContext ctx,
        [Option("team", "Team name")] string team,
        [Option("map", "Map name")] ProCompCommands.ProCompMap map,
        [Option("agents", "Comma-separated list of 5 agents")] string agentsInput)
    {
        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

        var agents = agentsInput.Split(',', System.StringSplitOptions.RemoveEmptyEntries)
                                .Select(a => a.Trim())
                                .ToList();

        if (agents.Count != 5)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent("You must provide exactly 5 agents."));
            return;
        }

        SaveCompForTeam(team, map.ToString(), agents);

        await ctx.EditResponseAsync(new DiscordWebhookBuilder()
            .WithContent($"Saved comp for **{team}** on **{map}**."));
    }

    // ---------------- /comps ----------------
    [SlashCommand("comps", "View and edit a team's saved comps across every map")]
    public async Task CompsCommand(InteractionContext ctx,
        [Option("team", "Team name")] string team)
    {
        var db = LoadDatabase();

        var teamComps = db
            .Where(mapEntry => mapEntry.Value.ContainsKey(team))
            .Select(mapEntry => (Map: mapEntry.Key, Agents: mapEntry.Value[team]))
            .OrderBy(entry => entry.Map)
            .ToList();

        if (teamComps.Count == 0)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent($"No comps saved for **{team}** yet. Use `/addcomp` to add one.")
                    .AsEphemeral(true));
            return;
        }

        var description = string.Join("\n", teamComps.Select(c => $"**{c.Map}**: {string.Join(" / ", c.Agents)}"));

        var embed = new DiscordEmbedBuilder()
            .WithTitle($"{team}'s comps")
            .WithDescription(description)
            .WithColor(DiscordColor.Azure);

        var mapOptions = teamComps
            .Take(25)
            .Select(c => new DiscordSelectComponentOption(c.Map, c.Map, string.Join(" / ", c.Agents)))
            .ToList();

        var editSelect = new DiscordSelectComponent(
            $"comps_edit_pick|{team}", "Edit a map's comp...", mapOptions);

        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder()
                .AddEmbed(embed)
                .AddComponents(editSelect));
    }
}