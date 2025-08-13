using HarmonyLib;
using static DiscordBot.DiscordBotPlugin;

namespace DiscordBot.Notices;

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
            Discord.instance.SendMessage(Webhook.Notifications, WorldName, $"{EmojiHelper.Emoji("save")} $msg_server_saving");
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
            var description = $"{playerCount} $label_players_connected!";
            var WorldName = ZNet.instance.GetWorldName();
            Discord.instance.SendEmbedMessage(Webhook.Notifications, $"{EmojiHelper.Emoji("donut")} $title_new_connection", description, WorldName, Links.ServerIcon);
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
            Discord.instance.SendMessage(Webhook.Notifications, WorldName, $"{EmojiHelper.Emoji("stop")} $msg_server_stop");
        }
    }
}