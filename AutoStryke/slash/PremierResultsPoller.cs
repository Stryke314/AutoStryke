using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using System.Runtime;

namespace AutoStrykeNew
{
    /// <summary>
    /// Polls HenrikDev's unofficial Valorant API for new completed Premier
    /// matches and writes them straight into matchResults.json, the same
    /// file /matchresults already reads from - no manual entry needed.
    /// </summary>
    public static class PremierResultsPoller
    {
        private const string SeenMatchesFile = "premier_seen_matches.json";
        private static readonly HttpClient Http = new HttpClient { BaseAddress = new Uri("https://api.henrikdev.xyz") };

        private static string GetSeenMatchesFilePath()
        {
            var configPaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, SeenMatchesFile),
                Path.Combine(Directory.GetCurrentDirectory(), SeenMatchesFile),
                Path.Combine(AppContext.BaseDirectory, "config", SeenMatchesFile),
                Path.Combine(Directory.GetCurrentDirectory(), "config", SeenMatchesFile),
            };

            return configPaths.FirstOrDefault(File.Exists) ?? Path.Combine(Directory.GetCurrentDirectory(), SeenMatchesFile);
        }

        private class PremierTeamResponse
        {
            public PremierTeamData data { get; set; }
        }

        private class PremierTeamData
        {
            public string id { get; set; }
        }

        private class PremierHistoryResponse
        {
            public PremierHistoryData data { get; set; }
        }

        private class PremierHistoryData
        {
            public List<PremierLeagueMatch> league_matches { get; set; }
        }

        private class PremierLeagueMatch
        {
            public string id { get; set; }
            public int points_before { get; set; }
            public int points_after { get; set; }
            public DateTime started_at { get; set; }
            public int rounds_won { get; set; }
            public int rounds_lost { get; set; }
            public string map { get; set; }
            public string opponent { get; set; }
        }

        // Minimal shape of the fields we actually need from a full match object.
        private class MatchDetailResponse
        {
            public MatchDetailData data { get; set; }
        }

        private class MatchDetailData
        {
            public MatchDetailMetadata metadata { get; set; }
            public List<MatchDetailTeam> teams { get; set; }
        }

        private class MatchDetailMetadata
        {
            public string map { get; set; }
        }

        private class MatchDetailTeam
        {
            public string team_id { get; set; }
            public bool won { get; set; }
            public int rounds_won { get; set; }
            public int rounds_lost { get; set; }
        }

        public static async Task<int> CheckForNewResults(
            string apiKey, string teamName, string teamTag, string region)
        {
            Console.WriteLine($"[PREMIER] Starting Premier poll check for {teamName}#{teamTag} in {region}");
            Console.WriteLine($"[PREMIER] API Key (first 10 chars): {apiKey.Substring(0, Math.Min(10, apiKey.Length))}...");
            
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(teamName))
            {
                Console.WriteLine("[PREMIER] Premier polling not configured - skipping");
                return 0; // Premier polling not configured - skip quietly.
            }

            Http.DefaultRequestHeaders.Authorization = null;
            Http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", apiKey);

            Console.WriteLine($"[PREMIER] Fetching team ID for {teamName}#{teamTag}");
            var teamResponse = await GetJson<PremierTeamResponse>(
                $"/valorant/v1/premier/{Uri.EscapeDataString(teamName)}/{Uri.EscapeDataString(teamTag)}");

            var teamId = teamResponse?.data?.id;
            if (string.IsNullOrWhiteSpace(teamId))
            {
                Console.WriteLine($"[PREMIER] Could not find team ID for {teamName}#{teamTag}");
                Console.WriteLine($"[PREMIER] Team response was null or empty");
                return 0;
            }
            
            Console.WriteLine($"[PREMIER] Found team ID: {teamId}");

            Console.WriteLine($"[PREMIER] Fetching match history for team {teamId}");
            var history = await GetJson<PremierHistoryResponse>(
                $"/valorant/v1/premier/{teamId}/history");

            var matches = history?.data?.league_matches ?? new List<PremierLeagueMatch>();
            Console.WriteLine($"[PREMIER] Found {matches.Count} total matches in history");
            
            var seenIds = LoadSeenMatchIds();
            var newMatches = matches.Where(m => !seenIds.Contains(m.id)).ToList();
            Console.WriteLine($"[PREMIER] Found {newMatches.Count} new matches to process");
            
            if (matches.Count > 0)
            {
                Console.WriteLine($"[PREMIER] First match ID in history: {matches[0].id}");
                Console.WriteLine($"[PREMIER] Last match ID in history: {matches[matches.Count - 1].id}");
            }

            if (newMatches.Count == 0)
            {
                Console.WriteLine("[PREMIER] No new matches to record");
                return 0;
            }

            var results = Program.LoadMatchResults();
            int added = 0;

            foreach (var match in newMatches)
            {
                Console.WriteLine($"[PREMIER] Processing match {match.id} from {match.started_at}");
                Console.WriteLine($"[PREMIER] Match data available - points: {match.points_before}→{match.points_after}, rounds: {match.rounds_won}-{match.rounds_lost}, map: {match.map}, opponent: {match.opponent}");

                // Try to use data from Premier history response first
                if (!string.IsNullOrWhiteSpace(match.map) && match.rounds_won > 0 && match.rounds_lost > 0)
                {
                    // Use data from Premier history
                    var opponentName = !string.IsNullOrWhiteSpace(match.opponent) ? match.opponent : "Premier opponent";
                    Console.WriteLine($"[PREMIER] Using Premier history data: {opponentName} on {match.map}, Score: {match.rounds_won}-{match.rounds_lost}");

                    results.Add(new Program.MatchResult
                    {
                        Opponent = opponentName,
                        Map = match.map,
                        OurScore = match.rounds_won,
                        TheirScore = match.rounds_lost,
                        Date = match.started_at,
                    });
                    added++;
                    seenIds.Add(match.id);
                }
                else
                {
                    // Try to fetch from match detail endpoint
                    Console.WriteLine($"[PREMIER] Premier history incomplete, trying match detail endpoint...");
                    var detail = await GetJson<MatchDetailResponse>(
                        $"/valorant/v4/match/{region}/{match.id}");

                    if (detail?.data is null)
                    {
                        // Couldn't fetch detail - mark as seen anyway to avoid retries
                        Console.WriteLine($"[PREMIER] Could not fetch details for match {match.id} - marking as seen and skipping");
                        seenIds.Add(match.id);
                        continue;
                    }

                    var ourTeam = detail.data.teams?.FirstOrDefault(t => t.team_id == teamId);
                    var theirTeam = detail.data.teams?.FirstOrDefault(t => t.team_id != teamId);

                    if (ourTeam is null || theirTeam is null)
                    {
                        Console.WriteLine($"[PREMIER] Could not identify teams in match {match.id}");
                        seenIds.Add(match.id);
                        continue;
                    }

                    var opponentName = await ResolveTeamName(theirTeam.team_id) ?? "Premier opponent";
                    Console.WriteLine($"[PREMIER] Match details: {opponentName} on {detail.data.metadata?.map}, Score: {ourTeam.rounds_won}-{theirTeam.rounds_won}");

                    results.Add(new Program.MatchResult
                    {
                        Opponent = opponentName,
                        Map = detail.data.metadata?.map ?? "Unknown",
                        OurScore = ourTeam.rounds_won,
                        TheirScore = theirTeam.rounds_won,
                        Date = match.started_at,
                    });
                    added++;
                    seenIds.Add(match.id);
                }
                Console.WriteLine($"[PREMIER] Successfully recorded match {match.id}");
            }

            if (added > 0)
            {
                Program.SaveMatchResults(results);
                Console.WriteLine($"[PREMIER] Saved {added} new match results to matchResults.json");
            }

            SaveSeenMatchIds(seenIds);
            Console.WriteLine($"[PREMIER] Updated seen matches file with {seenIds.Count} total matches");
            return added;
        }

        private class PremierTeamNameResponse
        {
            public PremierTeamNameData data { get; set; }
        }

        private class PremierTeamNameData
        {
            public string name { get; set; }
            public string tag { get; set; }
        }

        private static async Task<string> ResolveTeamName(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                return null;

            var response = await GetJson<PremierTeamNameResponse>($"/valorant/v1/premier/{teamId}");
            if (response?.data is null)
                return null;

            return string.IsNullOrWhiteSpace(response.data.tag)
                ? response.data.name
                : $"{response.data.name}#{response.data.tag}";
        }

        private static async Task<T> GetJson<T>(string path) where T : class
        {
            try
            {
                Console.WriteLine($"[PREMIER] API Request: {path}");
                var response = await Http.GetAsync(path);
                Console.WriteLine($"[PREMIER] API Response: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[PREMIER] API request failed: {response.StatusCode} for {path}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[PREMIER] Error content: {errorContent}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[PREMIER] Response length: {json.Length} characters");
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PREMIER] API request exception: {ex.Message}");
                return null;
            }
        }

        private static HashSet<string> LoadSeenMatchIds()
        {
            var filePath = GetSeenMatchesFilePath();
            Console.WriteLine($"[PREMIER] Loading seen match IDs from: {filePath}");
            
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[PREMIER] Seen matches file does not exist - starting with empty list");
                return new HashSet<string>();
            }

            var json = File.ReadAllText(filePath);
            var seenIds = JsonConvert.DeserializeObject<HashSet<string>>(json) ?? new HashSet<string>();
            Console.WriteLine($"[PREMIER] Loaded {seenIds.Count} seen match IDs");
            return seenIds;
        }

        private static void SaveSeenMatchIds(HashSet<string> ids)
        {
            var filePath = GetSeenMatchesFilePath();
            File.WriteAllText(filePath, JsonConvert.SerializeObject(ids, Formatting.Indented));
        }
    }
}