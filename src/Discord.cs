using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace DiscordBot;

public class Discord : MonoBehaviour
{
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    private static class ZNet_Awake_Patch
    {
        [UsedImplicitly]
        private static void Postfix(ZNet __instance)
        {
            DiscordBotPlugin.m_instance.gameObject.AddComponent<Discord>();
            DiscordBotPlugin.m_instance.gameObject.AddComponent<Screenshot>();
            DiscordBotPlugin.m_instance.gameObject.AddComponent<Recorder>();
        }
    }

    public static Discord? instance;
    public event Action<Sprite>? OnImageDownloaded;
    public event Action<AudioClip>? OnAudioDownloaded; // not implemented yet
    public event Action<string>? OnError;
    
    private bool m_isDownloadingImage;
    private bool m_isDownloadingSound;
    private bool isServer => ZNet.instance?.IsServer() ?? false;

    public void Awake()
    {
        instance = this;
        OnImageDownloaded += HandleImage;
        OnError += HandleError;
    }

    private void Start()
    {
        if (!isServer) return;
        SendMessage(Webhook.Commands, ZNet.instance.GetWorldName(), $"{EmojiHelper.Emoji("question")} type `!help` to find list of available commands");
        if (DiscordBotPlugin.ShowServerStart)
        {
            SendStatus(Webhook.Notifications, Keys.ServerStart, ZNet.instance.GetWorldName(), Keys.Launching, new Color(0.4f, 0.98f, 0.24f));
        }
    }

    private void OnDestroy()
    {
        instance = null;
    }
    
