using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;

namespace DiscordBot.Notices;

public static class Server
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

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Shutdown))]
    private static class ZNet_Shutdown_Patch
    {
        [UsedImplicitly]
        private static void Prefix(ZNet __instance)
        {
            if (!DiscordBotPlugin.ShowServerStop || !__instance.IsServer()) return;
            Discord.instance?.SendStatus(Webhook.Notifications, DiscordBotPlugin.OnWorldShutdownHooks, Keys.ServerStop, __instance.GetWorldName(), Keys.Offline, new Color(1f, 0.2f, 0f, 1f));
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_PeerInfo))]
    private static class ZNet_RPC_PeerInfo_Patch
    {
        [UsedImplicitly]
        private static void Postfix(ZNet __instance, ZRpc rpc)
        {
            if (!DiscordBotPlugin.ShowOnLogin || !__instance.IsServer() || __instance.GetPeer(rpc) is not { } peer || !__instance.IsConnected(peer.m_uid) || string.IsNullOrWhiteSpace(peer.m_playerName)) return;
            Discord.instance?.SendMessage(Webhook.Notifications, message: $"{peer.m_playerName} {Keys.HasJoined}", hooks: DiscordBotPlugin.OnLoginHooks);
        }
    }
    
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
    private static class ZNet_Disconnect_Patch
    {
        [UsedImplicitly]
        private static void Prefix(ZNet __instance, ZNetPeer peer)
        {
            if (!DiscordBotPlugin.ShowOnLogout || !__instance.IsServer() || string.IsNullOrWhiteSpace(peer.m_playerName)) return;
            Discord.instance?.SendMessage(Webhook.Notifications, message: $"{peer.m_playerName} {Keys.HasLeft}", hooks: DiscordBotPlugin.OnLogoutHooks);
        }
    }
}