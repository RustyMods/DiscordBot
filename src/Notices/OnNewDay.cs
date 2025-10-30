using HarmonyLib;
using JetBrains.Annotations;

namespace DiscordBot.Notices;

public static class OnNewDay
{
    [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.OnMorning))]
    private static class EnvMan_OnMorning_Patch
    {
        [UsedImplicitly]
        private static void Postfix(EnvMan __instance)
        {
            if (!DiscordBotPlugin.ShowNewDay || !ZNet.instance || !ZNet.instance.IsServer()) return;
            int day = __instance.GetCurrentDay();
            string msg = $"{Keys.Day} {day}";
            Discord.instance?.SendMessage(Webhook.Notifications, message: msg, hooks: DiscordBotPlugin.OnNewDayHooks);
        }
    }
}