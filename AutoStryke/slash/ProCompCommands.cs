using System.Diagnostics;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Microsoft.Data.Sqlite;

public class ProCompCommands : ApplicationCommandModule
{
    private static readonly string DataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
    private static readonly string DatabasePath = Path.Combine(DataDirectory, "comps.db");
    private static readonly string ImporterPath = Path.Combine(DataDirectory, "pro_comp_importer.py");

    [SlashCommand("updateprocomps", "Fetch the latest VCT and VCL pro compositions")]
    public async Task UpdateProComps(InteractionContext ctx)
    {
        if (ctx.Guild is null || ctx.User.Id != ctx.Guild.OwnerId)
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

        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AsEphemeral(true));

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("PRO_COMP_PYTHON") ?? "python",
                WorkingDirectory = DataDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add(ImporterPath);

            using var process = Process.Start(startInfo);
            if (process is null)
                throw new InvalidOperationException("Could not start the Python importer.");

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(error) ? output : error;
                await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                    .WithContent($"The update failed. {LastLine(detail)}"));
                return;
            }

            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"Pro-comp database updated successfully. {LastLine(output)}"));
        }
        catch (Exception exception)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"The update could not start: {exception.Message}"));
        }
    }

    [SlashCommand("findprocomp", "Show pro teams that have played a five-agent composition")]
    public async Task FindProComp(
        InteractionContext ctx,
        [Option("agents", "Five agents, separated by commas")] string agentsInput,
        [Option("map", "Optional map to narrow the results")] string? map = null)
    {
        var agents = agentsInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (agents.Length != 5 || agents.Any(string.IsNullOrWhiteSpace))
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Enter exactly five agents separated by commas, for example: `Jett, Omen, Sova, Killjoy, Viper`.")
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

        var composition = string.Join(" / ", agents.OrderBy(agent => agent, StringComparer.OrdinalIgnoreCase));
        var matches = new List<(string Team, string Map)>();

        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT team, map
            FROM compositions
            WHERE lower(comp) = lower($composition)
              AND ($map IS NULL OR lower(map) = lower($map))
            ORDER BY team, map
            LIMIT 50;
            """;
        command.Parameters.AddWithValue("$composition", composition);
        command.Parameters.AddWithValue("$map", (object?)map?.Trim() ?? DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            matches.Add((reader.GetString(0), reader.GetString(1)));

        if (matches.Count == 0)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent($"No recorded pro teams have played **{composition}**{(string.IsNullOrWhiteSpace(map) ? string.Empty : $" on {map.Trim()}")}.")
                    .AsEphemeral(true));
            return;
        }

        var results = string.Join("\n", matches.Select(match => $"• **{match.Team}** — {match.Map}"));
        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder()
                .AddEmbed(new DiscordEmbedBuilder()
                    .WithTitle("Pro teams using this composition")
                    .WithDescription($"**{composition}**\n\n{results}")
                    .WithColor(DiscordColor.Blurple)));
    }

    private static string LastLine(string text)
    {
        var line = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return string.IsNullOrWhiteSpace(line) ? "No details were returned." : line;
    }
}
