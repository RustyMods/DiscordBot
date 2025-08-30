using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using static DiscordBot.DiscordBotPlugin;

namespace DiscordBot.Notices;

public static class Server
{
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Save))]
    private static class ZNet_Save_Patch
    {
        [UsedImplicitly]
        private static void Postfix(ZNet __instance)
        {
            if (m_serverSaveNotice.Value is Toggle.Off) return;
            if (!__instance.IsServer()) return;
            Discord.instance.SendStatus(Webhook.Notifications, "$msg_server_saving", __instance.GetWorldName(), "$status_saving", new Color(0.4f, 0.98f, 0.24f));
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Shutdown))]
    private static class ZNet_Shutdown_Patch
    {
        [UsedImplicitly]
        private static void Prefix(ZNet __instance)
        {
            if (m_serverStopNotice.Value is Toggle.Off) return;
            if (!__instance.IsServer()) return;
            Discord.instance.SendStatus(Webhook.Notifications, "$msg_server_stop", __instance.GetWorldName(), "$status_offline", new Color(1f, 0.2f, 0f, 1f));
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_PeerInfo))]
    private static class ZNet_RPC_PeerInfo_Patch
    {
        [UsedImplicitly]
        private static void Postfix(ZNet __instance, ZRpc rpc)
        {
            if (!__instance.IsServer()) return;
            if (m_loginNotice.Value is Toggle.Off || __instance.GetPeer(rpc) is not { } peer || !__instance.IsConnected(peer.m_uid)) return;
            Discord.instance.SendMessage(Webhook.Notifications, message: $"{peer.m_playerName} $label_has_joined");
        }
    }
    
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_Disconnect))]
    private static class ZNet_RPC_Disconnect_Patch
    {
        [UsedImplicitly]
        private static void Prefix(ZNet __instance, ZRpc rpc)
        {
            if (m_logoutNotice.Value is Toggle.Off) return;
            if (!__instance.IsServer()) return;
            if (__instance.GetPeer(rpc) is not { } peer) return;
            Discord.instance.SendMessage(Webhook.Notifications, message: $"{peer.m_playerName} $label_has_left");
        }
    }
}