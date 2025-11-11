using System.Collections.Generic;
using HarmonyLib;
using JetBrains.Annotations;

namespace DiscordBot.Notices;

public static class OnCommand
{
    [HarmonyPatch(typeof(Terminal.ConsoleCommand), nameof(Terminal.ConsoleCommand.RunAction))]
    private static class Terminal_ConsoleCommand_RunAction
    {
        [UsedImplicitly]
        private static void Postfix(Terminal.ConsoleCommand __instance, Terminal.ConsoleEventArgs args)
        {
            if (!DiscordBotPlugin.ShowCommandUse || !__instance.IsCheat || __instance.IsPluginCommand()) return;
            if (Player.m_localPlayer)
            {
                Dictionary<string, string> details = new Dictionary<string, string>()
                {
                    ["Coordinates"] = $"{Player.m_localPlayer.transform.position.x:0.0}, {Player.m_localPlayer.transform.position.y:0.0}, {Player.m_localPlayer.transform.position.z:0.0}",
                    ["Biome"] = Player.m_localPlayer.GetCurrentBiome().ToString(),
                };
                
                Discord.instance?.SendTableEmbed(Webhook.Notifications, $"Executed command: `{string.Join(" ", args.Args)}`", details, Player.m_localPlayer.GetPlayerName(), hooks: DiscordBotPlugin.OnUseCommandHooks);
            }
            else
            {
                Discord.instance?.SendMessage(Webhook.Notifications, ZNet.instance.GetWorldName(), $"Executed command: `{string.Join(" ", args.Args)}`", DiscordBotPlugin.OnUseCommandHooks);
            }
        }
    }
}