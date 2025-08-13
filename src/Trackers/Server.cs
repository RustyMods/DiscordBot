using HarmonyLib;
using static DiscordBot.DiscordBotPlugin;

namespace DiscordBot.Trackers;

public static class Server
{
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Save))]
    private static class ZNet_Save_Patch
    {
        private static void Postfix(ZNet __instance)
        {
            if (m_serverSaveNotice.Value is Toggle.Off) return;
            if (!__instance.IsServer()) return;
            var WorldName = __instance.GetWorldName();
            Discord.instance.SendMessage(Webhook.Notifications, WorldName, $"{EmojiHelper.Emoji("save")} Server is saving!");
        }
    }
    
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
    private static class ZNet_OnNewConnection_Patch
    {
        private static void Postfix(ZNet __instance)
        {
            if (m_newPlayerNotice.Value is Toggle.Off) return;
            if (!__instance.IsServer()) return;
            var playerCount = __instance.GetNrOfPlayers();
            var description = $"{playerCount} players connected!";
            var WorldName = ZNet.instance.GetWorldName();
            Discord.instance.SendEmbedMessage(Webhook.Notifications, $"{EmojiHelper.Emoji("donut")} New Connection!", description, WorldName, Links.ServerIcon);
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Shutdown))]
    private static class ZNet_Shutdown_Patch
    {
        private static void Prefix(ZNet __instance)
        {
            if (m_serverStopNotice.Value is Toggle.Off) return;
            if (!__instance.IsServer()) return;
            var WorldName = ZNet.instance.GetWorldName();
            Discord.instance.SendMessage(Webhook.Notifications, WorldName, $"{EmojiHelper.Emoji("stop")} Server is shutting down!");
        }
    }
}