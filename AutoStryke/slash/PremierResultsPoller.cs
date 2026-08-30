using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

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
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(teamName))
                return 0; // Premier polling not configured - skip quietly.

            Http.DefaultRequestHeaders.Authorization = null;
            Http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", apiKey);

            var teamResponse = await GetJson<PremierTeamResponse>(
                $"/valorant/v1/premier/{Uri.EscapeDataString(teamName)}/{Uri.EscapeDataString(teamTag)}");

            var teamId = teamResponse?.data?.id;
            if (string.IsNullOrWhiteSpace(teamId))
                return 0;

            var history = await GetJson<PremierHistoryResponse>(
                $"/valorant/v1/premier/{teamId}/history");

            var matches = history?.data?.league_matches ?? new List<PremierLeagueMatch>();
            var seenIds = LoadSeenMatchIds();
            var newMatches = matches.Where(m => !seenIds.Contains(m.id)).ToList();

            if (newMatches.Count == 0)
                return 0;

            var results = Program.LoadMatchResults();
            int added = 0;

            foreach (var match in newMatches)
            {
                var detail = await GetJson<MatchDetailResponse>(
                    $"/valorant/v4/match/{region}/pc/{match.id}");

                if (detail?.data is null)
                {
                    // Couldn't fetch detail (maybe not ready yet) - mark as seen
                    // anyway using the points delta so it isn't retried forever,
                    // but skip adding a detailed result for it.
                    seenIds.Add(match.id);
                    continue;
                }

                var ourTeam = detail.data.teams?.FirstOrDefault(t => t.team_id == teamId);
                var theirTeam = detail.data.teams?.FirstOrDefault(t => t.team_id != teamId);

                if (ourTeam is null || theirTeam is null)
                {
                    seenIds.Add(match.id);
                    continue;
                }

                var opponentName = await ResolveTeamName(theirTeam.team_id) ?? "Premier opponent";

                results.Add(new Program.MatchResult
                {
                    Opponent = opponentName,
                    Map = detail.data.metadata?.map ?? "Unknown",
                    OurScore = ourTeam.rounds_won,
                    TheirScore = theirTeam.rounds_won,
                    Date = match.started_at,
                });

                seenIds.Add(match.id);
                added++;
            }

            if (added > 0)
                Program.SaveMatchResults(results);

            SaveSeenMatchIds(seenIds);
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
                var response = await Http.GetAsync(path);
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return null;
            }
        }

        private static HashSet<string> LoadSeenMatchIds()
        {
            if (!File.Exists(SeenMatchesFile))
                return new HashSet<string>();

            var json = File.ReadAllText(SeenMatchesFile);
            return JsonConvert.DeserializeObject<HashSet<string>>(json) ?? new HashSet<string>();
        }

        private static void SaveSeenMatchIds(HashSet<string> ids)
        {
            File.WriteAllText(SeenMatchesFile, JsonConvert.SerializeObject(ids, Formatting.Indented));
        }
    }
}