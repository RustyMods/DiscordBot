using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using static DiscordBot.DiscordBotPlugin;

namespace DiscordBot;

public class Discord : MonoBehaviour
{
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    private static class ZNet_Awake_Patch
    {
        private static void Postfix() => m_instance.gameObject.AddComponent<Discord>();
    }
    
    public static Discord instance = null!;
    
    public event Action<Message>? OnMessageReceived;
    public event Action<Message>? OnCommandReceived;
    public event Action<string>? OnError;
    
    private string m_lastMessageID = "";
    private string m_lastCommandID = "";
    private bool m_isPollingChatter;
    private bool m_isPollingCommands;

    private DateTime m_timeLoaded;

    public void Awake()
    {
        instance = this;
        m_timeLoaded = DateTime.UtcNow;
        if (!ZNet.m_isServer) return;
        // Only server manages polling
        OnMessageReceived += HandleChatMessage;
        OnCommandReceived += HandleCommands;
        OnError += HandleError;
    }

    private void Start()
    {
        if (!ZNet.instance || !ZNet.m_isServer) return;
        if (string.IsNullOrEmpty(Token.BOT_TOKEN))
        {
            DiscordBotLogger.LogError("Bot token not set, contact Rusty on OdinPlus discord");
            return;
        }
        if (!string.IsNullOrEmpty(m_chatChannelID.Value) && m_chatEnabled.Value is Toggle.On) StartPollingChatter();
        if (!string.IsNullOrEmpty(m_commandChannelID.Value)) StartPollingCommands();
        
        if (m_serverStartNotice.Value is Toggle.On) SendEmbedMessage(Webhook.Notifications, "$msg_server_start", ZNet.instance.GetServerIP(), ZNet.instance.GetWorldName(), Links.ServerIcon);
        SendMessage(Webhook.Commands, ZNet.instance.GetWorldName(), $"{EmojiHelper.Emoji("question")} type `!help` to find list of available commands");
    }

    private void OnDestroy()
    {
        StopPolling();
    }

    private static void HandleError(string message)
    {
        if (m_logErrors.Value is Toggle.Off) return;
        DiscordBotLogger.LogWarning(message);
    }

    private void HandleChatMessage(Message message)
    {
        BroadcastMessage(message.author.GetDisplayName(), message.content);
        if (Player.m_localPlayer) DisplayChatMessage(message.author.GetDisplayName(), message.content);
    }

    private void HandleCommands(Message message)
    {
        string[] args = message.content.Split(' ');
        string command = args[0].Trim();

        if (!DiscordCommands.m_commands.TryGetValue(command, out DiscordCommands.DiscordCommand discordCommand))
        {
            SendMessage(Webhook.Commands, ZNet.instance.GetWorldName(), "Failed to find command: " + command);
        }
        else if (!discordCommand.IsAllowed(message.author.GetFullUsername()))
        {
            SendMessage(Webhook.Commands, ZNet.instance.GetWorldName(), message.author.GetFullUsername() + " not allowed to use command: " + command);
        }
        else discordCommand.Run(args);
    }
    
    #region RPC Handlers

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
    private static class ZNet_OnNewConnection_Patch
    {
        private static void Postfix(ZNetPeer peer) => peer.m_rpc.Register<string, string>(nameof(RPC_ClientBotMessage),RPC_ClientBotMessage);
    }

    public void BroadcastMessage(string username, string message)
    {
        if (!ZNet.instance || !ZNet.m_isServer) return;
        foreach (var peer in ZNet.instance.GetPeers()) peer.m_rpc.Invoke(nameof(RPC_ClientBotMessage), username, message);
    }

    public static void RPC_ClientBotMessage(ZRpc rpc, string username, string message) => DisplayChatMessage(username, message);
    private static void DisplayChatMessage(string userName, string message)
    {
        string text = $"<color=#{ColorUtility.ToHtmlStringRGB(new Color(0f, 0.5f, 0.5f, 1f))}>[Discord]</color><color=orange>{userName}</color>: {message}";
        Chat.instance.AddString(text);
    }
    #endregion
    
    #region Sending Messages to Discord
    
    public void SendMessage(Webhook webhook, string username, string message)
    {
        var webhookData = new DiscordWebhookData
        {
            content = Localization.instance.Localize(message),
            username = username,
            avatar_url = Links.DefaultAvatar
        };
        
        StartCoroutine(SendWebhookMessage(webhookData, GetWebhookURL(webhook)));
    }

