using AutoStrykeNew.config;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.SlashCommands;
using DSharpPlus.EventArgs;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Collections.Generic;
using AutoStryke.slash;
using DSharpPlus.Entities;



namespace AutoStrykeNew
{
    internal class Program
    {
        // storage for aura points (key: user ID, value: points)
        public static Dictionary<ulong, Dictionary<ulong, int>> AuraPoints = new Dictionary<ulong, Dictionary<ulong, int>>();
        private static DiscordClient client { get; set; }
        private static CommandsNextExtension commands { get; set; }
        static async Task Main(string[] args)
        {
            LoadAuraPoints();

            var jsonreader = new jsonreader();
            await jsonreader.ReadJSON();



            var discordconfig = new DiscordConfiguration()
            {
                Intents = DiscordIntents.All,
                Token = jsonreader.token,
                TokenType = TokenType.Bot,
                AutoReconnect = true,

            };

            client = new DiscordClient(discordconfig);

            client.Ready += Client_Ready;

            var commandsconfig = new CommandsNextConfiguration()
            {
                StringPrefixes = new string[] { jsonreader.prefix },
                EnableDms = true,
                EnableMentionPrefix = true,
                EnableDefaultHelp = true,

            };


            client.MessageCreated += async (s, e) =>
            {
                if (e.Author.IsBot) return;

                if (e.Author.Id == 791982380801327115) 
                {
                    var emoji = e.Guild.Emojis.Values.FirstOrDefault(x => x.Name == "benerd");

                    if (emoji != null)
                    {
                        await e.Message.CreateReactionAsync(emoji);
                    }
                    else
                    {
                        Console.WriteLine("Emoji not found!");
                    }
                }
            };


            commands = client.UseCommandsNext(commandsconfig);

            // Register the slash commands properly
            var slashcommandsconfig = client.UseSlashCommands();
            slashcommandsconfig.RegisterCommands<slashcommandstest>(); 

            // Register normal commands
            commands.RegisterCommands<Commands.Commands>();
            //commands.RegisterCommands<Commands.AuraCommands>();
            await client.ConnectAsync();
            await Task.Delay(-1);
        }

        private static Task Client_Ready(DiscordClient sender, ReadyEventArgs args)
        {

            Console.WriteLine("Bot is ready");
            return Task.CompletedTask;
        }

        public static void SaveAuraPoints()
        {
            try
            {
                var json = JsonConvert.SerializeObject(Program.AuraPoints, Formatting.Indented);
                File.WriteAllText("auraPoints.json", json);
                Console.WriteLine("[DEBUG] Aura points successfully saved.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to save aura points: {ex.Message}");
            }
        }

        public static void LoadAuraPoints()
        {
            try
            {
                if (File.Exists("auraPoints.json"))
                {
                    var json = File.ReadAllText("auraPoints.json");
                    Program.AuraPoints = JsonConvert.DeserializeObject<Dictionary<ulong, Dictionary<ulong, int>>>(json);

                    // Ensure it's not null (in case of a corrupted file)
                    if (Program.AuraPoints == null)
                        Program.AuraPoints = new Dictionary<ulong, Dictionary<ulong, int>>();

                    Console.WriteLine("[DEBUG] Aura points successfully loaded.");
                }
                else
                {
                    Program.AuraPoints = new Dictionary<ulong, Dictionary<ulong, int>>(); // Initialize empty dictionary
                    Console.WriteLine("[DEBUG] No aura points file found. Starting fresh.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to load aura points: {ex.Message}");
                Program.AuraPoints = new Dictionary<ulong, Dictionary<ulong, int>>(); // Ensure bot doesn't crash
            }
        }

    }
}
