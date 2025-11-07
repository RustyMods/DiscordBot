using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace DiscordBot.Notices;

public static class OnShutdown
{
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Shutdown))]
    private static class ZNet_Shutdown_Patch
    {
        [UsedImplicitly]
        private static void Prefix(ZNet __instance)
        {
            if (!DiscordBotPlugin.ShowServerStop || !__instance.IsServer()) return;
            Discord.instance?.SendStatus(Webhook.Notifications, DiscordBotPlugin.OnWorldShutdownHooks, Keys.ServerStop, __instance.GetWorldName(), Keys.Offline, new Color(1f, 0.2f, 0f, 1f));
        }
    }
}