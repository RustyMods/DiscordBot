using System.Collections.Generic;
using HarmonyLib;
using JetBrains.Annotations;

namespace DiscordBot.Notices;

public static class OnLogout
{
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
    private static class ZNet_Disconnect_Patch
    {
        [UsedImplicitly]
        private static void Prefix(ZNet __instance, ZNetPeer peer)
        {
            if (!DiscordBotPlugin.ShowOnLogout || !__instance.IsServer() || string.IsNullOrWhiteSpace(peer.m_playerName)) return;
            var msg = $"{peer.m_playerName} {Keys.HasLeft}";
    
            if (DiscordBotPlugin.ShowCoordinates)
            {
                var coordinates = $"{peer.m_refPos.x:0.0}, {peer.m_refPos.y:0.0}, {peer.m_refPos.z:0.0}";
                var biome = WorldGenerator.instance.GetBiome(peer.m_refPos).ToString();
                var details = new Dictionary<string, string>(){["Coordinates"] = coordinates, ["Biome"] = biome};
                Discord.instance?.SendEvent(Webhook.Notifications, DiscordBotPlugin.OnLogoutHooks, msg, ColorExtensions.CoolGray, details);
            }
            else
            {
                Discord.instance?.SendMessage(Webhook.Notifications, message: msg, hooks: DiscordBotPlugin.OnLogoutHooks);
            }
        }
    }

    // [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
    // public static class Game_Logout_Patch
    // {
    //     [UsedImplicitly]
    //     private static void Postfix(Game __instance)
    //     {
    //         if (!DiscordBotPlugin.ShowOnLogout) return;
    //         var msg = $"{Player.m_localPlayer.GetPlayerName()} {Keys.HasLeft}";
    //
    //         if (DiscordBotPlugin.ShowCoordinates)
    //         {
    //             var coordinates = $"{Player.m_localPlayer.transform.position.x:0.0}, {Player.m_localPlayer.transform.position.y:0.0}, {Player.m_localPlayer.transform.position.z:0.0}";
    //             var biome = WorldGenerator.instance.GetBiome(Player.m_localPlayer.transform.position).ToString();
    //             var details = new Dictionary<string, string>(){["Coordinates"] = coordinates, ["Biome"] = biome};
    //             Discord.instance?.SendEvent(Webhook.Notifications, DiscordBotPlugin.OnLogoutHooks, msg, ColorExtensions.CoolGray, details);
    //         }
    //         else
    //         {
    //             Discord.instance?.SendMessage(Webhook.Notifications, message: msg, hooks: DiscordBotPlugin.OnLogoutHooks);
    //         }
    //     }
    // }
}