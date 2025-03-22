using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoStrykeNew.Commands
{
    public class Commands : BaseCommandModule
    {
        [Command("ping")]
        public async Task Ping(CommandContext ctx)
        {
            await ctx.Channel.SendMessageAsync("Pong!").ConfigureAwait(false);
        }

        [Command("cringe")]
        public async Task cringe(CommandContext ctx, string username = null)
        {
            // If no username is provided, use the command user's username
            string user = username ?? ctx.User.Username;
            await ctx.Channel.SendMessageAsync($"{user} is cringe").ConfigureAwait(false);
        }

        [Command("mc")]

        public async Task MCCommand(CommandContext ctx)
        {
            string response = @"holy FUCK pleapsleapsleaspleaspleaspelaspelsapel i NEED it I ALR HAD A RANT BUT THE OLD CHANNEL IS KAPUTT I DO NOT WANT TO SEE MIMIMI STORAGE TECH NULOAD LOAD SLIME CHUNK 1WT OPT 20TPS I HAVE HAD ENOUGH!!!! WHAT THE FUCK IS VARIABLE MIXED STORAGE SYSTEM I DO NOT CARE!!!! GET IT OUT OF GENERAL!!!!!! REDSTONE PISTON OBSERVER MINECART 2B2T IS THE OLDEST ANARCHY SERVER IN MINECRAFT HOPPER CHEST HOW ABOUT YOU HOP INTO SOME BITCHES DMS HOLY FUCK!!!!!";

            await ctx.Channel.SendMessageAsync(response).ConfigureAwait(false);
        }



        [Command("j")]

        public async Task joke(CommandContext ctx)
        {
            string response = @"Nah you can't get outta this one with a /j tut tut";

            await ctx.Channel.SendMessageAsync(response).ConfigureAwait(false);
        }

        [Command("pwea")]

        //sends a document of best players to watch for each agent
        public async Task playerstowatch(CommandContext ctx)
        {
            string response = @"https://sites.google.com/view/pwea/home";

            await ctx.Channel.SendMessageAsync(response).ConfigureAwait(false);
        }

        [Command("articulate")]

        //sends a document of best players to watch for each agent
        public async Task articulate(CommandContext ctx)
        {
            string response = @"https://cdn.discordapp.com/attachments/1320708926827790339/1352774536361279568/Screenshot_2025-03-21_224044.png?ex=67dfe5b6&is=67de9436&hm=a15b43b7c4e67e42af5b63439b0f44183ffa6e1fe957da1131f0c77d97c808dc&";

            await ctx.Channel.SendMessageAsync(response).ConfigureAwait(false);
        }

        [Command("astra")]

        //sends a document of best players to watch for each agent
        public async Task astra(CommandContext ctx)
        {
            string response = @"https://cdn.discordapp.com/attachments/1282036944611708972/1353137838526431282/quote_1343343367169376346-2.png?ex=67e08f50&is=67df3dd0&hm=51cfd2b861a78d2047a9c3da2a5ae56b2bfc5c4a9319a81a883cd03a87e135cf&";

            await ctx.Channel.SendMessageAsync(response).ConfigureAwait(false);
        }

        [Command("yap")]

        //sends a document of best players to watch for each agent
        public async Task yappanese(CommandContext ctx)
        {
            string response = @"https://cdn.discordapp.com/attachments/1320708926827790339/1353137479833751552/yappanese.mp4?ex=67e08efb&is=67df3d7b&hm=732a4d5a4c4cf20af0470d69558d7e07e646ba5b89298cf28708e1d99e2bf268&";

            await ctx.Channel.SendMessageAsync(response).ConfigureAwait(false);
        }

    }
    
    /*
    public class AuraCommands : BaseCommandModule
    {




        [Command("viewaura")]
        public async Task ViewAura(CommandContext ctx, string username = null)
        {
            // Get member based on username or invoking user
            DiscordMember member = await GetMemberAsync(ctx, username);

            // If no member is found, exit early
            if (member == null) return;

            // Get the points for the member
            int points = GetAuraPoints(ctx.Guild.Id, member);

            // Generate description based on the points
            string description = GenerateDescription(member, points);

            // Create and send the embed
            await SendAuraEmbedAsync(ctx, member, description);
        }

        // Function to retrieve member from the server by username or use invoking member
        private async Task<DiscordMember> GetMemberAsync(CommandContext ctx, string username)
        {
            if (username == null)
            {
                // If no username is provided, use the invoking user's member object
                return ctx.Member;
            }

            // Search for the member by username (case-insensitive)
            var member = ctx.Guild.Members.Values
                .FirstOrDefault(m => m.DisplayName.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (member == null)
            {
                // If the user with the given username doesn't exist in the server, send an error
                await ctx.Channel.SendMessageAsync("User not found in this server. Please check the username.").ConfigureAwait(false);
            }

            return member;
        }

        // Function to retrieve aura points from the dictionary
        private int GetAuraPoints(ulong guildId, DiscordMember member)
        {
            if (Program.AuraPoints.ContainsKey(guildId) && Program.AuraPoints[guildId].ContainsKey(member.Id))
            {
                return Program.AuraPoints[guildId][member.Id];
            }
            return 0;
        }

        // Function to generate the description based on the points and special cases
        private string GenerateDescription(DiscordMember member, int points)
        {
            //section for people who want special names for their points
            if (member.Username.ToLower() == "lyriscenic" || member.Username.ToLower() == "lyriscenic#4906")
            {
                return $"{member.Mention}, you have {points} silly point{(points == 1 ? "" : "s")}.";
            }

            if (member.Username.ToLower() == "chocolatefall")
            {
                return $"{member.Mention}, you have {points} stellar jade{(points == 1 ? "" : "s")}.";
            }


            //thresholds for points (yes ik i dont need the pluralisation bit for most of it, oh well)
            if (points == 69)

            {
                return $"{member.Mention}, you have 69 points. a perfect number of points if you ask me";
            }

            else if (points > 0 && points <= 1000)
            {
                return $"{member.Mention}, your aura twinkles with a warm glow, pulsing with {points} aura point{(points == 1 ? "" : "s")}.";
            }

            else if (points > 1000 && points <= 5000)
            {
                return $"{member.Mention}, your aura radiates with a bright light of {points} aura point{(points == 1 ? "" : "s")}.";
            }

            else if (points > 5000)
            {
                return $"{member.Mention}, your aura blinds your allies due to your {points} aura point{(points == 1 ? "" : "s")}.";
            }

            else if (points < 0 && points >= -1000)
            {
                return $"{member.Mention}, Your aura is tinged with negativity, as you bear {points} negative point{(System.Math.Abs(points) == 1 ? "" : "s")}.";
            }

            else if (points < -1000 && points >= -5000)
            {
                return $"{member.Mention}, A somber shadow envelops you, as you bear {points} negative point{(System.Math.Abs(points) == 1 ? "" : "s")}.";
            }

            else if (points < -5000)
            {
                return $"{member.Mention}, Your aura is completely consumed by darkness , bearing a massive {points} negative aura point{(points == 1 ? "" : "s")}. You might want to reflect on your actions!";
            }

            else
            {
                return $"{member.Mention}, your aura is perfectly balanced, you have 0 points.";
            }
        }

        // Function to create and send the embed with aura description
        private async Task SendAuraEmbedAsync(CommandContext ctx, DiscordMember member, string description)
        {
            var embed = new DiscordEmbedBuilder
            {
                Title = $"{member.DisplayName}'s Aura",
                Description = description,
                Color = DiscordColor.Blurple
            };

            await ctx.Channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
        }






        [Command("changeaura")]
        public async Task AddAura(CommandContext ctx, DiscordMember member, int points)
        {
            ulong allowedUserId = 889088075395923998; // Replace with the Discord ID of the allowed person

            if (ctx.User.Id != allowedUserId)
            {
                await ctx.Channel.SendMessageAsync("You do not have permission to use this command.").ConfigureAwait(false);
                return;
            }

            try
            {
                ulong guildId = ctx.Guild.Id;  // Get the server ID

                // Ensure the server exists in the dictionary
                if (!Program.AuraPoints.ContainsKey(guildId))
                    Program.AuraPoints[guildId] = new Dictionary<ulong, int>();

                // Ensure the user exists in the dictionary
                if (!Program.AuraPoints[guildId].ContainsKey(member.Id))
                    Program.AuraPoints[guildId][member.Id] = 0;

                // Update the aura points 
                Program.AuraPoints[guildId][member.Id] += points;

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
                await ctx.Channel.SendMessageAsync(response).ConfigureAwait(false);

                Console.WriteLine($"[DEBUG] {member.DisplayName}'s aura changed by {points} points to {Program.AuraPoints[guildId][member.Id]} in Guild {guildId}.");

                Program.SaveAuraPoints();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] AddAura failed: {ex.Message}");
                await ctx.Channel.SendMessageAsync("An error occurred while updating aura points.").ConfigureAwait(false);
            }
        }







        [Command("setaura")]
        public async Task SetAura(CommandContext ctx, DiscordMember member, int points)
        {
            ulong allowedUserId = 889088075395923998; // Replace with the Discord ID of the allowed person

            if (ctx.User.Id != allowedUserId)
            {
                await ctx.Channel.SendMessageAsync("You do not have permission to use this command.").ConfigureAwait(false);
                return;
            }

            try
            {
                ulong guildId = ctx.Guild.Id;  // Get the server ID

                // Ensure the server exists in the dictionary
                if (!Program.AuraPoints.ContainsKey(guildId))
                    Program.AuraPoints[guildId] = new Dictionary<ulong, int>();

                // Set the aura points for the member in this specific server
                Program.AuraPoints[guildId][member.Id] = points;

                string response = $"The aura for {member.DisplayName} has been set to {points} point{(points == 1 ? "" : "s")}.";
                await ctx.Channel.SendMessageAsync(response).ConfigureAwait(false);

                Console.WriteLine($"[DEBUG] {member.DisplayName}'s aura set to {points} in Guild {guildId} by {ctx.User.Username}");

                Program.SaveAuraPoints();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SetAura failed: {ex.Message}");
                await ctx.Channel.SendMessageAsync("An error occurred while setting the aura points.").ConfigureAwait(false);
            }
        }






        [Command("auraboard")]
        public async Task Leaderboard(CommandContext ctx)
        {
            // Ensure the server exists in the dictionary and sort by points
            var sortedAuraPoints = Program.AuraPoints.ContainsKey(ctx.Guild.Id)
                ? Program.AuraPoints[ctx.Guild.Id].OrderByDescending(x => x.Value).ToList()
                : new List<KeyValuePair<ulong, int>>();

            if (sortedAuraPoints.Count == 0)
            {
                await ctx.Channel.SendMessageAsync("No aura points have been assigned yet!").ConfigureAwait(false);
                return;
            }

            // Build the leaderboard message
            var leaderboard = new DiscordEmbedBuilder
            {
                Title = "Aura Points Leaderboard",
                Color = DiscordColor.Blurple
            };

            // Add each user to the leaderboard
            foreach (var (entry, index) in sortedAuraPoints.Select((value, i) => (value, i)))  // Iterate with index
            {
                var user = await ctx.Guild.GetMemberAsync(entry.Key);
                int points = entry.Value;

                // Check if this is the top user and add the crown emoji
                string userName = user != null ? user.DisplayName : "Unknown User";
                if (index == 0)  // Top user
                {
                    userName = $"👑 {userName}";  // Add the crown emoji for the top user
                }

                leaderboard.AddField(userName,
                    $"{points} aura point{(points == 1 ? "" : "s")}");
            }

            await ctx.Channel.SendMessageAsync(embed: leaderboard).ConfigureAwait(false);
        }

        [Command("L-aura")]
        public async Task BottomLeaderboard(CommandContext ctx)
        {
            // Ensure the server exists in the dictionary and sort by points in ascending order for the bottom 10
            var sortedAuraPoints = Program.AuraPoints.ContainsKey(ctx.Guild.Id)
                ? Program.AuraPoints[ctx.Guild.Id]
                    .Where(x => x.Value < 0)  // Only include negative points
                    .OrderBy(x => x.Value)    // Sort by points ascending (bottom first)
                    .ToList()
                : new List<KeyValuePair<ulong, int>>();

            if (sortedAuraPoints.Count == 0)
            {
                await ctx.Channel.SendMessageAsync("No negative aura points have been assigned yet!").ConfigureAwait(false);
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
                var user = await ctx.Guild.GetMemberAsync(entry.Key);
                int points = entry.Value;

                // Check if this is the user with the most negative aura and add the poo emoji
                string userName = user != null ? user.DisplayName : "Unknown User";
                if (index == 0)  // Most negative user
                {
                    userName = $"💩 {userName}";  // Add the poo emoji for the most negative user
                }

                leaderboard.AddField(userName,
                    $"{points} aura point{(points == 1 ? "" : "s")}");
            }

            await ctx.Channel.SendMessageAsync(embed: leaderboard).ConfigureAwait(false);
        }

   
    }
    */

}
