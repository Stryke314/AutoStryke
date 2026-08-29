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

    // ---------------- /team ----------------
    [SlashCommand("team", "Show a team's comp on a map")]
    public async Task TeamCommand(InteractionContext ctx,
        [Option("team", "Team name")] string team,
        [Option("map", "Map name")] ProCompCommands.ProCompMap map)
    {
        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

        var db = LoadDatabase();
        var mapName = map.ToString();

        if (db.ContainsKey(mapName) && db[mapName].ContainsKey(team))
        {
            var comp = db[mapName][team];
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"**{team}** on **{mapName}** plays: {string.Join(", ", comp)}"));
        }
        else
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"No comp found for **{team}** on **{mapName}**."));
        }
    }
}