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

    // ---------------- /addcomp ----------------
    [SlashCommand("addcomp", "Add a team's composition for a map")]
    public async Task AddCompCommand(InteractionContext ctx,
        [Option("team", "Team name")] string team,
        [Option("map", "Map name")] string map,
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

        // Load or create database
        var db = File.Exists(JsonFile)
            ? JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<string>>>>(File.ReadAllText(JsonFile))
            : new Dictionary<string, Dictionary<string, List<string>>>();

        if (!db.ContainsKey(map))
            db[map] = new Dictionary<string, List<string>>();

        db[map][team] = agents;

        File.WriteAllText(JsonFile, JsonSerializer.Serialize(db, new JsonSerializerOptions { WriteIndented = true }));

        await ctx.EditResponseAsync(new DiscordWebhookBuilder()
            .WithContent($"Saved comp for **{team}** on **{map}**."));
    }

    // ---------------- /team ----------------
    [SlashCommand("team", "Show a team's comp on a map")]
    public async Task TeamCommand(InteractionContext ctx,
        [Option("team", "Team name")] string team,
        [Option("map", "Map name")] string map)
    {
        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

        if (!File.Exists(JsonFile))
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent("No comps database found."));
            return;
        }

        var db = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<string>>>>(File.ReadAllText(JsonFile));

        if (db.ContainsKey(map) && db[map].ContainsKey(team))
        {
            var comp = db[map][team];
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"**{team}** on **{map}** plays: {string.Join(", ", comp)}"));
        }
        else
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"No comp found for **{team}** on **{map}**."));
        }
    }
}
