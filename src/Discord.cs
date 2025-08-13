using System;
using System.Collections;
using System.Text;
using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace DiscordBot;

public class Discord : MonoBehaviour
{
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    private static class ZNet_Awake_Patch
    {
        private static void Postfix() => DiscordBotPlugin.m_instance.gameObject.AddComponent<Discord>();
    }
    
    public static Discord instance = null!;
    
    public event Action<Message>? OnMessageReceived;
    public event Action<Message>? OnCommandReceived;
    public event Action<string>? OnMessageSent;
    public event Action<string>? OnError;
    
    private string m_lastMessageID = "";
    private bool m_isPollingChatter;
    private bool m_isPollingCommands;

    private DateTime m_timeLoaded;

    public void Awake()
    {
        instance = this;
        m_timeLoaded = DateTime.UtcNow;
        if (!ZNet.m_isServer) return;
        OnMessageReceived += HandleChatMessage;
        OnCommandReceived += HandleCommands;
        if (DiscordBotPlugin.m_serverStartNotice.Value is DiscordBotPlugin.Toggle.On)
            SendEmbedMessage(DiscordBotPlugin.m_notificationWebhookURL.Value, "Server is booting up!", ZNet.instance.GetServerIP(), ZNet.instance.GetWorldName(), Links.ServerIcon);
    }

    private void Start()
    {
        if (!ZNet.instance || !ZNet.m_isServer) return;
        if (string.IsNullOrEmpty(Token.BOT_TOKEN))
        {
            DiscordBotPlugin.DiscordBotLogger.LogError("Bot token not set, contact Rusty on OdinPlus discord");
            return;
        }
        if (!string.IsNullOrEmpty(DiscordBotPlugin.m_chatChannelID.Value) && DiscordBotPlugin.m_chatEnabled.Value is DiscordBotPlugin.Toggle.On) StartPollingChatter();
        if (!string.IsNullOrEmpty(DiscordBotPlugin.m_commandChannelID.Value)) StartPollingCommands();
    }

    private void OnDestroy()
    {
        StopPolling();
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
            SendMessage(DiscordBotPlugin.m_commandWebhookURL.Value, ZNet.instance.GetWorldName(), "Failed to find command: " + command);
            return;
        }

        if (!discordCommand.IsAllowed(message.author.GetFullUsername()))
        {
            SendMessage(DiscordBotPlugin.m_commandWebhookURL.Value, ZNet.instance.GetWorldName(), message.author.GetFullUsername() + " not allowed to use command: " + command);
            return;
        }
        discordCommand.Run(args);
    }
    
    #region RPC Handlers

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
    private static class ZNet_OnNewConnection_Patch
    {
        private static void Postfix(ZNetPeer peer) => peer.m_rpc.Register<string, string>(nameof(RPC_ClientBotMessage),RPC_ClientBotMessage);
    }

    public void BroadcastMessage(string userName, string message)
    {
        if (!ZNet.instance || !ZNet.m_isServer) return;
        foreach (var peer in ZNet.instance.GetPeers()) peer.m_rpc.Invoke(nameof(RPC_ClientBotMessage), userName, message);
    }

    public static void RPC_ClientBotMessage(ZRpc rpc, string userName, string message) => DisplayChatMessage(userName, message);
    private static void DisplayChatMessage(string userName, string message)
    {
        string text = $"<color=#{ColorUtility.ToHtmlStringRGB(new Color(0f, 0.5f, 0.5f, 1f))}>[Discord]</color><color=orange>{userName}</color>: {message}";
        Chat.instance.AddString(text);
    }
    #endregion
    
    #region Sending Messages to Discord
    
    public void SendMessage(string webhookURL, string userName, string message)
    {
        var webhookData = new DiscordWebhookData
        {
            content = message,
            username = userName,
            avatar_url = Links.DefaultAvatar
        };
        
        StartCoroutine(SendWebhookMessage(webhookData, webhookURL));
    }

    public void SendEmbedMessage(string webhookURL, string title, string description, string userName, string avatarUrl = "")
    {
        var embed = new Embed
        {
            title = title,
            description = description,
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };
        if (!string.IsNullOrEmpty(avatarUrl))
        {
            embed.thumbnail = new EmbedImage() { url = avatarUrl, width = 256, height = 256 };
        }
        var webhookData = new DiscordWebhookData
        {
            username = userName.IsNullOrWhiteSpace() ? ZNet.instance.GetWorldName() : userName,
            avatar_url = Links.DefaultAvatar,
            embeds = new Embed[] {embed}
        };
        
        StartCoroutine(SendWebhookMessage(webhookData, webhookURL));
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
            
        if (request.result == UnityWebRequest.Result.Success)
        {
            OnMessageSent?.Invoke(data.content ?? "Embed message sent");
        }
        else
        {
            string error = $"Failed to send message: {request.error} - {request.downloadHandler.text}";
            OnError?.Invoke(error);
        }
    }
    
    #endregion
    
    #region Reading Messages from Discord
    
    public void StartPollingChatter()
    {
        if (m_isPollingChatter || string.IsNullOrEmpty(Token.BOT_TOKEN) || string.IsNullOrEmpty(DiscordBotPlugin.m_chatChannelID.Value)) return;
        m_isPollingChatter = true;
        StartCoroutine(PollForMessages());
    }

    public void StartPollingCommands()
    {
        if (!m_isPollingCommands && !string.IsNullOrEmpty(Token.BOT_TOKEN) &&
            !string.IsNullOrEmpty(DiscordBotPlugin.m_commandChannelID.Value))
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
            yield return new WaitForSeconds(DiscordBotPlugin.m_pollInterval.Value);
        }
    }

    private IEnumerator PollForCommands()
    {
        while (m_isPollingCommands)
        {
            yield return StartCoroutine(GetChannelCommands());
            yield return new WaitForSeconds(DiscordBotPlugin.m_pollInterval.Value);
        }
    }

    private IEnumerator GetChannelCommands()
    {
        string url = $"https://discord.com/api/v10/channels/{DiscordBotPlugin.m_commandChannelID.Value}/messages?limit=1";

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
        string url = $"https://discord.com/api/v10/channels/{DiscordBotPlugin.m_chatChannelID.Value}/messages?limit=10";
        
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
    
    private int ColorToInt(Color color)
    {
        int r = Mathf.RoundToInt(color.r * 255);
        int g = Mathf.RoundToInt(color.g * 255);
        int b = Mathf.RoundToInt(color.b * 255);
        return (r << 16) + (g << 8) + b;
    }
    
    #endregion
    
    #region Discord Data Structures

    [Serializable]
    public class DiscordWebhookData
    {
        public string content = null!;
        public string username = null!;
        public string avatar_url = null!;
        public Embed[] embeds = null!;
    }

    [Serializable]
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

    [Serializable]
    public class EmbedImage
    {
        public string url = null!;
        public int width;
        public int height;
    }

    [Serializable]
    public class EmbedAuthor
    {
        public string name = null!;
        public string icon_url = null!;
    }

    [Serializable]
    public class EmbedField
    {
        public string name = null!;
        public string value = null!;
        public bool inline;
    }

    [Serializable]
    public class Footer
    {
        public string text = null!;
        public string icon_url = null!;
    }

    [Serializable]
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

    [Serializable]
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

