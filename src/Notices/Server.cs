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
            Discord.instance.SendMessage(Webhook.Notifications, WorldName, $"{EmojiHelper.Emoji("save")}  $msg_server_saving");
        }
    }

    [HarmonyPatch(typeof(ZPlayFabMatchmaking), nameof(ZPlayFabMatchmaking.RegisterServer))]
    private static class ZPlayFabMatchmaking_RegisterServer_Patch
    {
        private static void Postfix(ZPlayFabMatchmaking __instance)
        {
            if (m_serverStartNotice.Value is Toggle.Off) return;
            Discord.instance.SendMessage(Webhook.Notifications, ZNet.instance.GetWorldName(), $"$msg_server_start `{__instance.GetServerIPAndPort()}`");
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
            Discord.instance.SendMessage(Webhook.Notifications, WorldName, $"{EmojiHelper.Emoji("stop")}  $msg_server_stop");
        }
    }
}