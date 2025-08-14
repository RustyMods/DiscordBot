using HarmonyLib;
using UnityEngine;
using static DiscordBot.DiscordBotPlugin;

namespace DiscordBot.Notices;

public static class Server
{
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Save))]
    private static class ZNet_Save_Patch
    {
        private static void Postfix(ZNet __instance)
        {
            if (m_serverSaveNotice.Value is Toggle.Off || !Discord.m_worldIsValid) return;
            if (!__instance.IsServer()) return;
            var WorldName = __instance.GetWorldName();
            Discord.instance.SendMessage(Webhook.Notifications, WorldName, $"{EmojiHelper.Emoji("save")}  $msg_server_saving");
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Shutdown))]
    private static class ZNet_Shutdown_Patch
    {
        private static void Prefix(ZNet __instance)
        {
            if (m_serverStopNotice.Value is Toggle.Off || !Discord.m_worldIsValid) return;
            if (!__instance.IsServer()) return;
            var WorldName = ZNet.instance.GetWorldName();
            Discord.instance.SendMessage(Webhook.Notifications, WorldName, $"{EmojiHelper.Emoji("stop")}  $msg_server_stop");
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.OpenServer))]
    private static class ZNet_OpenServer_Patch
    {
        private static void Postfix(ZNet __instance)
        {
            if (!__instance.IsServer() || !Discord.m_worldIsValid) return;
            if (m_serverStartNotice.Value is Toggle.Off) return;
            Discord.instance.SendMessage(Webhook.Notifications, __instance.GetWorldName(), "$msg_server_start");
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_PeerInfo))]
    private static class ZNet_RPC_PeerInfo_Patch
    {
        private static void Postfix(ZNet __instance, ZRpc rpc)
        {
            if (!__instance.IsServer()) return;
            if (m_loginNotice.Value is Toggle.Off || __instance.GetPeer(rpc) is not { } peer || !__instance.IsConnected(peer.m_uid)) return;
            Discord.instance.SendMessage(Webhook.Notifications, __instance.GetWorldName(), $"{peer.m_playerName} $label_has_joined");
        }
    }
    
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_Disconnect))]
    private static class ZNet_RPC_Disconnect_Patch
    {
        private static void Prefix(ZNet __instance, ZRpc rpc)
        {
            if (!__instance.IsServer() || !Discord.m_worldIsValid) return;
            if (__instance.GetPeer(rpc) is not { } peer) return;
            Discord.instance.SendMessage(Webhook.Notifications, ZNet.instance.GetWorldName(), $"{peer.m_playerName} $label_has_left");
        }
    }
}