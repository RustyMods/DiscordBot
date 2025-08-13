using HarmonyLib;

namespace DiscordBot.Trackers;

public static class OnDeath
{
    [HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
    private static class Player_OnDeath_Patch
    {
        private static void Prefix(Player __instance)
        {
            if (DiscordBotPlugin.m_deathNotice.Value is DiscordBotPlugin.Toggle.Off) return;
            
            if (__instance != Player.m_localPlayer) return;
            if (__instance.m_nview.GetZDO() == null) return;
            if (__instance.m_nview.GetZDO().GetBool(ZDOVars.s_dead)) return;
            string killedBy = "Unknown";
            string avatar = "";
            if (__instance.m_lastHit is { } lastHit && lastHit.GetAttacker() is { } killer)
            {
                killedBy = Localization.instance.Localize(killer.m_name) + " level " + killer.m_level;
                avatar = Links.CreatureLinks.TryGetValue(killer.name.Replace("(Clone)", string.Empty),
                    out string url)
                    ? url
                    : "";
            }
            else if (__instance.m_lastHit is {} hit)
            {
                string FormatCamelOrPascal(string input)
                {
                    if (string.IsNullOrWhiteSpace(input))
                        return string.Empty;

                    // Insert spaces before uppercase letters (except first)
                    string spaced = System.Text.RegularExpressions.Regex.Replace(
                        input,
                        "(?<!^)([A-Z])",
                        " $1"
                    );

                    // Lowercase everything except the first letter
                    return char.ToUpper(spaced[0]) + spaced.Substring(1).ToLower();
                }

                killedBy = FormatCamelOrPascal(hit.m_hitType.ToString()).ToLower();
            }
            Discord.instance.SendEmbedMessage(DiscordBotPlugin.m_notificationWebhookURL.Value, __instance.GetPlayerName() + " has died!", $"Killed by {killedBy}", "Valheim Bot", avatar);
            
        }
    }
}