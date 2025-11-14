using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace DiscordBot;

public class DiscordGatewayClient : MonoBehaviour
{
    private static readonly JsonSerializerSettings settings = new()
    {
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore,
        Error = (_, args) => args.ErrorContext.Handled = true,
    };

    public static DiscordGatewayClient? instance;
    public event Action<Message>? OnChatReceived;
    public event Action<Message>? OnCommandReceived;
    public event Action<string>? OnError;
    public event Action<string>? OnLog;
    public event Action? OnConnected;
    public event Action? OnDisconnected;
    public event Action<string>? OnClosed;

    private const int BUFFER_SIZE = 16384; // 16kb
    private ClientWebSocket? websocket;
    private CancellationTokenSource? cancellationTokenSource;

    private string? gatewayUrl;
    private int sequenceNumber;
    private string? sessionId;
    private bool heartbeatAcknowledged = true;
    private Coroutine? heartbeatCoroutine;
    
    private bool isConnecting;
    private bool isConnected;
    private bool shouldReconnect = true;

    private readonly PriorityQueue<object> messageQueue = new();
    private bool isSending;
    
    private static readonly WaitForSeconds processMessageRate = new (0.1f);
    private static readonly WaitForSeconds retryConnectionDelay = new(5f);
    public void Awake()
    {
        instance = this;
        OnChatReceived += HandleChatMessage;
        OnCommandReceived += HandleCommands;
        OnError += HandleError;
        OnLog += HandleLog;
        OnClosed += DiscordBotPlugin.LogDebug;
        OnConnected += () => DiscordBotPlugin.LogDebug("Connected to discord gateway");
        OnDisconnected += () => DiscordBotPlugin.LogDebug("Disconnected from discord gateway");
        
        DiscordBotPlugin.LogDebug("Initializing Discord Gateway");
    }

    private void Start()
    {
        if (string.IsNullOrEmpty(DiscordBotPlugin.BOT_TOKEN))
        {
            DiscordBotPlugin.LogWarning("Bot token not set");
            return;
        }
        StartCoroutine(InitializeGateway());
    }
    private void OnDestroy()
    {
        shouldReconnect = false;
        DisconnectWebSocket();
        instance = null;
    }
    private static void HandleError(string message)
    {
        DiscordBotPlugin.LogError(message);
    }

    private static void HandleLog(string message) => DiscordBotPlugin.records.Log(LogLevel.Info, message);
    
    private static void HandleChatMessage(Message message)
    {
        instance?.OnLog?.Invoke($"Received discord chat message: username: {message.author?.username ?? "null"} - content: {message.content ?? "null"}");
        var content = message.content ?? "";
        
        Discord.instance?.BroadcastMessage(message.author?.GetDisplayName() ?? "", RemoveMentions(content));
    }
    
    private static string RemoveMentions(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;
    
        // Only removes Discord user mentions: <@userid> or <@!userid>
        // Won't match general text with < and >
        return System.Text.RegularExpressions.Regex.Replace(
            content, 
            @"<@!?\d+>", 
            ""
        ).Trim();
    }
    
    private static void HandleCommands(Message message)
    {
        instance?.OnLog?.Invoke($"Received discord command: username: {message.author?.username ?? "null"} - content: {message.content ?? "null"}");
        if (message.content == null) return;
        string[] args = message.content.Split(' ');
        string command = args[0].Trim();
        
        if (!DiscordCommands.m_commands.TryGetValue(command, out DiscordCommands.DiscordCommand discordCommand))
        {
            Discord.instance?.SendMessage(Webhook.Commands, ZNet.instance.GetWorldName(), "Failed to find command: " + command);
        }
        else if (!discordCommand.IsAllowed(message.author?.GetFullUsername() ?? ""))
        {
            Discord.instance?.SendMessage(Webhook.Commands, ZNet.instance.GetWorldName(), message.author?.GetFullUsername() + " not allowed to use command: " + command);
        }
        else
        {
            discordCommand.Run(args, message.author?.GetDisplayName());
        }
    }

    public void TriggerProcessing()
    {
        if (isSending || messageQueue.Count <= 0) return;
        isSending = true;
        StartCoroutine(ProcessMessageQueue());
    }