    public void SendEmbedMessage(Webhook webhook, string title, string content, string username, string thumbnail = "")
    {
        var embed = new Embed
        {
            title = Localization.instance.Localize(title),
            description = Localization.instance.Localize(content),
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };
        if (!string.IsNullOrEmpty(thumbnail))
        {
            embed.thumbnail = new EmbedImage() { url = thumbnail, width = 256, height = 256 };
        }
        var webhookData = new DiscordWebhookData
        {
            username = username.IsNullOrWhiteSpace() ? ZNet.instance.GetWorldName() : username,
            avatar_url = Links.DefaultAvatar,
            embeds = new Embed[] {embed}
        };
        
        StartCoroutine(SendWebhookMessage(webhookData, GetWebhookURL(webhook)));
    }
    
    public void SendTableEmbed(Webhook webhook, string title, Dictionary<string, string> tableData, string username, string thumbnail = "")
    {
        if (tableData.Count <= 0)
        {
            OnError?.Invoke("Table data is empty");
        }
        var fields = new List<EmbedField>();

        foreach (var kvp in tableData)
        {
            fields.Add(new EmbedField
            {
                name = kvp.Key,
                value = kvp.Value,
                inline = true
            });
        }

        var embed = new Embed
        {
            title = title,
            fields = fields.ToArray(),
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };

        if (!string.IsNullOrEmpty(thumbnail))
        {
            embed.thumbnail = new EmbedImage
            {
                url = thumbnail,
                width = 256,
                height = 256
            };
        }

        var webhookData = new DiscordWebhookData
        {
            username = string.IsNullOrWhiteSpace(username) ? ZNet.instance.GetWorldName() : username,
            avatar_url = Links.DefaultAvatar,
            embeds = new Embed[] { embed }
        };

        StartCoroutine(SendWebhookMessage(webhookData, GetWebhookURL(webhook)));
    }

    
    private IEnumerator SendWebhookMessage(DiscordWebhookData data, string webhookURL)
    {
        if (string.IsNullOrEmpty(webhookURL))
        {
            OnError?.Invoke("Webhook URL is not set!");
            yield break;
        }
        
        string jsonData = JsonConvert.SerializeObject(data);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        using UnityWebRequest request = new UnityWebRequest(webhookURL, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
            
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            string error = $"Failed to send message: {request.error} - {request.downloadHandler.text}";
            OnError?.Invoke(error);
        }
    }
    
    #endregion
    
    #region Reading Messages from Discord
    
    public void StartPollingChatter()
    {
        if (m_isPollingChatter || string.IsNullOrEmpty(Token.BOT_TOKEN) || string.IsNullOrEmpty(m_chatChannelID.Value)) return;
        m_isPollingChatter = true;
        StartCoroutine(PollForMessages());
    }

    public void StartPollingCommands()
    {
        if (!m_isPollingCommands && !string.IsNullOrEmpty(Token.BOT_TOKEN) &&
            !string.IsNullOrEmpty(m_commandChannelID.Value))
        {
            m_isPollingCommands = true;
            StartCoroutine(PollForCommands());
        }
    }

    public void StopPolling()
    {
        m_isPollingChatter = false;
        m_isPollingCommands = false;
    }
    
    private IEnumerator PollForMessages()
    {
        while (m_isPollingChatter)
        {
            yield return StartCoroutine(GetChannelMessages());
            yield return new WaitForSeconds(m_pollInterval.Value);
        }
    }

    private IEnumerator PollForCommands()
    {
        while (m_isPollingCommands)
        {
            yield return StartCoroutine(GetChannelCommands());
            yield return new WaitForSeconds(m_pollInterval.Value);
        }
    }

