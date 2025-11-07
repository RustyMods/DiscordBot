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
            Discord.instance?.SendMessage(Webhook.Notifications, Player.m_localPlayer?.GetPlayerName() ?? "Unknown", $"Executed command: `{string.Join(" ", args.Args)}`");
        }
    }
}