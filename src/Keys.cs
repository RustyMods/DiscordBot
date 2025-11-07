using System;
using System.Collections.Generic;

namespace DiscordBot;
public static class Keys
{
    private static readonly Dictionary<string, string> keys = new();

    public static void Write()
    {
        List<string> lines = new();
        lines.Add("{");
        
        var keysList = new List<KeyValuePair<string, string>>(keys);
        foreach (HitData.HitType hitType in Enum.GetValues(typeof(HitData.HitType)))
        {
            string key = $"hittype_{hitType.ToString().ToLower()}";
            string value = Format(hitType.ToString());
            lines.Add($"    \"{key}\": \"{value}\",");
        }
        for (int i = 0; i < keysList.Count; i++)
        {
            var kvp = keysList[i];
            var comma = i == keysList.Count - 1 ? "" : ",";
            lines.Add($"    \"{kvp.Key}\": \"{kvp.Value}\"{comma}");
        }
        
        lines.Add("}");
        DiscordBotPlugin.directory.WriteAllLines("DiscordBot.English.json", lines);
        
        string Format(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Insert spaces before uppercase letters (except the first)
            string spaced = System.Text.RegularExpressions.Regex.Replace(
                input,
                "(?<!^)([A-Z])",
                " $1"
            );

            // Make everything lowercase
            return spaced.ToLower();
        }
    }

    private class Key
    {
        public readonly string key;

        public Key(string key, string value)
        {
            this.key = key;
            keys[key.Replace("$", string.Empty)] = value;
        }
    }

    public static readonly string HasDied = new Key("$msg_hasdied", "has died!").key;
    public static readonly string KilledBy = new Key("$msg_killedby", "killed by").key;
    public static readonly string ServerSaving = new Key("$msg_serversaving", "server is saving!").key;
    public static readonly string ServerStop = new Key("$msg_serverstop", "server is shutting down!").key;
    public static readonly string ServerStart = new Key("$msg_serverstart", "server is booting up!").key;
    public static readonly string Launching = new Key("$msg_lauching", "Launching").key;
    public static readonly string Saving = new Key("$msg_saving", "Saving").key;
    public static readonly string Shouts = new Key("$msg_shout", "shouts").key;
    public static readonly string Offline = new Key("$msg_offline", "Offline").key;
    public static readonly string Level = new Key("$label_level", "level").key;
    public static readonly string Unknown = new Key("$label_unknown", "Unknown").key;
    public static readonly string InGame = new Key("$label_ingame", "in-game").key;
    public static readonly string HasLeft = new Key("$msg_hasleft", "has left!").key;
    public static readonly string HasJoined = new Key("$msg_hasjoined", "has joined!").key;
    public static readonly string WorldName = new Key("$label_worldname", "World name").key;
    public static readonly string Status = new Key("$label_status", "Status").key;
    public static readonly string Day = new Key("$label_day", "Day").key;
}