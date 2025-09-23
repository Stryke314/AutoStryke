using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

public class CompsDataManager
{
    private Dictionary<string, Dictionary<string, List<string>>> _data;

    public CompsDataManager(string jsonFilePath)
    {
        LoadDatabase(jsonFilePath);
    }

    private void LoadDatabase(string jsonFilePath)
    {
        if (!File.Exists(jsonFilePath))
            throw new FileNotFoundException($"JSON file not found: {jsonFilePath}");

        var json = File.ReadAllText(jsonFilePath);
        _data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<string>>>>(json);
    }

    // Returns the comp for a given team on a given map
    public List<string> GetComp(string team, string map)
    {
        if (_data.ContainsKey(map) && _data[map].ContainsKey(team))
        {
            return _data[map][team];
        }

        return new List<string>(); // empty if not found
    }
}
