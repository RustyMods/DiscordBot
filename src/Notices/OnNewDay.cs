using HarmonyLib;
using JetBrains.Annotations;

namespace DiscordBot.Notices;

public static class OnNewDay
{
    [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.UpdateTriggers))]
    private static class EnvMan_UpdateTriggers_Patch
    {
        [UsedImplicitly]
        private static void Postfix(EnvMan __instance, float oldDayFraction, float newDayFraction, float dt)
        {
            if (!DiscordBotPlugin.ShowNewDay || !(ZNet.instance?.IsServer() ?? false)) return;
            if (oldDayFraction > 0.20000000298023224 && oldDayFraction < 0.25 && newDayFraction > 0.25 && newDayFraction < 0.30000001192092896)
            {
                int day = __instance.GetCurrentDay();
                string msg = $"{Keys.Day} {day}";
                Discord.instance?.SendMessage(Webhook.Notifications, message: msg, hooks: DiscordBotPlugin.OnNewDayHooks);
            }
        }
    }
}