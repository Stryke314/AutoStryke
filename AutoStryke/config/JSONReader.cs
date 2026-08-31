using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoStrykeNew.config
{
    internal class jsonreader
    {
        public string token { get; set; }
        public string prefix { get; set; }
        public string henrikApiKey { get; set; }
        public string premierTeamName { get; set; }
        public string premierTeamTag { get; set; }
        public string premierRegion { get; set; }
        public string pythonInterpreter { get; set; }

        public async Task ReadJSON()
        {
            var configPaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "config.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "config.json"),
                Path.Combine(AppContext.BaseDirectory, "config", "config.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "config", "config.json"),
            };

            var configPath = configPaths.FirstOrDefault(File.Exists)
                ?? throw new FileNotFoundException(
                    "Could not find config.json. Put it beside the bot executable or in a config folder.");

            using (StreamReader sr = new StreamReader(configPath))
            {
                string json = await sr.ReadToEndAsync();
                JSONstructure data = JsonConvert.DeserializeObject<JSONstructure>(json);

                this.token = data.token;
                this.prefix = data.prefix;
                this.henrikApiKey = data.henrikApiKey;
                this.premierTeamName = data.premierTeamName;
                this.premierTeamTag = data.premierTeamTag;
                this.premierRegion = data.premierRegion;
                this.pythonInterpreter = data.pythonInterpreter;
            }
        }
    }

    internal sealed class JSONstructure
    {
        public string token { get; set; }
        public string prefix { get; set; }
        public string henrikApiKey { get; set; }
        public string premierTeamName { get; set; }
        public string premierTeamTag { get; set; }
        public string premierRegion { get; set; }
        public string pythonInterpreter { get; set; }
    }
}