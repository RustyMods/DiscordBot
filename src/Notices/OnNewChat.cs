using HarmonyLib;
using Splatform;

namespace DiscordBot.Notices;

public static class OnNewChat
{
    [HarmonyPatch(typeof(Chat), nameof(Chat.OnNewChatMessage))]
    private static class Chat_OnNewChatMessage_Patch
    {
        private static void Postfix(Talker.Type type, UserInfo sender, string text)
        {
            if (DiscordBotPlugin.m_chatEnabled.Value is DiscordBotPlugin.Toggle.Off) return;
            // make sure only triggered by local user, not any incoming messages from other players
            if (PlatformManager.DistributionPlatform.LocalUser.PlatformUserID != sender.UserId) return;
            if (type is not Talker.Type.Shout) return;
            if (text == Localization.instance.Localize("$text_player_arrived")) return;
            Discord.instance.SendMessage(DiscordBotPlugin.Webhook.Chat, sender.GetDisplayName(), text);
        }
    }
}