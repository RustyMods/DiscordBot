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
            if (!DiscordBotPlugin.ShowOnDeath || __instance != Player.m_localPlayer || __instance.m_nview.GetZDO() == null) return;

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

            bool isGeneratingQuip = false;
            if (ChatAI.HasKey())
            {
                var prompt =
                    "You are a witty, sarcastic Viking spirit in Valheim" +
                    "A player has just died, and the original quip is: " + quip +
                    ". Reimagine this quip to make it fresh, humorous and entertaining, keep it 1-2 sentences.";
                ChatAI.instance?.Ask(prompt, true);
                isGeneratingQuip = true;
            }
            
            if (DiscordBotPlugin.ScreenshotGif) Recorder.instance?.StartRecording($"{__instance.GetPlayerName()} {Keys.HasDied}", quip, avatar);
            else if (DiscordBotPlugin.ScreenshotDeath) Screenshot.instance?.StartCapture($"{__instance.GetPlayerName()} {Keys.HasDied}", quip, avatar);
            else
            {
                if (isGeneratingQuip && ChatAI.instance)
                {
                    void delayedMessage(string msg)
                    {
                        Discord.instance?.SendEmbedMessage(Webhook.DeathFeed, $"{__instance.GetPlayerName()} {Keys.HasDied}", msg, thumbnail: avatar);
                        if (ChatAI.instance) ChatAI.instance.OnDeathQuip -= delayedMessage;
                    }
                    ChatAI.instance.OnDeathQuip += delayedMessage;
                }
                else Discord.instance?.SendEmbedMessage(Webhook.DeathFeed, $"{__instance.GetPlayerName()} {Keys.HasDied}", quip, thumbnail: avatar);
            }
        }
    }
}