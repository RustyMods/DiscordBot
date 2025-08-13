using HarmonyLib;

namespace DiscordBot.Trackers;

public static class Server
{
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Save))]
    private static class ZNet_Save_Patch
    {
        private static void Postfix(ZNet __instance)
        {
            if (DiscordBotPlugin.m_serverSaveNotice.Value is DiscordBotPlugin.Toggle.Off) return;
            if (!__instance.IsServer()) return;
            var WorldName = __instance.GetWorldName();
            Discord.instance.SendEmbedMessage(DiscordBotPlugin.m_notificationWebhookURL.Value, "Server is saving!", "", WorldName, Links.ServerIcon);
        }
    }
    
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
    private static class ZNet_OnNewConnection_Patch
    {
        private static void Postfix(ZNet __instance)
        {
            if (DiscordBotPlugin.m_newPlayerNotice.Value is DiscordBotPlugin.Toggle.Off) return;
            if (!__instance.IsServer()) return;
            var playerCount = __instance.GetNrOfPlayers();
            var description = $"{playerCount} players connected!";
            var WorldName = ZNet.instance.GetWorldName();
            Discord.instance.SendEmbedMessage(DiscordBotPlugin.m_notificationWebhookURL.Value, "New Connection!", description, WorldName, Links.ServerIcon);
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Shutdown))]
    private static class ZNet_Shutdown_Patch
    {
        private static void Prefix(ZNet __instance)
        {
            if (DiscordBotPlugin.m_serverStopNotice.Value is DiscordBotPlugin.Toggle.Off) return;
            if (!__instance.IsServer()) return;
            var WorldName = ZNet.instance.GetWorldName();
            Discord.instance.SendEmbedMessage(DiscordBotPlugin.m_notificationWebhookURL.Value, "Server is shutting down!", "", WorldName, Links.ServerIcon);
        }
    }
}