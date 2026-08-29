public static class AgentProfiles
{
    public record Profile(string Role, HashSet<string> Tags);

    // Tags describe what an agent's kit actually does, independent of role:
    // Flash, Smoke, Recon (reveals enemies), Trap (slows/traps/disrupts),
    // Heal, Mobility, AreaDenial (mollies/damage zones), Shield, Buff
    // (self/team stat boosts, e.g. stim).
    public static readonly Dictionary<string, Profile> Data = new(StringComparer.OrdinalIgnoreCase)
    {
        // Duelists
        ["Jett"] = new("Duelist", new() { "Mobility" }),
        ["Phoenix"] = new("Duelist", new() { "Flash", "AreaDenial", "Heal" }),
        ["Raze"] = new("Duelist", new() { "AreaDenial", "Mobility" }),
        ["Reyna"] = new("Duelist", new() { "Heal", "Flash" }),
        ["Yoru"] = new("Duelist", new() { "Mobility", "Flash" }),
        ["Neon"] = new("Duelist", new() { "Mobility", "Trap" }),
        ["Iso"] = new("Duelist", new() { "Shield" }),
        ["Waylay"] = new("Duelist", new() { "Mobility" }),

        // Initiators
        ["Sova"] = new("Initiator", new() { "Recon", "AreaDenial" }),
        ["Breach"] = new("Initiator", new() { "Flash", "Trap" }),
        ["Skye"] = new("Initiator", new() { "Recon", "Flash", "Heal" }),
        ["Kayo"] = new("Initiator", new() { "Flash", "Recon" }),
        ["Fade"] = new("Initiator", new() { "Recon", "Trap" }),
        ["Gekko"] = new("Initiator", new() { "Flash", "Trap" }),
        ["Tejo"] = new("Initiator", new() { "Recon", "AreaDenial" }),

        // Controllers
        ["Brimstone"] = new("Controller", new() { "Smoke", "AreaDenial" }),
        ["Omen"] = new("Controller", new() { "Smoke", "Mobility", "Flash" }),
        ["Viper"] = new("Controller", new() { "Smoke", "Trap", "AreaDenial" }),
        ["Astra"] = new("Controller", new() { "Smoke", "Trap", "AreaDenial" }),
        ["Harbor"] = new("Controller", new() { "Smoke", "Trap" }),
        ["Clove"] = new("Controller", new() { "Smoke", "Heal" }),
        ["Miks"] = new("Controller", new() { "Smoke", "Heal", "Trap", "Buff" }),

        // Sentinels
        ["Sage"] = new("Sentinel", new() { "Heal", "Trap" }),
        ["Cypher"] = new("Sentinel", new() { "Recon", "Trap" }),
        ["Killjoy"] = new("Sentinel", new() { "Recon", "Trap", "AreaDenial" }),
        ["Chamber"] = new("Sentinel", new() { "Recon", "Mobility" }),
        ["Deadlock"] = new("Sentinel", new() { "Trap", "Recon" }),
        ["Vyse"] = new("Sentinel", new() { "Trap", "AreaDenial" }),
        ["Veto"] = new("Sentinel", new() { "Trap", "Recon", "Mobility" }),

       
    };

    /// <summary>0.0-1.0 similarity between two agents. Same agent always returns 1.0.</summary>
    public static double Similarity(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            return 1.0;

        if (!Data.TryGetValue(a, out var pa) || !Data.TryGetValue(b, out var pb))
            return 0.0;

        double roleScore = (pa.Role == pb.Role && pa.Role != "Unknown") ? 1.0 : 0.0;

        double tagScore;
        if (pa.Tags.Count == 0 || pb.Tags.Count == 0)
        {
            tagScore = 0.0;
        }
        else
        {
            int intersection = pa.Tags.Intersect(pb.Tags).Count();
            int union = pa.Tags.Union(pb.Tags).Count();
            tagScore = union == 0 ? 0.0 : (double)intersection / union;
        }

        return 0.5 * roleScore + 0.5 * tagScore;
    }
}

public static class CompSimilarity
{
    /// <summary>
    /// 0.0-1.0 similarity between two 5-agent compositions, order-independent.
    /// Tries every pairing between the two comps (5! = 120, trivial) and keeps
    /// the best-scoring one, since agent order within a comp is meaningless.
    /// </summary>
    public static double Compute(string[] queryAgents, string[] candidateAgents)
    {
        if (queryAgents.Length != 5 || candidateAgents.Length != 5)
            return 0.0;

        double best = double.MinValue;

        foreach (var permutation in Permute(candidateAgents))
        {
            double sum = 0;
            for (int i = 0; i < 5; i++)
                sum += AgentProfiles.Similarity(queryAgents[i], permutation[i]);

            if (sum > best)
                best = sum;
        }

        return best / 5.0;
    }

    private static IEnumerable<string[]> Permute(string[] items)
    {
        if (items.Length <= 1)
        {
            yield return items;
            yield break;
        }

        for (int i = 0; i < items.Length; i++)
        {
            var rest = items.Where((_, index) => index != i).ToArray();
            foreach (var permutation in Permute(rest))
            {
                var result = new string[items.Length];
                result[0] = items[i];
                Array.Copy(permutation, 0, result, 1, permutation.Length);
                yield return result;
            }
        }
    }
}