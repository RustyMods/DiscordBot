using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace DiscordBot.Notices;

public static class OnSave
{
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Save))]
    private static class ZNet_Save_Patch
    {
        [UsedImplicitly]
        private static void Postfix(ZNet __instance)
        {
            if (!DiscordBotPlugin.ShowServerSave || !__instance.IsServer()) return;
            Discord.instance?.SendStatus(Webhook.Notifications, DiscordBotPlugin.OnWorldSaveHooks, Keys.ServerSaving, __instance.GetWorldName(), Keys.Saving, new Color(0.4f, 0.98f, 0.24f));
        }
    }
}