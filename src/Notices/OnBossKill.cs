using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;

namespace DiscordBot.Notices;

public static class OnBossKill
{
    [HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
    private static class Character_OnDeath_Patch
    {
        [UsedImplicitly]
        private static void Prefix(Character __instance)
        {
            if (!DiscordBotPlugin.ShowBossDeath || !__instance.IsBoss()) return;
            string killer = (__instance.m_lastHit?.GetAttacker() as Player)?.GetPlayerName() ?? "Unknown";
            List<Player> players = new();
            Player.GetPlayersInRange(__instance.transform.position, 50f, players);
            Discord.instance?.SendTableEmbed(Webhook.Notifications, $"{__instance.m_name} {Keys.HasDied}", new ()
            {
                ["Last Hit"] = killer,
                ["Players"] = string.Join("\n", players.Select(x => $"`{x.GetPlayerName()}`"))
            }, thumbnail: Links.GetCreatureIcon(__instance.name), 
                hooks: DiscordBotPlugin.OnBossDeathHooks);
        }
    }
}