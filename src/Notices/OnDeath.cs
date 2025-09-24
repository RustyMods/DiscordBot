using HarmonyLib;
using JetBrains.Annotations;

namespace DiscordBot.Notices;

public static class OnDeath
{
    // [HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
    // private static class Player_OnDeath_Patch
    // {
    //     [UsedImplicitly]
    //     private static void Prefix(Player __instance)
    //     {
    //         if (!DiscordBotPlugin.ShowOnDeath) return;
    //         
    //         if (__instance != Player.m_localPlayer) return;
    //         if (__instance.m_nview.GetZDO() == null) return;
    //         // Prevent this from running multiple times.
    //         // The first time OnDeath is called, we set ZDO variable `s_dead` to true.
    //         // This ensures that this patch only triggers once per instance, on the first death call.
    //         // Since Player isn't destroyed instantly
    //         string avatar = "";
    //         string quip = "";
    //         if (__instance.m_lastHit is { } hit)
    //         {
    //             if (hit.GetAttacker() is { } killer)
    //             {
    //                 avatar = Links.GetCreatureIcon(killer.name);
    //                 quip = DeathQuips.GenerateDeathQuip(__instance.GetPlayerName(), killer.m_name, killer.m_level, killer.IsBoss());
    //             }
    //             else
    //             {
    //                 quip = DeathQuips.GenerateEnvironmentalQuip(__instance.GetPlayerName(), hit.m_hitType);
    //             }
    //         }
    //         Discord.instance?.SendEmbedMessage(Webhook.DeathFeed, 
    //             $"{__instance.GetPlayerName()} {Keys.HasDied}",
    //             quip, 
    //             thumbnail: avatar);
    //     }
    // }
    
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
            
            if (DiscordBotPlugin.ScreenshotDeath) DeathRecorder.instance?.CaptureFrame($"{__instance.GetPlayerName()} {Keys.HasDied}", quip, avatar);
            else Discord.instance?.SendEmbedMessage(Webhook.DeathFeed, $"{__instance.GetPlayerName()} {Keys.HasDied}", quip, thumbnail: avatar);
        }
    }
}