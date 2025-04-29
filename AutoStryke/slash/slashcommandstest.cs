using DSharpPlus.SlashCommands;
using DSharpPlus;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoStrykeNew;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using System.Reflection;
using static AutoStrykeNew.Program;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using DSharpPlus.Interactivity.Extensions;

namespace AutoStryke.slash
{
    public class slashcommandstest : ApplicationCommandModule
    {

        public enum ValorantMap
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
            [ChoiceName("Breeze")] Breeze
        }

        //---------------------------------------------------------------------AURA COMMANDS---------------------------------------------------------------------//



        [SlashCommand("viewaura", "View a user's aura points.")]
        public async Task ViewAura(InteractionContext ctx, [Option("user", "User to check aura for")] DiscordUser user = null)
        {
            await ctx.DeferAsync(); // Defer to allow async operations

            // Get the member, default to the invoking user if no user is provided
            DiscordMember member = user != null
                ? await ctx.Guild.GetMemberAsync(user.Id)
                : ctx.Member;

            //  no member is found exit 
            if (member == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Could not find that user."));
                return;
            }

            // Get the points for the member
            int points = GetAuraPoints(ctx.Guild.Id, member);

            // Generate description based on the points
            string description = GenerateDescription(member, ctx.Member, points);


            //  send embed
            await SendAuraEmbedAsync(ctx, member, description);
        }

        //  retrieve aura points from dictionary
        private int GetAuraPoints(ulong guildId, DiscordMember member)
        {
            if (Program.AuraPoints.ContainsKey(guildId) && Program.AuraPoints[guildId].ContainsKey(member.Id))
            {
                return Program.AuraPoints[guildId][member.Id];
            }
            return 0;
        }

        // Function to generate the description based on the points
        private string GenerateDescription(DiscordMember targetMember, DiscordMember commandUser, int points)
        {
            // Determine if the user is checking their own aura or someone else's
            bool isSelf = targetMember.Id == commandUser.Id;
            string pronoun = isSelf ? "your" : "their";
            string possessiveName = isSelf ? "your" : $"{targetMember.Mention}'s"; // Possessive form for names

            // Special cases for specific users
            if (targetMember.Username.ToLower() == "lyriscenic")
            {
                return $"{targetMember.Mention} {(isSelf ? "You have" : $"{targetMember.DisplayName} has")} {points} silly point{(points == 1 ? "" : "s")}.";
            }
            if (targetMember.Username.ToLower() == "chocolatefall")
            {
                return $"{targetMember.Mention} {(isSelf ? "You have" : $"{targetMember.DisplayName} has")} {points} stellar jade{(points == 1 ? "" : "s")}.";
            }
            if (targetMember.Username.ToLower() == "squirr0l")
            {
                return $"{targetMember.Mention} {(isSelf ? "You have" : $"{targetMember.DisplayName} has")} {points} cringe point{(points == 1 ? "" : "s")}.";
            }

            // Standard aura descriptions
            if (points == 69)
            {
                return $"{targetMember.Mention}, {(isSelf ? "You have" : $" has")} 69 points. A perfect number if you ask me.";
            }
            else if (points > 0 && points <= 1000)
            {
                return $"{(isSelf ? targetMember.Mention + "," : "")} {possessiveName} aura twinkles with a warm glow, pulsing with {points} aura point{(points == 1 ? "" : "s")}.";
            }
            else if (points > 1000 && points <= 5000)
            {
                return $"{(isSelf ? targetMember.Mention + "," : "")} {possessiveName} aura radiates with a bright light of {points} aura point{(points == 1 ? "" : "s")}.";
            }
            else if (points > 5000)
            {
                return $"{(isSelf ? targetMember.Mention + "," : "")} {possessiveName} aura blinds {(isSelf ? "your" : "their")} allies due to {(isSelf ? "your" : "their")} {points} aura point{(points == 1 ? "" : "s")}.";
            }
            else if (points < 0 && points >= -1000)
            {
                return $"{(isSelf ? targetMember.Mention + "," : "")} {possessiveName} aura is tinged with negativity, as {(isSelf ? "you bear" : "they bear")} {points} negative point{(Math.Abs(points) == 1 ? "" : "s")}.";
            }
            else if (points < -1000 && points >= -5000)
            {
                return $"{(isSelf ? targetMember.Mention + "," : "")} A somber shadow envelops {possessiveName} aura, as {(isSelf ? "you bear" : "they bear")} {points} negative point{(Math.Abs(points) == 1 ? "" : "s")}.";
            }
            else if (points < -5000)
            {
                return $"{(isSelf ? targetMember.Mention + "," : "")} {possessiveName} aura is completely consumed by darkness, bearing a massive {points} negative aura point{(Math.Abs(points) == 1 ? "" : "s")}. {(isSelf ? "You might want to reflect on your actions!" : "They might want to reflect on their actions!")}";
            }
            else // **Fixed Balanced Aura Message**
            {
                return $"{(isSelf ? targetMember.Mention + "," : "")} {possessiveName} aura is perfectly balanced. {(isSelf ? "You have" : "They have")} 0 points.";
            }
        }

