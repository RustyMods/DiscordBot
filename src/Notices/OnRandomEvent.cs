using System.Collections.Generic;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace DiscordBot.Notices;

public static class OnRandomEvent
{
    [HarmonyPatch(typeof(RandomEvent), nameof(RandomEvent.OnStart))]
    private static class RandomEvent_OnStart_Patch
    {
        [UsedImplicitly]
        private static void Postfix(RandomEvent __instance)
        {
            if (!DiscordBotPlugin.ShowEvent || !ZNet.instance.IsServer() || !__instance.m_firstActivation || string.IsNullOrWhiteSpace(__instance.m_startMessage)) return;

            var details = new Dictionary<string, string>()
            {
                ["Position"] = $"{__instance.m_pos.x:0.0}, {__instance.m_pos.y:0.0}, {__instance.m_pos.z:0.0}",
                ["Biome"] = WorldGenerator.instance.GetBiome(__instance.m_pos).ToString()
            };
            List<string> creatures = new();
            for (var index = 0; index < __instance.m_spawn.Count; ++index)
            {
                var spawn = __instance.m_spawn[index];
                if (!spawn.m_prefab.TryGetComponent(out Character character)) continue;
                creatures.Add(character.m_name);
            }
            details["Creatures"] = string.Join(", ", creatures);

            Discord.instance?.SendEvent(Webhook.Notifications, DiscordBotPlugin.OnEventHooks, __instance.m_startMessage, Color.yellow, details);
        }
    }
    
    [HarmonyPatch(typeof(RandomEvent), nameof(RandomEvent.OnStop))]
    private static class RandomEvent_OnStop_Patch
    {
        [UsedImplicitly]
        private static void Postfix(RandomEvent __instance)
        {
            if (!DiscordBotPlugin.ShowEvent || !ZNet.instance.IsServer() || string.IsNullOrWhiteSpace(__instance.m_endMessage)) return;
            
            Discord.instance?.SendEvent(Webhook.Notifications, DiscordBotPlugin.OnEventHooks, __instance.m_endMessage, Color.yellow);
        }
    }
}