using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
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
        private static void Postfix(ZNet __instance)
        {
            m_instance.gameObject.AddComponent<Discord>();
            if (!__instance.IsServer()) return;
            if (m_serverStartNotice.Value is Toggle.Off) return;
            instance.SendStatus(Webhook.Notifications, "$msg_server_start", __instance.GetWorldName(), "$status_launch", new Color(0.4f, 0.98f, 0.24f));
        }
    }

    public static Discord instance = null!;
    
    public event Action<Message>? OnMessageReceived;
    public event Action<Message>? OnCommandReceived;
    public event Action<Sprite>? OnImageDownloaded;
    public event Action<AudioClip>? OnAudioDownloaded;
    public event Action<string>? OnError;
    
    private string m_lastMessageID = "";
    private string m_lastCommandID = "";
    private bool m_isPollingChatter;
    private bool m_isPollingCommands;
    private bool m_isDownloadingImage;
    private bool m_isDownloadingSound;

    private DateTime m_timeLoaded;

    public void Awake()
    {
        instance = this;
        m_timeLoaded = DateTime.UtcNow;
        if (!ZNet.m_isServer) return;
        // Only server manages polling
        OnMessageReceived += HandleChatMessage;
        OnCommandReceived += HandleCommands;
        OnImageDownloaded += HandleImage;
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
        BroadcastMessage(message.author?.GetDisplayName() ?? "", message?.content ?? "");
        if (Player.m_localPlayer) DisplayChatMessage(message?.author?.GetDisplayName() ?? "", message?.content ?? "");
    }

    private void HandleCommands(Message message)
    {
        if (message.content == null) return;
        string[] args = message.content.Split(' ');
        string command = args[0].Trim();

        if (!DiscordCommands.m_commands.TryGetValue(command, out DiscordCommands.DiscordCommand discordCommand))
        {
            SendMessage(Webhook.Commands, ZNet.instance.GetWorldName(), "Failed to find command: " + command);
        }
        else if (!discordCommand.IsAllowed(message.author?.GetFullUsername() ?? ""))
        {
            SendMessage(Webhook.Commands, ZNet.instance.GetWorldName(), message.author?.GetFullUsername() + " not allowed to use command: " + command);
        }
        else discordCommand.Run(args);
    }

    private static void HandleImage(Sprite sprite)
    {
        if (ImageHud.instance is null) return;
        ImageHud.instance.Show(sprite);
    }
    
    #region Image Download

    public void GetImage(string imageUrl)
    {
        if (m_isDownloadingImage) return;
        m_isDownloadingImage = true;
        StartCoroutine(DownloadImage(imageUrl));
    }

    private IEnumerator DownloadImage(string imageUrl)
    {
        using UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            string message = $"Failed to download image from {imageUrl}: {request.error}";
            OnError?.Invoke(message);
        }
        else
        {
            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f) // Pivot at center
            );
            OnImageDownloaded?.Invoke(sprite);
        }
        m_isDownloadingImage = false;
    }
    #endregion
    
    #region Sound Download

    public void GetSound(string url, AudioType type)
    {
        if (m_isDownloadingSound) return;
        m_isDownloadingSound = true;
        StartCoroutine(DownloadSound(url, type));
    }

    private IEnumerator DownloadSound(string url, AudioType type)
    {
        using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, type);
        ((DownloadHandlerAudioClip)request.downloadHandler).streamAudio = true;
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            OnError?.Invoke("Failed to download audio: " + request.error);   
        }
        else
        {
            var clip = DownloadHandlerAudioClip.GetContent(request);
            OnAudioDownloaded?.Invoke(clip);
        }
        m_isDownloadingSound = false;
    }
    #endregion
    
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
    
    // does not work
    public void SendComponentMessage(Webhook webhook, string username = "", params EmbedComponent[] components)
    {
        var layoutData = new DiscordWebhookComponents(username, components);
        StartCoroutine(SendLayoutMessage(layoutData, GetWebhookURL(webhook)));
    }
    
    public void SendMessage(Webhook webhook, string username = "", string message = "")
    {
        var webhookData = new DiscordWebhookData(username, message);
        StartCoroutine(SendWebhookMessage(webhookData, GetWebhookURL(webhook)));
    }

    public void SendEmbedMessage(Webhook webhook, string title, string content, string username = "", string thumbnail = "")
    {
        var embed = new Embed(title, content);
        embed.AddThumbnail(thumbnail);
        var webhookData = new DiscordWebhookData(username, embed);

        StartCoroutine(SendWebhookMessage(webhookData, GetWebhookURL(webhook)));
    }
    
    public void SendTableEmbed(Webhook webhook, string title, Dictionary<string, string> tableData, string username = "", string thumbnail = "")
    {
        if (tableData.Count <= 0)
        {
            OnError?.Invoke("Table data is empty");
        }
        var fields = new List<EmbedField>();

        foreach (var kvp in tableData)
        {
            fields.Add(new EmbedField(kvp.Key, kvp.Value));
        }

        var embed = new Embed(title, fields);
        embed.AddThumbnail(thumbnail);
        
        var webhookData = new DiscordWebhookData(username, embed);

        StartCoroutine(SendWebhookMessage(webhookData, GetWebhookURL(webhook)));
    }

    public void SendStatus(Webhook webhook, string content, string worldName, string status, Color color, string username = "", string thumbnail = "")
    {
        var embed = new Embed(content);
        embed.SetColor(color);
        List<EmbedField> fields = new();
        fields.Add(new EmbedField("$label_worldname", worldName));
        fields.Add(new EmbedField("$label_status", status));
        embed.fields = fields.ToArray();
        embed.AddThumbnail(thumbnail);
        var webhookData = new DiscordWebhookData(username, embed);
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
    
    private IEnumerator SendLayoutMessage(DiscordWebhookComponents data, string webhookURL)
    {
        if (string.IsNullOrEmpty(webhookURL))
        {
            OnError?.Invoke("Webhook URL is not set!");
            yield break;
        }
        
        string jsonData = JsonConvert.SerializeObject(data);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        using UnityWebRequest request = new UnityWebRequest(webhookURL + "?with_components=true", "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            string error = $"Failed to send message: {request.error} - {request.downloadHandler.text}";
            OnError?.Invoke(error);
            OnError?.Invoke(jsonData);
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
                if (message.author?.bot ?? true) yield break;

                var timeStamp = DateTime.Parse(message.timestamp).ToUniversalTime(); // local time, so convert
                if (m_timeLoaded > timeStamp)
                {
                    yield break;
                }

                if (message.id == null || message.id == m_lastCommandID) yield break;
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
                        if (message.author?.bot ?? true) continue;
                        if (message.id == null) continue;
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
    public static int ColorToInt(Color color)
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
        public string? content; // up to 2000 characters
        public bool tts; // text-to-speech
        public Embed[]? embeds; // up to 10
        public string? username; // override username display
        public string? avatar_url;

        public DiscordWebhookData(string username, string content)
        {
            if (!string.IsNullOrEmpty(username)) this.username = username;
            this.content = Localization.instance.Localize(content);
        }

        public DiscordWebhookData(string username, params Embed[] embeds)
        {
            if (!string.IsNullOrEmpty(username)) this.username = username;
            this.embeds = embeds;
        }
    }

    public enum DiscordMessageFlags
    {
        CrossPosted = 0, Is_CrossPost = 1, Suppress_Embeds = 2, Source_Message_Deleted = 3, 
        Urgent = 4, Has_Thread = 5, Ephemeral = 6, Loading = 7, Failed_To_Mention_Some_Roles_In_Thread = 8,
        Suppress_Notifications = 12, Is_Voice_Message = 13, Has_Snapshot = 14, Is_Components_V2 = 15
    }
    
    #region Component

    [Serializable]
    public class DiscordWebhookComponents
    {
        public string? username;
        public string? avatar_url;
        public EmbedComponent[]? components;
        public int flags = (int)DiscordMessageFlags.Is_Components_V2;

        public DiscordWebhookComponents(string username, params EmbedComponent[] components)
        {
            if (!string.IsNullOrEmpty(username)) this.username = username;
            this.components = components;
        }
    }

    [Serializable]
    public class EmbedComponent
    {
        public int type;
        public int id;
    }
    [Serializable]
    public class ActionRow : EmbedComponent
    {
        // up to 5 contextually grouped button components
        // a single text input component
        // a single select component
        public EmbedComponent[]? components;

        public ActionRow()
        {
            type = 1;
        }
    }
    [Serializable]
    public class Button : EmbedComponent
    {
        public int style;
        public string? label;
        public string? custom_id;
        public string? url;
        public bool disabled;
        
        public Button(ButtonStyle style, string label)
        {
            this.label = label;
            this.style = (int)style;
            type = 2;
        }

        public enum ButtonStyle
        {
            Primary = 1, Secondary = 2, Success = 3, Danger = 4, Link = 5, Premium = 6
        }
    }
    [Serializable]
    public class StringSelect : EmbedComponent
    {
        public string? custom_id;
        public EmbedOption[]? options;
        public string? placeholder;
        public int min_values;
        public int max_values;
        public bool disabled;

        public StringSelect()
        {
            type = 3;
        }
    }
    [Serializable]
    public class TextInput : EmbedComponent
    {
        public string? custom_id;
        public int style;
        public string? label;
        public int min_length;
        public int max_length;
        public bool required;
        public string? value;
        public string? placeholder;

        public TextInput(TextInputStyle style)
        {
            this.style = (int)style;
            type = 4;
        }

        public enum TextInputStyle
        {
            Short = 1, Paragraph = 2
        }
    }
    [Serializable]
    public class UserSelect : EmbedComponent
    {
        public string? custom_id;
        public string? placeholder;
        public object[]? default_values;
        public int min_values;
        public int max_values;
        public bool disabled;

        public UserSelect()
        {
            type = 5;
        }
    }

    [Serializable]
    public class Section : EmbedComponent
    {
        public EmbedComponent[]? components; // 1 - 3 text components
        public EmbedComponent? accessory; // only thumbnail or button
        public Section(params EmbedComponent[] components)
        {
            this.components = components;
            type = 9;
        }

        public Section()
        {
            type = 9;
        }
    }

    [Serializable]
    public class TextDisplay : EmbedComponent
    {
        public string? content;

        public TextDisplay(string content)
        {
            this.content = content;
            type = 10;
        }
    }
    
    #endregion
    
    [Serializable]
    public class EmbedOption
    {
        public string? label;
        public string? value;
        public string? description;
        public Dictionary<string, string>? emoji;
    }

    [Serializable][Description("Discord embed json object")]
    public class Embed
    {
        public string? title;
        public string? description;
        public string? url;
        public string? timestamp;
        public int? color;
        public Footer? footer;
        public EmbedImage? image;
        public EmbedImage? thumbnail;
        public EmbedVideo? video;
        public EmbedProvider? provider;
        public EmbedAuthor? author;
        public EmbedField[]? fields; // max 25

        public Embed()
        {
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }

        public Embed(string title, string description)
        {
            this.title = Localization.instance.Localize(title);
            this.description = Localization.instance.Localize(description);
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }

        public Embed(string title, params EmbedField[] fields)
        {
            this.title = title;
            this.fields = fields;
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }

        public Embed(string description)
        {
            this.description = Localization.instance.Localize(description);
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }
        
        public Embed(string title, List<EmbedField> fields) : this (title, fields.ToArray()){}

        public void AddImage(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return;
            image = new EmbedImage(imageUrl);
        }

        public void AddThumbnail(string thumbnailUrl)
        {
            if (string.IsNullOrEmpty(thumbnailUrl)) return;
            thumbnail = new EmbedImage(thumbnailUrl);
        }

        public void SetColor(Color Color)
        {
            this.color = ColorToInt(Color);
        }
    }
    
    [Serializable][Description("Discord image json object")]
    public class EmbedImage
    {
        public string? url;
        public int width;
        public int height;

        public EmbedImage(string url, int width = 256, int height = 256)
        {
            this.url = url;
            this.width = width;
            this.height = height;
        }
    }

    [Serializable]
    public class EmbedVideo
    {
        public string? url;
        public int height;
        public int width;
    }

    [Serializable]
    public class EmbedProvider
    {
        public string? name;
        public string? url;
    }
    
    [Serializable][Description("Discord author json object")]
    public class EmbedAuthor
    {
        public string? name;
        public string? icon_url;
    }

    [Serializable][Description("Discord embed json object")]
    public class EmbedField
    {
        public string? name;
        public string? value;
        public bool inline;

        public EmbedField(string name, string value, bool inline = true)
        {
            this.name = Localization.instance.Localize(name);
            this.value = Localization.instance.Localize(value);
            this.inline = inline;
        }
    }

    [Serializable][Description("Discord footer json object")]
    public class Footer
    {
        public string? text;
        public string? icon_url;

        public Footer(string text)
        {
            this.text = Localization.instance.Localize(text);
        }

        public void AddIcon(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            icon_url = url;
        }
    }

    [Serializable][Description("Discord message json object")]
    public class Message
    {
        public string? id;
        public string? channel_id;
        public User? author;
        public string? content;
        public string? timestamp;
        public string? edited_timestamp;
        public bool tts;
        public bool mention_everyone;
        public User[]? mentions;
        public User[]? mention_roles;
        
        public int type;
        public Embed[]? embeds;
    }

    [Serializable][Description("Discord user json object")]
    public class User
    {
        public string? id;
        public string? username;
        public string? global_name;
        public string? discriminator;
        public bool bot;
        public string? avatar;
        public string GetDisplayName() => (!string.IsNullOrEmpty(global_name) ? global_name : username) ?? string.Empty;
        public string GetFullUsername()
        {
            return !string.IsNullOrEmpty(discriminator) && discriminator != "0"
                ? $"{username}#{discriminator}"
                : username ?? string.Empty;
        }
    }
    #endregion
}