    private static void HandleError(string message)
    {
        if (!DiscordBotPlugin.LogErrors) return;
        DiscordBotPlugin.LogWarning(message);
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
            AudioClip? clip = DownloadHandlerAudioClip.GetContent(request);
            OnAudioDownloaded?.Invoke(clip);
        }
        m_isDownloadingSound = false;
    }
    #endregion
    
    #region RPC Handlers

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
    private static class ZNet_OnNewConnection_Patch
    {
        [UsedImplicitly]
        private static void Postfix(ZNetPeer peer) => peer.m_rpc.Register<string, string>(nameof(RPC_ClientBotMessage),RPC_ClientBotMessage);
    }

    public void BroadcastMessage(string username, string message)
    {
        if (!ZNet.instance || !ZNet.m_isServer) return;
        foreach (var peer in ZNet.instance.GetPeers()) peer.m_rpc.Invoke(nameof(RPC_ClientBotMessage), username, message);
    }

    public static void RPC_ClientBotMessage(ZRpc rpc, string username, string message) => DisplayChatMessage(username, message);
    public static void DisplayChatMessage(string userName, string message)
    {
        string text = $"<color=#{ColorUtility.ToHtmlStringRGB(new Color(0f, 0.5f, 0.5f, 1f))}>[Discord]</color><color=orange>{userName}</color>: {message}";
        Chat.instance.AddString(text);
    }
    #endregion
    
    #region Sending Messages to Discord
    
    public void SendMessage(Webhook webhook, string username = "", string message = "")
    {
        DiscordWebhookData webhookData = new DiscordWebhookData(username, message);
        StartCoroutine(SendWebhookMessage(webhookData, webhook.ToURL()));
    }

    public void SendImage(Webhook webhook, string username, Texture2D image)
    {
        var webhookData = new DiscordWebhookData(username);
        byte[] data = image.EncodeToPNG();
        var form = new MultipartFormFileSection("file", data, DateTime.UtcNow.ToShortDateString() + ".png", "image/png");
        StartCoroutine(SendWebhookAttachment(webhookData, webhook.ToURL(), form));
    }

    public void SendEmbedMessage(Webhook webhook, string title, string content, string username = "", string thumbnail = "")
    {
        Embed embed = new Embed(title, content);
        embed.AddThumbnail(thumbnail);
        var webhookData = new DiscordWebhookData(username, embed);

        StartCoroutine(SendWebhookMessage(webhookData, webhook.ToURL()));
    }
    
    public void SendTableEmbed(Webhook webhook, string title, Dictionary<string, string> tableData, string username = "", string thumbnail = "")
    {
        if (tableData.Count <= 0)
        {
            OnError?.Invoke("Table data is empty");
        }
        List<EmbedField> fields = new List<EmbedField>();

        foreach (KeyValuePair<string, string> kvp in tableData)
        {
            fields.Add(new EmbedField(kvp.Key, kvp.Value));
        }

        Embed embed = new Embed(title, fields);
        embed.AddThumbnail(thumbnail);
        
        DiscordWebhookData webhookData = new DiscordWebhookData(username, embed);

        StartCoroutine(SendWebhookMessage(webhookData, webhook.ToURL()));
    }

    public void SendStatus(Webhook webhook, string content, string worldName, string status, Color color, string username = "", string thumbnail = "")
    {
        Embed embed = new Embed(content);
        embed.SetColor(color);
        List<EmbedField> fields = new();
        fields.Add(new EmbedField(Keys.WorldName, worldName));
        fields.Add(new EmbedField(Keys.Status, status));
        embed.fields = fields.ToArray();
        embed.AddThumbnail(thumbnail);
        DiscordWebhookData webhookData = new DiscordWebhookData(username, embed);
        StartCoroutine(SendWebhookMessage(webhookData, webhook.ToURL()));
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

    private IEnumerator SendWebhookAttachment(DiscordWebhookData data, string webhookURL, MultipartFormFileSection attachment)
    {
        if (string.IsNullOrEmpty(webhookURL))
        {
            OnError?.Invoke("Webhook URL is not set!");
            yield break;
        }
        
        string json = JsonConvert.SerializeObject(data);

        List<IMultipartFormSection> formData = new();
        formData.Add(attachment);
        formData.Add(new MultipartFormDataSection("payload_json", json, "application/json"));

        using UnityWebRequest request = UnityWebRequest.Post(webhookURL, formData);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            string error = $"Failed to send message: {request.error} - {request.downloadHandler.text}";
            OnError?.Invoke(error);
        }
    }
    
    public void SendImageMessage(Webhook webhook, string title, string content, byte[] imageData, string filename, string username = "", string thumbnail = "")
    {
        MultipartFormFileSection attachment = new MultipartFormFileSection("file", imageData, filename, "image/png");
        Embed screenshot = new Embed(title, content);
        screenshot.AddImage($"attachment://{filename}");
        screenshot.AddThumbnail(thumbnail);
        DiscordWebhookData data = new(username, screenshot);
        StartCoroutine(SendWebhookAttachment(data, webhook.ToURL(), attachment));
    }

    public void SendGifMessage(Webhook webhook, string title, string content, byte[] gif, string filename, string username = "", string thumbnail = "")
    {
        MultipartFormFileSection attachment = new MultipartFormFileSection("file", gif, filename, "image/gif");
        Embed screenshot = new Embed(title, content);
        screenshot.AddImage($"attachment://{filename}");
        screenshot.AddThumbnail(thumbnail);
        DiscordWebhookData data = new(username, screenshot);
        StartCoroutine(SendWebhookAttachment(data, webhook.ToURL(), attachment));
    }
    
    #endregion
    
    #region Utility Methods
    
    [Description("Color utility to format into int for discord")]
    private static int ColorToInt(Color color)
    {
        int r = Mathf.RoundToInt(color.r * 255);
        int g = Mathf.RoundToInt(color.g * 255);
        int b = Mathf.RoundToInt(color.b * 255);
        return (r << 16) + (g << 8) + b;
    }
    
    #endregion
    
    #region Discord Webhook 

    [Serializable][UsedImplicitly]
    public class DiscordWebhookData
    {
        public string? content; // up to 2000 characters
        public bool tts; // text-to-speech
        public Embed[]? embeds; // up to 10
        public string? username; // override username display
        public string? avatar_url;

        public DiscordWebhookData(string username, string content)
        {
            if (!string.IsNullOrEmpty(username)) this.username = Localization.instance.Localize(username);
            this.content = Localization.instance.Localize(content);
        }

        public DiscordWebhookData(string username, params Embed[] embeds)
        {
            if (!string.IsNullOrEmpty(username)) this.username = username;
            this.embeds = embeds;
        }
    }

    [Serializable][UsedImplicitly]
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

        public Embed(string title, string description) : this()
        {
            this.title = Localization.instance.Localize(title);
            this.description = Localization.instance.Localize(description);
        }

        public Embed(string title, params EmbedField[] fields) : this()
        {
            this.title = title;
            this.fields = fields;
        }

        public Embed(string description) : this()
        {
            this.description = Localization.instance.Localize(description);
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
            color = ColorToInt(Color);
        }
    }
    
    [Serializable][UsedImplicitly]
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

    [Serializable][UsedImplicitly]
    public class EmbedVideo
    {
        public string? url;
        public int height;
        public int width;
    }

    [Serializable][UsedImplicitly]
    public class EmbedProvider
    {
        public string? name;
        public string? url;
    }
    
    [Serializable][UsedImplicitly]
    public class EmbedAuthor
    {
        public string? name;
        public string? icon_url;
    }

    [Serializable][UsedImplicitly]
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

    [Serializable][UsedImplicitly]
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
    #endregion
}

