using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using Splatform;
using UnityEngine;

namespace DiscordBot.Notices;

public static class OnNewChat
{
    // [HarmonyPatch(typeof(Chat), nameof(Chat.OnNewChatMessage))]
    // private static class Chat_OnNewChatMessage_Patch
    // {
    //     [UsedImplicitly]
    //     private static void Postfix(Talker.Type type, UserInfo? sender, string text)
    //     {
    //         if (!DiscordBotPlugin.ShowChat || sender is null || string.IsNullOrEmpty(text)) return;
    //         // make sure only triggered by local user, not any incoming messages from other players
    //         if (PlatformManager.DistributionPlatform?.LocalUser.PlatformUserID != sender.UserId) return;
    //         if (type is not Talker.Type.Shout) return;
    //         if (text == Localization.instance.Localize("$text_player_arrived")) return;
    //         switch (DiscordBotPlugin.ChatType)
    //         {
    //             case ChatDisplay.Player:
    //                 Discord.instance?.SendMessage(Webhook.Chat, sender.GetDisplayName() + $" ({Keys.InGame})", text);
    //                 break;
    //             case ChatDisplay.Bot:
    //                 Discord.instance?.SendMessage(Webhook.Chat, message: $"{sender.GetDisplayName()} {Keys.Shouts} {text.Format(TextFormat.Bold)}");
    //                 break;
    //         }
    //     }
    // }

    [HarmonyPatch(typeof(Chat), nameof(Chat.SendText))]
    private static class Chat_SendText_Patch
    {
        [UsedImplicitly]
        private static void Postfix(Talker.Type type, string text)
        {
            if (!DiscordBotPlugin.ShowChat || !Player.m_localPlayer || type is not Talker.Type.Shout || string.IsNullOrEmpty(text) || text == Localization.instance.Localize("$text_player_arrived")) return;
            switch (DiscordBotPlugin.ChatType)
            {
                case ChatDisplay.Player:
                    Discord.instance?.SendMessage(Webhook.Chat, Player.m_localPlayer.GetPlayerName() + $" ({Keys.InGame})", text);
                    break;
                case ChatDisplay.Bot:
                    Discord.instance?.SendMessage(Webhook.Chat, message: $"{Player.m_localPlayer.GetPlayerName()} {Keys.Shouts} {text.Format(TextFormat.Bold)}");
                    break;
            }
        }
    }
}