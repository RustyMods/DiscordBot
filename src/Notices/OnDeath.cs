using HarmonyLib;

namespace DiscordBot.Notices;

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
            // Prevent this from running multiple times.
            // The first time OnDeath is called, we set ZDO variable `s_dead` to true.
            // This ensures that this patch only triggers once per instance, on the first death call.
            // Since Player isn't destroyed instantly
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

                killedBy = Format(hit.m_hitType.ToString());
            }
            Discord.instance.SendEmbedMessage(DiscordBotPlugin.Webhook.Notifications, 
                $"{__instance.GetPlayerName()} $label_has_died!", $"$label_killed_by {killedBy}", 
                thumbnail: avatar);
        }
    }
}