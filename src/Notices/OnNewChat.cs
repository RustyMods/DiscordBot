using HarmonyLib;
using JetBrains.Annotations;

namespace DiscordBot.Notices;

public static class OnNewChat
{
    [HarmonyPatch(typeof(Chat), nameof(Chat.SendText))]
    private static class Chat_SendText_Patch
    {
        [UsedImplicitly]
        private static void Postfix(Talker.Type type, string text)
        {
            if (!DiscordBotPlugin.ShowChat || type is not Talker.Type.Shout || string.IsNullOrEmpty(text) || text == Localization.instance.Localize("$text_player_arrived")) return;
            switch (DiscordBotPlugin.ChatType)
            {
                case ChatDisplay.Player:
                    Discord.instance?.SendMessage(Webhook.Chat, (Player.m_localPlayer?.GetPlayerName() ?? ZNet.instance.GetWorldName()) + $" ({Keys.InGame})", text);
                    break;
                case ChatDisplay.Bot:
                    Discord.instance?.SendMessage(Webhook.Chat, message: $"{Player.m_localPlayer?.GetPlayerName() ?? ZNet.instance.GetWorldName()} {Keys.Shouts} {text.Format(TextFormat.Bold)}");
                    break;
            }
        }
    }
}