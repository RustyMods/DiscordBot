using HarmonyLib;
using JetBrains.Annotations;
using Splatform;

namespace DiscordBot.Notices;

public static class OnNewChat
{
    [HarmonyPatch(typeof(Chat), nameof(Chat.OnNewChatMessage))]
    private static class Chat_OnNewChatMessage_Patch
    {
        [UsedImplicitly]
        private static void Postfix(Talker.Type type, UserInfo sender, string text)
        {
            if (!DiscordBotPlugin.ShowChat) return;
            // make sure only triggered by local user, not any incoming messages from other players
            if (PlatformManager.DistributionPlatform.LocalUser.PlatformUserID != sender.UserId) return;
            if (type is not Talker.Type.Shout) return;
            if (text == Localization.instance.Localize("$text_player_arrived")) return;
            switch (DiscordBotPlugin.ChatType)
            {
                case ChatDisplay.Player:
                    Discord.instance.SendMessage(Webhook.Chat, sender.GetDisplayName() + $" ({Keys.InGame})", text);
                    break;
                case ChatDisplay.Bot:
                    Discord.instance.SendMessage(Webhook.Chat, 
                        message: $"{sender.GetDisplayName()} {Keys.Shouts} {text.Format(TextFormat.Bold)}");
                    break;
            }
        }
    }
}