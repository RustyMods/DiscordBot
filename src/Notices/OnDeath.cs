using HarmonyLib;
using JetBrains.Annotations;

namespace DiscordBot.Notices;

public static class OnDeath
{
    [HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
    private static class Player_OnDeath_Patch
    {
        [UsedImplicitly]
        private static void Prefix(Player __instance)
        {
            if (!DiscordBotPlugin.ShowOnDeath) return;
            if (__instance != Player.m_localPlayer) return;
            if (__instance.m_nview.GetZDO() == null) return;
            string avatar = "";
            string quip = "";
            if (__instance.m_lastHit is { } hit)
            {
                if (hit.GetAttacker() is { } killer)
                {
                    avatar = Links.GetCreatureIcon(killer.name);
                    quip = DeathQuips.GenerateDeathQuip(__instance.GetPlayerName(), killer.m_name, killer.m_level, killer.IsBoss());
                }
                else
                {
                    quip = DeathQuips.GenerateEnvironmentalQuip(__instance.GetPlayerName(), hit.m_hitType);
                }
            }
            
            if (DiscordBotPlugin.ScreenshotGif) Screenshot.instance?.StartRecording($"{__instance.GetPlayerName()} {Keys.HasDied}", quip, avatar);
            else if (DiscordBotPlugin.ScreenshotDeath) Screenshot.instance?.StartCapture($"{__instance.GetPlayerName()} {Keys.HasDied}", quip, avatar);
            else Discord.instance?.SendEmbedMessage(Webhook.DeathFeed, $"{__instance.GetPlayerName()} {Keys.HasDied}", quip, thumbnail: avatar);
        }
    }
}