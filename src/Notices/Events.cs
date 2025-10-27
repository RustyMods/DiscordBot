using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace DiscordBot.Notices;

public static class Events
{
    [HarmonyPatch(typeof(RandomEvent), nameof(RandomEvent.OnStart))]
    private static class RandomEvent_OnStart_Patch
    {
        [UsedImplicitly]
        private static void Postfix(RandomEvent __instance)
        {
            if (!DiscordBotPlugin.ShowEvent || !ZNet.instance.IsServer() || !__instance.m_firstActivation || string.IsNullOrWhiteSpace(__instance.m_startMessage)) return;
            Discord.instance?.SendMessage(Webhook.Notifications, message: $"{__instance.m_startMessage}", hooks: DiscordBotPlugin.OnEventHooks);
        }
    }
    
    [HarmonyPatch(typeof(RandomEvent), nameof(RandomEvent.OnStop))]
    private static class RandomEvent_OnStop_Patch
    {
        [UsedImplicitly]
        private static void Postfix(RandomEvent __instance)
        {
            if (!DiscordBotPlugin.ShowEvent || !ZNet.instance.IsServer() || string.IsNullOrWhiteSpace(__instance.m_endMessage)) return;
            Discord.instance?.SendMessage(Webhook.Notifications, message: $"{__instance.m_endMessage}", hooks: DiscordBotPlugin.OnEventHooks);
        }
    }
}