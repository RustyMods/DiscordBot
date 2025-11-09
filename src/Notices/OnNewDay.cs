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
                string msg = DayQuips.GenerateNewDayQuip(day);

                if (DiscordBotPlugin.ImproveDayQuips && ChatAI.HasKey() && ChatAI.instance != null)
                {
                    string prompt = "You are a witty, sarcastic Viking spirit in Valheim" +
                                    "A new day has arrive, and the original message is: " + msg +
                                    ". Reimagine thsi quip to make it fresh, humorous and entertaining, keep it 1-2 sentences.";
                    ChatAI.instance.OnDayQuip += delayedMsg;
                    ChatAI.instance.Ask(prompt, false, true);
                    
                    void delayedMsg(string message)
                    {
                        msg = message;
                        Discord.instance?.SendMessage(Webhook.Notifications, message: msg, hooks: DiscordBotPlugin.OnNewDayHooks);
                        ChatAI.instance!.OnDayQuip -= delayedMsg;
                        Discord.instance?.BroadcastMessage(ZNet.instance.GetWorldName(), msg, false);
                    }
                }
                else
                {
                    Discord.instance?.SendMessage(Webhook.Notifications, message: msg, hooks: DiscordBotPlugin.OnNewDayHooks);
                    Discord.instance?.BroadcastMessage(ZNet.instance.GetWorldName(), msg, false);
                }
            }
        }
    }
}