    private IEnumerator ProcessMessageQueue()
    {
        while (messageQueue.Count > 0 && isConnected)
        {
            object message = messageQueue.Dequeue();
            yield return SendGatewayMessage(message);
            yield return processMessageRate;
        }

        isSending = false;
    }

    
    #region Gateway Initialization
    private IEnumerator InitializeGateway()
    {
        if (isConnecting || isConnected) yield break;
        isConnecting = true;
        using UnityWebRequest request = UnityWebRequest.Get("https://discord.com/api/v10/gateway/bot");
        request.SetRequestHeader("Authorization", $"Bot {DiscordBotPlugin.BOT_TOKEN}");
        request.SetRequestHeader("Content-Type", "application/json");
        
        yield return request.SendWebRequest();
        
        if (request.result == UnityWebRequest.Result.Success)
        {
            JObject response = JObject.Parse(request.downloadHandler.text);
            gatewayUrl = response["url"]?.ToString() ?? string.Empty;
                
            if (!string.IsNullOrEmpty(gatewayUrl))
            {
                gatewayUrl += "/?v=10&encoding=json";
                yield return StartCoroutine(ConnectToGateway());
            }
            else
            {
                OnError?.Invoke("Failed to get gateway URL");
                isConnecting = false;
            }
        }
        else
        {
            OnError?.Invoke($"Failed to get gateway URL: {request.error}");
            isConnecting = false;
            
            yield return retryConnectionDelay;
            if (shouldReconnect)
            {
                StartCoroutine(InitializeGateway());
            }
        }
    }
    
    #endregion
    
    #region WebSocket Connection
    
    private IEnumerator ConnectToGateway()
    {
        websocket = new ClientWebSocket();
        cancellationTokenSource = new CancellationTokenSource();
        
        Task connectTask = websocket.ConnectAsync(new Uri(gatewayUrl ?? ""), cancellationTokenSource.Token);
        yield return new WaitUntil(() => connectTask.IsCompleted);

        switch (connectTask.Status)
        {
            case TaskStatus.RanToCompletion:
                isConnected = true;
                isConnecting = false;
                StartCoroutine(ListenForMessages());
                break;
            case TaskStatus.Canceled:
                OnClosed?.Invoke("Connection task canceled");
                isConnecting = false;
                break;
            case TaskStatus.Faulted:
            {
                OnError?.Invoke($"Failed to connect: {connectTask.Exception?.GetBaseException().Message}");
                isConnecting = false;
        
                if (shouldReconnect)
                {
                    StartCoroutine(ReconnectAfterDelay());
                }

                break;
            }
            default:
                OnError?.Invoke($"Failed to connect: {connectTask.Exception?.GetBaseException().Message}");
                isConnecting = false;
                break;
        }
    }
    
