using System.Collections.Generic;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace DiscordBot.Notices;

public static class OnLogin
{
    private static bool m_firstSpawn = true;
    
    [HarmonyPatch(typeof(Game), nameof(Game.SpawnPlayer))]
    private static class Player_OnSpawned_Patch
    {
        [UsedImplicitly]
        private static void Postfix(Vector3 spawnPoint, Player __result)
        {
            if (!DiscordBotPlugin.ShowOnLogin || !m_firstSpawn) return;
            m_firstSpawn = false;
            var msg = $"{__result.GetPlayerName()} {Keys.HasJoined}";

            if (DiscordBotPlugin.ShowCoordinates)
            {
                var coordinates = $"{spawnPoint.x:0.0}, {spawnPoint.y:0.0}, {spawnPoint.z:0.0}";
                var biome = WorldGenerator.instance.GetBiome(spawnPoint).ToString();
                var details = new Dictionary<string, string>(){["Coordinates"] = coordinates, ["Biome"] = biome};
                
                Discord.instance?.SendEvent(Webhook.Notifications, DiscordBotPlugin.OnLoginHooks, msg, ColorExtensions.SoftBlue, details);
            }
            else
            {
                Discord.instance?.SendMessage(Webhook.Notifications, message: msg, hooks: DiscordBotPlugin.OnLoginHooks);
            }
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
    private static class Logout_Patch
    {
        [UsedImplicitly]
        private static void Postfix() => m_firstSpawn = true;
    }
}