    private IEnumerator GetChannelCommands()
    {
        string url = $"https://discord.com/api/v10/channels/{GetChannelID(Channel.Commands)}/messages?limit=1";

        using UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("Authorization", $"Bot {Token.BOT_TOKEN}");
        request.SetRequestHeader("Content-Type", "application/json");
        
        yield return request.SendWebRequest();
        
        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                var messages = JsonConvert.DeserializeObject<Message[]>(request.downloadHandler.text);
                if (messages is not { Length: > 0 }) yield break;
                var message = messages[0];
                if (message.author.bot) yield break;

                var timeStamp = DateTime.Parse(message.timestamp).ToUniversalTime(); // local time, so convert
                if (m_timeLoaded > timeStamp)
                {
                    yield break;
                }

                if (message.id == m_lastCommandID) yield break;
                m_lastCommandID = message.id;

                OnCommandReceived?.Invoke(message);
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Failed to parse message: {e.Message}");
            }
        }
        else
        {
            OnError?.Invoke($"Failed to get message: {request.error}");
        }
    }

    private IEnumerator GetChannelMessages()
    {
        string url = $"https://discord.com/api/v10/channels/{GetChannelID(Channel.Chat)}/messages?limit=10";
        
        if (!string.IsNullOrEmpty(m_lastMessageID))
        {
            url += $"&after={m_lastMessageID}";
        }

        using UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("Authorization", $"Bot {Token.BOT_TOKEN}");
        request.SetRequestHeader("Content-Type", "application/json");
            
        yield return request.SendWebRequest();
            
        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                var messages = JsonConvert.DeserializeObject<Message[]>(request.downloadHandler.text);
                if (messages != null)
                {
                    for (int i = messages.Length - 1; i >= 0; i--)
                    {
                        var message = messages[i];
                        if (message.author.bot) continue;
                        if (string.IsNullOrEmpty(m_lastMessageID) || 
                            ulong.Parse(message.id) > ulong.Parse(m_lastMessageID))
                        {
                            m_lastMessageID = message.id;
                        }
                        if (!string.IsNullOrEmpty(m_lastMessageID))
                        {
                            OnMessageReceived?.Invoke(message);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Failed to parse messages: {e.Message}");
            }
        }
        else
        {
            OnError?.Invoke($"Failed to get messages: {request.error}");
        }
    }
    
    #endregion
    
    #region Utility Methods
    
    [Description("Color utility to format into int for discord")]
    private int ColorToInt(Color color)
    {
        int r = Mathf.RoundToInt(color.r * 255);
        int g = Mathf.RoundToInt(color.g * 255);
        int b = Mathf.RoundToInt(color.b * 255);
        return (r << 16) + (g << 8) + b;
    }
    
    #endregion
    
    #region Discord Data Structures

    [Serializable][Description("Discord webhook json object")]
    public class DiscordWebhookData
    {
        public string content = null!;
        public string username = null!;
        public string avatar_url = null!;
        public Embed[] embeds = null!;
    }

    [Serializable][Description("Discord embed json object")]
    public class Embed
    {
        public string title = null!;
        public string description = null!;
        public int color;
        public string timestamp = null!;
        public EmbedAuthor author = null!;
        public EmbedField[] fields = null!;
        public Footer footer = null!;
        public EmbedImage image = null!;
        public EmbedImage thumbnail = null!;
    }

    [Serializable][Description("Discord image json object")]
    public class EmbedImage
    {
        public string url = null!;
        public int width;
        public int height;
    }

    [Serializable][Description("Discord author json object")]
    public class EmbedAuthor
    {
        public string name = null!;
        public string icon_url = null!;
    }

    [Serializable][Description("Discord embed json object")]
    public class EmbedField
    {
        public string name = null!;
        public string value = null!;
        public bool inline;
    }

    [Serializable][Description("Discord footer json object")]
    public class Footer
    {
        public string text = null!;
        public string icon_url = null!;
    }

    [Serializable][Description("Discord message json object")]
    public class Message
    {
        public string id = null!;
        public string channel_id = null!;
        public User author = null!;
        public string content = null!;
        public string timestamp = null!;
        public string edited_timestamp = null!;
        public bool tts;
        public bool mention_everyone;
        public User[] mentions = null!;
        public User[] mention_roles = null!;
        
        public int type;
        public Embed[] embeds = null!;
    }

    [Serializable][Description("Discord user json object")]
    public class User
    {
        public string id = null!;
        public string username = null!;
        public string global_name = null!;
        public string discriminator = null!;
        public bool bot;
        public string avatar = null!;
        public string GetDisplayName() => !string.IsNullOrEmpty(global_name) ? global_name : username;
    
        public string GetFullUsername()
        {
            if (!string.IsNullOrEmpty(discriminator) && discriminator != "0")
            {
                return $"{username}#{discriminator}";
            }
            return username;
        }
    }

    #endregion
}