        // Function to create and send the embed with aura description
        private async Task SendAuraEmbedAsync(InteractionContext ctx, DiscordMember member, string description)
        {
            var embed = new DiscordEmbedBuilder
            {
                Title = $"{member.DisplayName}'s Aura",
                Description = description,
                Color = DiscordColor.Blurple
            };

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed));
        }







        [SlashCommand("auraboard", "Displays the leaderboard for aura points.")]
        public async Task Leaderboard(InteractionContext ctx)
        {
            await ctx.DeferAsync(); // Defer response to prevent timeouts

            // Ensure the server exists in the dictionary and sort by points
            var sortedAuraPoints = Program.AuraPoints.ContainsKey(ctx.Guild.Id)
                ? Program.AuraPoints[ctx.Guild.Id].OrderByDescending(x => x.Value).ToList()
                : new List<KeyValuePair<ulong, int>>();

            if (sortedAuraPoints.Count == 0)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("No aura points have been assigned yet!"));
                return;
            }

            // Build the leaderboard message
            var leaderboard = new DiscordEmbedBuilder
            {
                Title = "Aura Points Leaderboard",
                Color = DiscordColor.Blurple
            };

            // Add each user to the leaderboard
            foreach (var (entry, index) in sortedAuraPoints.Select((value, i) => (value, i))) // Iterate with index
            {
                try
                {
                    var user = await ctx.Guild.GetMemberAsync(entry.Key);
                    int points = entry.Value;

                    // Check if this is the top user and add the crown emoji
                    string userName = user != null ? user.DisplayName : "Unknown User";
                    if (index == 0) // Top user
                    {
                        userName = $"👑 {userName}"; // top user
                    }

                    leaderboard.AddField(userName, $"{points} aura point{(points == 1 ? "" : "s")}");
                }
                catch (Exception)
                {
                    leaderboard.AddField("Unknown User", $"{entry.Value} aura points");
                }
            }

            // Send the leaderboard as the response
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(leaderboard));
        }




        [SlashCommand("L-aura", "Displays the leaderboard for negative aura points.")]
        public async Task BottomLeaderboard(InteractionContext ctx)
        {
            await ctx.DeferAsync(); // Defer response to prevent timeouts

            // Ensure the server exists in the dictionary and sort by points in ascending order (most negative first)
            var sortedAuraPoints = Program.AuraPoints.ContainsKey(ctx.Guild.Id)
                ? Program.AuraPoints[ctx.Guild.Id]
                    .Where(x => x.Value < 0)  // Only include negative points
                    .OrderBy(x => x.Value)    // Sort by points ascending (most negative first)
                    .ToList()
                : new List<KeyValuePair<ulong, int>>();

            if (sortedAuraPoints.Count == 0)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("No negative aura points have been assigned yet!"));
                return;
            }

            // Build the leaderboard message
            var leaderboard = new DiscordEmbedBuilder
            {
                Title = "L aura from these guys icl",
                Color = DiscordColor.Blurple
            };

            // Add the bottom 10 negative aura users to the leaderboard
            foreach (var (entry, index) in sortedAuraPoints.Select((value, i) => (value, i)))  // Iterate with index
            {
                try
                {
                    var user = await ctx.Guild.GetMemberAsync(entry.Key);
                    int points = entry.Value;

                    // Check if this is the user with the most negative aura and add the poo emoji
                    string userName = user != null ? user.DisplayName : "Unknown User";
                    if (index == 0)  // Most negative user
                    {
                        userName = $"💩 {userName}";  // Add the poo emoji for the most negative user
                    }

                    leaderboard.AddField(userName, $"{points} aura point{(points == 1 ? "" : "s")}");
                }
                catch (Exception)
                {
                    leaderboard.AddField("Unknown User", $"{entry.Value} aura points");
                }
            }

            // Send the leaderboard as the response
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(leaderboard));
        }






        [SlashCommand("setaura", "Set a user's aura points to a specific value.")]
        public async Task SetAura(InteractionContext ctx,
            [Option("user", "The user whose aura points you want to set")] DiscordUser user,
            [Option("points", "The number of aura points to set")] long points)  // Slash commands require long, not int
        {
            await ctx.DeferAsync(); // Defer response to prevent timeouts

            List<ulong> allowedUserIds = new List<ulong>
            {
                889088075395923998, // Stryke
                791982380801327115, // Squirr0l
            
            };


            if (!allowedUserIds.Contains(ctx.User.Id))
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("You are not allowed to use this command."));
                return;
            }

            try
            {
                ulong guildId = ctx.Guild.Id;  // Get the server ID

                // Convert DiscordUser to DiscordMember
                DiscordMember member = await ctx.Guild.GetMemberAsync(user.Id);

                // Ensure the server exists in the dictionary
                if (!Program.AuraPoints.ContainsKey(guildId))
                    Program.AuraPoints[guildId] = new Dictionary<ulong, int>();

                // Set the aura points for the member in this specific server
                Program.AuraPoints[guildId][member.Id] = (int)points; // Convert long to int

                string response = $"The aura for {member.DisplayName} has been set to {points} point{(points == 1 ? "" : "s")}.";
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(response));

                Console.WriteLine($"[DEBUG] {member.DisplayName}'s aura set to {points} in Guild {guildId} by {ctx.User.Username}");

                Program.SaveAuraPoints();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SetAura failed: {ex.Message}");
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("An error occurred while setting the aura points."));
            }
        }




        [SlashCommand("changeaura", "Add or subtract a user's aura points.")]
        public async Task ChangeAura(InteractionContext ctx,
            [Option("user", "The user whose aura you want to modify")] DiscordUser user,
            [Option("amount", "The number of points to add or subtract")] long points) // Slash commands require long, not int
        {
            await ctx.DeferAsync(); // Defer response to prevent timeouts

            List<ulong> allowedUserIds = new List<ulong>
            {
                889088075395923998, // Stryke
                791982380801327115, // Squirr0l
            
            };


            if (!allowedUserIds.Contains(ctx.User.Id))
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("You are not allowed to use this command."));
                return;
            }

            try
            {
                ulong guildId = ctx.Guild.Id;  // Get the server ID

                // Convert DiscordUser to DiscordMember
                DiscordMember member = await ctx.Guild.GetMemberAsync(user.Id);

                // Ensure the server exists in the dictionary
                if (!Program.AuraPoints.ContainsKey(guildId))
                    Program.AuraPoints[guildId] = new Dictionary<ulong, int>();

                // Ensure the user exists in the dictionary
                if (!Program.AuraPoints[guildId].ContainsKey(member.Id))
                    Program.AuraPoints[guildId][member.Id] = 0;

                // Update the aura points
                Program.AuraPoints[guildId][member.Id] += (int)points; // Convert long to int

                string response;
                if (points > 0)
                {
                    response = $"The aura for {member.DisplayName} has increased by {points} point{(points == 1 ? "" : "s")}.";
                }
                else if (points < 0)
                {
                    response = $"The aura for {member.DisplayName} has decreased by {Math.Abs(points)} point{(Math.Abs(points) == 1 ? "" : "s")}.";
                }
                else
                {
                    response = $"{member.DisplayName}'s aura remains unchanged.";
                }

                // Send the response to the channel
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(response));

                Console.WriteLine($"[DEBUG] {member.DisplayName}'s aura changed by {points} points to {Program.AuraPoints[guildId][member.Id]} in Guild {guildId}.");

                Program.SaveAuraPoints();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ChangeAura failed: {ex.Message}");
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("An error occurred while updating aura points."));
            }
        }


        //----------------------------------------------------------------------------------------------------GRU COMMANDS----------------------------------------------------------------------------------------------------//

        private const ulong StrykeID = 889088075395923998;
        private static string jsonFilePath = "scrims.json"; // Path to  JSON file

        [SlashCommand("Creatematch", "Schedule a scrim or match")]
        public async Task ScrimsCommand(InteractionContext ctx,
            [Option("type", "Is this a Scrim or Match?")]
                [Choice("Scrim", "scrim")]
                [Choice("Match", "match")]
                [Choice("VODreview", "vodreview")]
                string matchType,
            [Option("timecode", "Unix timestamp for the scrim in <t:1745914353> format")] string timecode,
            [Option("map", "Map to be played")] string map,
            [Option("enemy_team", "Name of the enemy team")] string enemyTeam,
            [Option("Apollolink", "Link to the apollo post")] string? apolloLink = null)  // Make apolloLink optional
        {
            if (ctx.User.Id != StrykeID)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("Only Stryke can use this command.")
                                                            .AsEphemeral(true));
                return;
            }

            // Extract the Unix timestamp from the <t:1745914353> format using regex
            var match = Regex.Match(timecode, @"<t:(\d+)>");

            if (!match.Success)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("Invalid timecode format. Please use the format <t:1745914353>.")
                                                            .AsEphemeral(true));
                return;
            }

            // Parse the timestamp (in seconds) to a DateTime
            long timestamp = long.Parse(match.Groups[1].Value);
            DateTime scrimDate = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;

            // Set the title prefix and emoji based on the match type
            string titlePrefix = matchType.ToLower() switch
            {
                "scrim" => "📝 Scrim Scheduled",
                "match" => "⚔️ Match Scheduled",
                "vodreview" => "🎥 VOD Review Scheduled",
                _ => "❓ Unknown Type Scheduled"  // This is a fallback in case of an unrecognized input
            };

            // Build the embed
            var embed = new DiscordEmbedBuilder
            {
                Title = titlePrefix,
                Color = DiscordColor.Cyan,
                Description = $"**Date & Time:** <t:{timestamp}> (<t:{timestamp}:R>)\n" +
                              $"**Map:** {map}\n" +
                              $"**Enemy Team:** {enemyTeam}"
            };

            // Add Apollo link if it's provided
            if (!string.IsNullOrWhiteSpace(apolloLink))
            {
                embed.Description += $"\n**Sign Up Here:** [Apollo Link]({apolloLink})";
            }

            // Send the embed response
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().AddEmbed(embed));

            // Create the scrim data object with MatchType and Apollo link
            var scrimData = new ScrimData
            {
                Timecode = timestamp,
                Map = map,
                EnemyTeam = enemyTeam,
                MatchType = matchType,
                apolloLink = apolloLink  // This will be null if not provided
            };

            // Load existing scrims from the JSON file
            var scrims = LoadScrims();

            // Add the new scrim data to the list
            scrims.Add(scrimData);

            // Save the updated list back to the JSON file
            SaveScrims(scrims);
        }

        public class ScrimData
        {
            public long Timecode { get; set; }
            public string Map { get; set; }
            public string EnemyTeam { get; set; }
            public string MatchType { get; set; } // Add MatchType property
            public string? apolloLink { get; set; } // Apollo link property, nullable
        }

        public void SaveScrims(List<ScrimData> scrims)
        {
            // Convert the scrim data to JSON format
            var json = JsonConvert.SerializeObject(scrims, Formatting.Indented);

            // Write the JSON data to a file
            File.WriteAllText(jsonFilePath, json);
        }

        public List<ScrimData> LoadScrims()
        {
            // Check if the file exists
            if (!File.Exists(jsonFilePath))
            {
                // If the file doesn't exist, return an empty list
                return new List<ScrimData>();
            }

            // Read the JSON data from the file
            var json = File.ReadAllText(jsonFilePath);

            // Convert the JSON data back into a list of ScrimData objects
            return JsonConvert.DeserializeObject<List<ScrimData>>(json) ?? new List<ScrimData>();
        }



        [SlashCommand("matches", "View the next 5 upcoming scrims and matches")]
        public async Task ViewScrimsCommand(InteractionContext ctx)
        {
            var scrims = LoadScrims();

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Filter out past scrims and sort by time
            var upcoming = scrims
                .Where(s => s.Timecode > now)
                .OrderBy(s => s.Timecode)
                .Take(5)
                .ToList();

            if (!upcoming.Any())
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("📭 No upcoming scrims found.")
                        .AsEphemeral(false));
                return;
            }

            var embed = new DiscordEmbedBuilder
            {
                Title = "📅 Upcoming Events",
                Color = DiscordColor.Azure
            };

            foreach (var scrim in upcoming.Select((value, index) => new { value, index })) // Use Select to get both value and index
            {
                // Determine if it's a scrim or match based on the match type
                string emoji = scrim.value.MatchType.ToLower() switch
                {
                    "scrim" => "📝",
                    "match" => "⚔️",
                    "vodreview" => "🎥",
                    _ => "❓"
                };

                string fieldTitle = $"{emoji} {scrim.value.EnemyTeam} on {scrim.value.Map}";

                string fieldContent = $"**Date & Time:** <t:{scrim.value.Timecode}> (<t:{scrim.value.Timecode}:R>)\n" +
                                      $"**Map:** {scrim.value.Map}\n" +
                                      $"**Enemy Team:** {scrim.value.EnemyTeam}";

                if (!string.IsNullOrWhiteSpace(scrim.value.apolloLink))
                {
                    fieldContent += $"\n**Sign Up Here:** [Apollo Link]({scrim.value.apolloLink})";
                }

                embed.AddField(fieldTitle, fieldContent, inline: false);

                // Add a blank field to create a gap, but only if this isn't the last entry
                if (scrim.index < upcoming.Count() - 1)
                {
                    embed.AddField("\u200B", "\u200B", inline: false); // Zero-width space for a blank field
                }
            }

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().AddEmbed(embed));
        }




        [SlashCommand("deletematch", "Delete a match from the list")]
        public async Task DeleteScrimCommand(InteractionContext ctx)
        {
            if (ctx.User.Id != StrykeID)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("Only Stryke can use this command.")
                        .AsEphemeral(true));
                return;
            }

            var scrims = LoadScrims();

            if (!scrims.Any())
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("No scrims found.")
                        .AsEphemeral(true));
                return;
            }

            var embed = new DiscordEmbedBuilder
            {
                Title = "🗑️ Scrims List - Select a number to delete or type 'cancel' to cancel",
                Color = DiscordColor.Red
            };

            for (int i = 0; i < scrims.Count; i++)
            {
                var s = scrims[i];
                string emoji = s.MatchType.ToLower() switch
                {
                    "scrim" => "📝",               // Emoji for scrim
                    "match" => "⚔️",              // Emoji for match
                    "vodreview" => "🎥",         // Emoji for VOD review
                    _ => "❓"                      // Default fallback emoji for unknown types
                };
                embed.AddField($"#{i + 1}: {emoji} {s.EnemyTeam} on {s.Map}", $"<t:{s.Timecode}>", false);
            }

            // Send the list with ephemeral messages
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(embed)
                    .AsEphemeral(true));

            var interactivity = ctx.Client.GetInteractivity();
            var response = await interactivity.WaitForMessageAsync(
                x => x.Author.Id == ctx.User.Id &&
                     x.ChannelId == ctx.Channel.Id, TimeSpan.FromSeconds(120));

            if (!response.TimedOut)
            {
                if (response.Result.Content.ToLower() == "cancel")
                {
                    // Cancel the command and notify the user
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ Command canceled.")
                            .AsEphemeral(true));
                    return;  // Exit the command early if canceled
                }

                if (int.TryParse(response.Result.Content, out int index) &&
                    index >= 1 && index <= scrims.Count)
                {
                    scrims.RemoveAt(index - 1);
                    SaveScrims(scrims);

                    // Confirmation message sent only to the user
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent($"✅ Scrim #{index} deleted.")
                            .AsEphemeral(true));
                }
                else
                {
                    // Error message sent only to the user
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent($"❌ Invalid input. Please enter a number between 1 and {scrims.Count}, or 'cancel' to cancel.")
                            .AsEphemeral(true));
                }
            }
            else
            {
                // Timeout message sent only to the user
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ Timed out waiting for a response.")
                        .AsEphemeral(true));
            }
        }

        [SlashCommand("createcomp", "Create a comp for a specific map. This will overwrite the existing comp.")]
        public async Task CreateCompCommand(InteractionContext ctx,
            [Option("map", "Select a Valorant map")] ValorantMap map,
            [Option("agent1", "First agent")] string agent1,
            [Option("agent2", "Second agent")] string agent2,
            [Option("agent3", "Third agent")] string agent3,
            [Option("agent4", "Fourth agent")] string agent4,
            [Option("agent5", "Fifth agent")] string agent5)
        {
            var comps = Program.LoadComps();

            var agentNames = new[] { agent1, agent2, agent3, agent4, agent5 };
            var formattedAgents = new List<string>();

            foreach (var name in agentNames)
            {
                var emojiName = name.ToLower();
                var emoji = ctx.Guild.Emojis.Values.FirstOrDefault(e => e.Name.ToLower() == emojiName);

                if (emoji != null)
                {
                    formattedAgents.Add(emoji.ToString()); // Proper format like <:viper:1234567890>
                }
                else
                {
                    formattedAgents.Add(name); // fallback to plain text
                }
            }

            var newComp = new Program.ValorantComp
            {
                Map = map.ToString().ToLower(), // map enum as string
                Agents = formattedAgents
            };

            comps[map.ToString().ToLower()] = newComp;
            Program.SaveComps(comps);

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent($"✅ Comp for **{map}** saved:\n{string.Join(" ", formattedAgents)}"));
        }



        [SlashCommand("comps", "View all saved comps.")]
        public async Task ViewAllCompsCommand(InteractionContext ctx)
        {
            var comps = Program.LoadComps();

            if (comps.Count == 0)
            {
                await ctx.CreateResponseAsync("❌ No comps saved yet.");
                return;
            }

            var embed = new DiscordEmbedBuilder()
                .WithTitle("📋 All Saved Comps")
                .WithColor(DiscordColor.Azure);

            foreach (var kvp in comps)
            {
                var mapName = char.ToUpper(kvp.Key[0]) + kvp.Key[1..]; // Capitalize map name
                var agents = string.Join(" ", kvp.Value.Agents);
                embed.AddField($"__{mapName}__", agents, false);
            }

            embed.WithFooter("Use /createcomp or a map-specific command to update comps.");

            await ctx.CreateResponseAsync(embed);
        }



        private async Task ViewMapComp(InteractionContext ctx, string key, string displayName)
        {
            var comps = Program.LoadComps();

            if (comps.TryGetValue(key, out var comp))
            {
                // Attempt to resolve each agent string into a server emoji
                var emojiList = new List<string>();

                foreach (var agent in comp.Agents)
                {
                    var emojiName = agent.Trim(':'); // from ":viper:" -> "viper"
                    var emoji = ctx.Guild.Emojis.Values.FirstOrDefault(e => e.Name.ToLower() == emojiName);

                    emojiList.Add(emoji?.ToString() ?? agent); // fallback to raw text if not found
                }

                await ctx.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"**{displayName} Comp:**\n{string.Join(" ", emojiList)}"));
            }
            else
            {
                await ctx.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"❌ No comp saved for {displayName}."));
            }
        }


        [SlashCommand("ascent", "View the comp for Ascent")]
        public async Task ViewAscentComp(InteractionContext ctx)
        {
            await ViewMapComp(ctx, "ascent", "Ascent");
        }

        [SlashCommand("haven", "View the comp for Haven")]
        public async Task ViewHavenComp(InteractionContext ctx)
        {
            await ViewMapComp(ctx, "haven", "Haven");
        }

        [SlashCommand("split", "View the comp for Split")]
        public async Task ViewSplitComp(InteractionContext ctx)
        {
            await ViewMapComp(ctx, "split", "Split");
        }

        [SlashCommand("bind", "View the comp for Bind")]
        public async Task ViewBindComp(InteractionContext ctx)
        {
            await ViewMapComp(ctx, "bind", "Bind");
        }

        [SlashCommand("icebox", "View the comp for Icebox")]
        public async Task ViewIceboxComp(InteractionContext ctx)
        {
            await ViewMapComp(ctx, "icebox", "Icebox");
        }

        [SlashCommand("pearl", "View the comp for Pearl")]
        public async Task ViewPearlComp(InteractionContext ctx)
        {
            await ViewMapComp(ctx, "pearl", "Pearl");
        }

        [SlashCommand("fracture", "View the comp for Fracture")]
        public async Task ViewFractureComp(InteractionContext ctx)
        {
            await ViewMapComp(ctx, "fracture", "Fracture");
        }

        [SlashCommand("sunset", "View the comp for Sunset")]
        public async Task ViewSunsetComp(InteractionContext ctx)
        {
            await ViewMapComp(ctx, "sunset", "Sunset");
        }

        [SlashCommand("abyss", "View the comp for Abyss")]
        public async Task ViewAbyssComp(InteractionContext ctx)
        {
            await ViewMapComp(ctx, "abyss", "Abyss");
        }

        [SlashCommand("lotus", "View the comp for Lotus")]
        public async Task ViewLotusComp(InteractionContext ctx)
        {
            await ViewMapComp(ctx, "lotus", "Lotus");
        }

        [SlashCommand("breeze", "View the comp for Breeze")]
        public async Task ViewBreezeComp(InteractionContext ctx)
        {
            await ViewMapComp(ctx, "breeze", "Breeze");
        }




    }
}