    private IEnumerator ListenForMessages()
    {
        byte[] buffer = new byte[BUFFER_SIZE];
    
        while (isConnected && websocket?.State == WebSocketState.Open && cancellationTokenSource != null)
        {
            Task<WebSocketReceiveResult>? receiveTask = websocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationTokenSource.Token);
        
            yield return new WaitUntil(() => receiveTask.IsCompleted);
        
            if (receiveTask.Status == TaskStatus.RanToCompletion)
            {
                WebSocketReceiveResult result = receiveTask.Result;
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    
                    try
                    {
                        GatewayEvent? payload = JsonConvert.DeserializeObject<GatewayEvent>(message, settings);
                        if (payload == null) continue;
                        HandleGatewayPayload(payload);
                    }
                    catch (Exception e)
                    {
                        OnError?.Invoke($"Failed to process gateway message: {e.Message}");
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    OnClosed?.Invoke("WebSocket closed by server");
                    OnWebSocketClose();
                    break;
                }
            }
            else if (receiveTask.Status == TaskStatus.Canceled)
            {
                OnClosed?.Invoke("WebSocket receive task canceled");
                break;
            }
            else if (receiveTask.Status == TaskStatus.Faulted)
            {
                OnError?.Invoke($"WebSocket receive error: {receiveTask.Exception?.GetBaseException().Message}");
                OnWebSocketClose();
                break;
            }
        }
    }
    
    private void OnWebSocketClose()
    {
        OnClosed?.Invoke("WebSocket connection closed");
        isConnected = false;
        
        if (heartbeatCoroutine != null)
        {
            StopCoroutine(heartbeatCoroutine);
            heartbeatCoroutine = null;
        }
        
        OnDisconnected?.Invoke();
        
        if (shouldReconnect)
        {
            StartCoroutine(ReconnectAfterDelay());
        }
    }
    
    #endregion
    
    #region Gateway Protocol Handling
    [Serializable]
    public class GatewayEvent
    {
        public int op;
        public JObject? d;
        public int? s;
        public string? t;
    }
    
    [UsedImplicitly]
    public enum Opcodes
    {
        Dispatch = 0,
        Heartbeat = 1,
        Identify = 2,
        PresenceUpdate = 3,
        VoiceStateUpdate = 4,
        Resume = 6,
        Reconnect = 7,
        RequestGuildMembers = 8,
        InvalidSession = 9,
        Hello = 10,
        HeartbeatOK = 11,
        RequestSoundboardSounds = 31
    }
    
    private void HandleGatewayPayload(GatewayEvent? payload)
    {
        if (payload == null) return;
        if (payload.s.HasValue)
        {
            sequenceNumber = payload.s.Value;
        }
        switch ((Opcodes)payload.op)
        {
            case Opcodes.Dispatch: 
                HandleDispatchEvent(payload.t, payload.d);
                break;
            case Opcodes.Heartbeat:
                SendHeartbeat();
                break;
            case Opcodes.Reconnect:
                StartCoroutine(ReconnectAfterDelay());
                break;
            case Opcodes.InvalidSession:
                OnError?.Invoke("Invalid session, re-identifying...");
                sessionId = null;
                SendIdentify();
                break;
            case Opcodes.Hello:
                HandleHello(payload.d);
                break;
            case Opcodes.HeartbeatOK: 
                heartbeatAcknowledged = true;
                break;
        }
    }
    
    private void HandleDispatchEvent(string? eventType, JObject? data)
    {
        if (data == null) return;
        switch (eventType)
        {
            case "READY":
                HandleReady(data);
                break;
            case "MESSAGE_CREATE":
                HandleMessageCreate(data);
                break;
            case "RESUMED":
                OnConnected?.Invoke();
                break;
        }
    }
    
    private void HandleHello(JObject? data)
    {
        if (data == null) return;
        if (heartbeatCoroutine != null)
        {
            StopCoroutine(heartbeatCoroutine);
        }
        heartbeatCoroutine = StartCoroutine(HeartbeatLoop((data["heartbeat_interval"]?.Value<int>() ?? 41250) / 1000f));
        if (string.IsNullOrEmpty(sessionId)) SendIdentify();
        else SendResume();
    }
    
    private void HandleReady(JToken data)
    {
        sessionId = data["session_id"]?.Value<string>();
        OnConnected?.Invoke();
    }
    private void HandleMessageCreate(JObject? data)
    {
        if (data == null) return;
        try
        {
            Message? message = JsonConvert.DeserializeObject<Message>(data.ToString(), settings);
            if (message == null) return;
            if (message.author?.bot == true || string.IsNullOrEmpty(message.content)) return;

            if (Channel.Commands.ToID() == Channel.Chat.ToID() && message.channel_id == Channel.Commands.ToID())
            {
                string[] args = message.content!.Split(' ');
                string command = args[0].Trim();
                if (DiscordCommands.IsCommand(command))
                {
                    OnCommandReceived?.Invoke(message);
                }
                else
                {
                    OnChatReceived?.Invoke(message);
                }
            }
            else
            {
                if (message.channel_id == Channel.Commands.ToID())
                {
                    OnCommandReceived?.Invoke(message);
                }
                else if (message.channel_id == Channel.Chat.ToID())
                {
                    OnChatReceived?.Invoke(message);
                }
            }
            
        }
        catch (Exception e)
        {
            OnError?.Invoke($"Failed to parse message: {e.Message}");
        }
    }
    
    #endregion
    
    #region Gateway Commands

    [UsedImplicitly]
    public enum Intent
    {
        Guilds = 1,
        GuildMessages = 512,
        MessageContent = 32768
    }
    
    private void SendIdentify()
    {
        var identify = new
        {
            op = (int)Opcodes.Identify,
            d = new
            {
                token = DiscordBotPlugin.BOT_TOKEN,
                intents = (int)Intent.Guilds + (int)Intent.GuildMessages + (int)Intent.MessageContent,
                properties = new
                {
                    os = "unity",
                    browser = "unity-bot",
                    device = "unity-bot"
                }
            }
        };
        messageQueue.Enqueue(identify, true);
    }
    
    private void SendResume()
    {
        var resume = new
        {
            op = (int)Opcodes.Resume,
            d = new
            {
                token = DiscordBotPlugin.BOT_TOKEN,
                session_id = sessionId,
                seq = sequenceNumber
            }
        };
        messageQueue.Enqueue(resume, true);
    }
    
    private void SendHeartbeat()
    {
        if (!isConnected) return;
        
        var heartbeat = new
        {
            op = (int)Opcodes.Heartbeat,
            d = sequenceNumber
        };
        messageQueue.Enqueue(heartbeat, true);
    }
    
    private IEnumerator SendGatewayMessage(object payload)
    {
        if (websocket?.State != WebSocketState.Open || cancellationTokenSource == null) yield break;
        string json = JsonConvert.SerializeObject(payload);
        byte[] data = Encoding.UTF8.GetBytes(json);
            
        Task sendTask = websocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, cancellationTokenSource.Token);
            
        yield return new WaitUntil(() => sendTask.IsCompleted);
            
        if (sendTask.Status == TaskStatus.Faulted)
        {
            OnError?.Invoke($"Failed to send gateway message: {sendTask.Exception?.Message}");
        }
    }
    
    #endregion
    
    #region Heartbeat System
    
    private IEnumerator HeartbeatLoop(float intervalSeconds)
    {
        yield return new WaitForSeconds(UnityEngine.Random.Range(0f, intervalSeconds));
        
        while (isConnected)
        {
            if (!heartbeatAcknowledged)
            {
                OnError?.Invoke("Heartbeat not acknowledged, reconnecting...");
                StartCoroutine(ReconnectAfterDelay());
                yield break;
            }
            
            heartbeatAcknowledged = false;
            SendHeartbeat();
            
            yield return new WaitForSeconds(intervalSeconds);
        }
    }
    
    #endregion
    
    #region Reconnection
    
    private IEnumerator ReconnectAfterDelay()
    {
        DisconnectWebSocket();
        
        if (!shouldReconnect) yield break;
        
        OnError?.Invoke("Attempting to reconnect in 5 seconds...");
        yield return retryConnectionDelay;
        
        if (shouldReconnect)
        {
            StartCoroutine(InitializeGateway());
        }
    }
    
    private void DisconnectWebSocket()
    {
        isConnected = false;
        messageQueue.Clear();
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }
        
        if (websocket != null)
        {
            if (websocket.State == WebSocketState.Open)
            {
                try
                {
                    websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
                catch (Exception e)
                {
                    OnError?.Invoke($"Error closing WebSocket: {e.Message}");
                }
            }
            
            websocket.Dispose();
            websocket = null;
        }
        
        if (heartbeatCoroutine != null)
        {
            StopCoroutine(heartbeatCoroutine);
            heartbeatCoroutine = null;
        }
    }
    
    #endregion
    
    private class PriorityQueue<T>
    {
        private readonly Queue<T> highPriority = new Queue<T>();
        private readonly Queue<T> normalPriority = new Queue<T>();
    
        public void Enqueue(T item, bool priority = false)
        {
            if (!instance?.isConnected ?? false) return;
            if (priority) highPriority.Enqueue(item);
            else normalPriority.Enqueue(item);
            instance?.TriggerProcessing();
        }
    
        public T Dequeue()
        {
            return highPriority.Count > 0 ? highPriority.Dequeue() : normalPriority.Dequeue();
        }
    
        public int Count => highPriority.Count + normalPriority.Count;
        public void Clear() { highPriority.Clear(); normalPriority.Clear(); }
    }
}

[Serializable]
public class Message
{
    public string? timestamp;
    public string? id;
    public string? channel_id;
    public Author? author;
    public string? content;
}

[Serializable]
public class Author
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

