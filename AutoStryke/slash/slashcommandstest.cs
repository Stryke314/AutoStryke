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

namespace AutoStryke.slash
{
    public class slashcommandstest : ApplicationCommandModule
    {


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

            ulong allowedUserId = 889088075395923998; // Replace with the Discord ID of the allowed person

            if (ctx.User.Id != allowedUserId)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("You do not have permission to use this command."));
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

            ulong allowedUserId = 889088075395923998; // Replace with the Discord ID of the allowed person

            if (ctx.User.Id != allowedUserId)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("You do not have permission to use this command."));
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


    }
}

