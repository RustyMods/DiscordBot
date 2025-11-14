using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DiscordBot.Notices;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace DiscordBot;

public enum AIService
{
    None,
    ChatGPT,
    Gemini,
    DeepSeek,
    OpenRouter
}
[PublicAPI]
public enum GPTModel
{
    [InternalName("gpt-3.5-turbo")] Turbo,
    [InternalName("gpt-4o")] GPT4o,
    [InternalName("gpt-4o-mini")] GPT4oMini,
    [InternalName("gpt-4.1")] GPT4_1
}

[PublicAPI]
public enum GeminiModel
{
    [InternalName("gemini-2.0-flash")] Flash2_0,
    [InternalName("gemini-2.5-flash")] Flash2_5,
    [InternalName("gemini-2.0-pro")] Pro2_0,
    [InternalName("gemini-2.5-pro")] Pro2_5
}
[PublicAPI]
public enum DeepSeekModel
{
    [InternalName("deepseek-chat")] Chat,
    [InternalName("deepseek-reasoner")] Reasoner
}

[PublicAPI]
public enum OpenRouterModel
{
    // OpenRouter models - https://openrouter.ai/models
    [InternalName("anthropic/claude-3.5-sonnet")] Claude3_5Sonnet,
    [InternalName("google/gemini-2.0-flash-exp:free")] GeminiFlashFree,
    // [InternalName("meta-llama/llama-3.1-8b-instruct:free")] Llama31_8B,
    // [InternalName("meta-llama/llama-3.1-70b-instruct:free")] Llama31_70B,
    [InternalName("meta-llama/llama-4-maverick:free")] Llama4_Maverick,
    [InternalName("microsoft/wizardlm-2-8x22b")] WizardLM8x22B,
    [InternalName("openai/gpt-4o-mini")] GPT4oMini,
    [InternalName("deepseek/deepseek-chat")] DeepSeekChat,
    [InternalName("nousresearch/hermes-3-llama-3.1-405b:free")] Hermes3_Llama31_405b
}

public class InternalName : Attribute
{
    public readonly string internalName;
    public InternalName(string internalName)
    {
        this.internalName = internalName;
    }
}
public class ChatAI : MonoBehaviour
{
    public static ChatAI? instance;
    public event Action<string>? OnResponse;
    public event Action<int, int, int>? OnMetadata;
    public event Action<string>? OnError;
    public Action<string>? OnDeathQuip;
    public Action<string>? OnDayQuip;

    public event Action<string, bool, bool>? OnReply;

    public bool isThinking;
    public int ellipsesCount;
    public float ellipsesTimer;
    public string tempChat = "";

    [HarmonyPatch(typeof(Terminal), nameof(Terminal.UpdateChat))]
    private static class Terminal_UpdateChat_Patch
    {
        [UsedImplicitly]
        private static void Postfix(Terminal __instance)
        {
            if (__instance != Chat.instance || !instance) return;
            instance.tempChat = __instance.m_output.text;
        }
    }
    
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
    private static class ZNet_OnNewConnection_Patch
    {
        [UsedImplicitly]
        private static void Postfix(ZNetPeer peer) => peer.m_rpc.Register<string, string>(nameof(RPC_ChatAIMessage),RPC_ChatAIMessage);
    }

    public void BroadcastMessage(string username, string message)
    {
        if (!ZNet.instance) return;
        foreach (ZNetPeer? peer in ZNet.instance.GetPeers()) peer.m_rpc.Invoke(nameof(RPC_ChatAIMessage), username, message);
        if (!Player.m_localPlayer) return;
        DisplayChatMessage(username, message);
    }

    public static void RPC_ChatAIMessage(ZRpc rpc, string username, string message) => DisplayChatMessage(username, message);

    private static void DisplayChatMessage(string username, string message)
    {
        string text = $"</color><color=orange>{username}</color>: {message}";
        Chat.instance.AddString(Localization.instance.Localize(text));
        Chat.instance.Show();
    }
    
    public void Awake()
    {
        instance = this;
        OnResponse += HandleResponse;
        OnError += HandleError;
        OnDeathQuip += HandleDeathQuip;
        OnMetadata += HandleMetadata;
        OnReply += HandleReply;
        OnDayQuip = HandleDayQuip;
        DiscordBotPlugin.LogDebug("Initializing Chat AI");
    }

    public void Update()
    {
        if (!Chat.instance || !isThinking) return;
        ellipsesTimer += Time.deltaTime;
        if (ellipsesTimer < 0.5f) return;
        ellipsesTimer = 0.0f;
        if (ellipsesCount > 3) ellipsesCount = 0;
        ellipsesCount++;
        Chat.instance.m_output.text = $"{tempChat}{new string('.', ellipsesCount)}";
    }

    public void OnDestroy()
    {
        instance = null;
    }

    public void HandleDayQuip(string message)
    {
        Discord.instance?.SendMessage(Webhook.Notifications, message: message, hooks: DiscordBotPlugin.OnNewDayHooks);
        Discord.instance?.BroadcastMessage(ZNet.instance.GetWorldName(), message, false);
    }

    public void HandleReply(string message, bool deathQuip, bool dayQuip)
    {
        if (deathQuip) OnDeathQuip?.Invoke(message);
        else if (dayQuip) OnDayQuip?.Invoke(message);
        else OnResponse?.Invoke(message);
    }
    public void HandleResponse(string response)
    {
        BroadcastMessage($"[{DiscordBotPlugin.AIService}]", response);
        Discord.instance?.SendMessage(Webhook.Chat, DiscordBotPlugin.AIService.ToString(), response);
    }

    public void HandleMetadata(int promptTokenCount, int candidatesTokenCount, int totalTokenCount)
    {
        var log = $"Prompt Token Count: {promptTokenCount}; Candidates Token Count: {candidatesTokenCount}; Total Token Count: {totalTokenCount}";
        DiscordBotPlugin.LogDebug(log);
    }

    public void HandleError(string error)
    {
        DiscordBotPlugin.LogError(error);
    }

    public void HandleDeathQuip(string quip)
    {
        if (Screenshot.instance) Screenshot.instance.message = quip;
        if (Recorder.instance) Recorder.instance.message = quip;
    }

    public void Ask(string prompt, bool deathQuip = false, bool dayQuip = false)
    {
        switch (DiscordBotPlugin.GetAIServiceOption())
        {
            case AIService.ChatGPT:
                AskOpenAI(prompt, deathQuip, dayQuip);
                break;
            case AIService.Gemini:
                AskGemini(prompt, deathQuip, dayQuip);
                break;
            case AIService.DeepSeek:
                AskDeepSeek(prompt, deathQuip, dayQuip);
                break;
            case AIService.OpenRouter:
                AskOpenRouter(prompt, deathQuip, dayQuip);
                break;
        }
    }

    public static bool HasKey() => DiscordBotPlugin.GetAIServiceOption() switch
    {
        AIService.ChatGPT => !string.IsNullOrEmpty(DiscordBotPlugin.GetChatGPTKey()),
        AIService.Gemini => !string.IsNullOrEmpty(DiscordBotPlugin.GetGeminiKey()),
        AIService.DeepSeek => !string.IsNullOrEmpty(DiscordBotPlugin.GetDeepSeekKey()),
        AIService.OpenRouter => !string.IsNullOrEmpty(DiscordBotPlugin.GetOpenRouterKey()),
        _ => false
    };
    
    public void AskOpenAI(string prompt, bool deathQuip = false, bool dayQuip = false)
    {
        string key = DiscordBotPlugin.GetChatGPTKey();
        
        if (string.IsNullOrEmpty(key))
        {
            OnError?.Invoke("OpenAI API token not set");
            return;
        }
        StartCoroutine(PromptOpenAI(key, prompt, deathQuip, dayQuip));
    }

    public void AskGemini(string prompt, bool deathQuip = false, bool dayQuip = false)
    {
        var key = DiscordBotPlugin.GetGeminiKey();
        if (string.IsNullOrEmpty(key))
        {
            OnError?.Invoke("Gemini API token not set");
            return;
        }
        StartCoroutine(PromptGemini(key, prompt, deathQuip, dayQuip));
    }

    public void AskDeepSeek(string prompt, bool deathQuip = false, bool dayQuip = false)
    {
        var key = DiscordBotPlugin.GetDeepSeekKey();
        if (string.IsNullOrEmpty(key))
        {
            OnError?.Invoke("DeepSeek API token not set");
            return;
        }
        StartCoroutine(PromptDeepSeek(key, prompt, deathQuip, dayQuip));
    }
    
    public void AskOpenRouter(string prompt, bool deathQuip = false, bool dayQuip = false)
    {
        var key = DiscordBotPlugin.GetOpenRouterKey();
        if (string.IsNullOrEmpty(key))
        {
            OnError?.Invoke("OpenRouter API token not set");
            return;
        }
        StartCoroutine(PromptOpenRouter(key, prompt, deathQuip, dayQuip));
    }
    
    private IEnumerator PromptOpenAI(string apiKey, string prompt, bool deathQuip, bool dayQuip)
    {
        isThinking = true;
        const string url = "https://api.openai.com/v1/chat/completions";
        
        GPTRequest gpt = new GPTRequest();
        gpt.model = GPTModel.Turbo.GetAttributeOfType<InternalName>().internalName;
        gpt.messages.Add(new  GPTMessage("user", prompt));

        string json = JsonConvert.SerializeObject(gpt);
        byte[] body = Encoding.UTF8.GetBytes(json);
        
        using UnityWebRequest request = new (url, "POST");
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        
        yield return request.SendWebRequest();
        isThinking = false;

        if (request.result != UnityWebRequest.Result.Success)
        {
            OnError?.Invoke($"Failed to prompt gpt: {request.error}");
            yield break;
        }
        
        ParseGPTResponse(request.downloadHandler.text, deathQuip, dayQuip);
    }

    public void ParseGPTResponse(string json, bool deathQuip, bool dayQuip)
    {
        GPTResponse? response = JsonConvert.DeserializeObject<GPTResponse>(json);
        if (response == null)
        {
            OnError?.Invoke("Failed to parse response");
        }
        else
        {
            string reply = response.choices[0].message.content.Trim();
            OnReply?.Invoke(reply, deathQuip, dayQuip);
        }
    }
    
    private IEnumerator PromptGemini(string apiKey, string prompt, bool deathQuip, bool dayQuip)
    {
        isThinking = true;
        string model = DiscordBotPlugin.GetGeminiOption().GetAttributeOfType<InternalName>().internalName;
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
        
        GeminiRequest geminiRequest = new GeminiRequest();
        geminiRequest.contents.Add(new GeminiContent
        {
            parts = new List<GeminiPart> { new() { text = prompt } }
        });

        string json = JsonConvert.SerializeObject(geminiRequest);
        byte[] body = Encoding.UTF8.GetBytes(json);
        
        using UnityWebRequest request = new (url, "POST");
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        
        yield return request.SendWebRequest();
        isThinking = false;
        if (request.result != UnityWebRequest.Result.Success)
        {
            OnError?.Invoke($"Failed to prompt Gemini: {request.error}");
            OnError?.Invoke($"Response: {request.downloadHandler.text}");
            yield break;
        }
        
        ParseGeminiResponse(request.downloadHandler.text, deathQuip, dayQuip);
    }
    
    public void ParseGeminiResponse(string json, bool deathQuip, bool dayQuip)
    {
        GeminiResponse? response = JsonConvert.DeserializeObject<GeminiResponse>(json);
        if (response?.candidates == null || response.candidates.Length == 0)
        {
            OnError?.Invoke("Failed to parse gemini response");
        }
        else
        {
            string reply = response.candidates[0].content.parts[0].text.Trim();

            if (response.UsageMetadata != null)
            {
                OnMetadata?.Invoke(response.UsageMetadata.promptTokenCount, response.UsageMetadata.candidatesTokenCount, response.UsageMetadata.totalTokenCount);
            }
            
            OnReply?.Invoke(reply, deathQuip, dayQuip);
        }
    }
    
    private IEnumerator PromptDeepSeek(string apiKey, string prompt, bool deathQuip, bool dayQuip)
    {
        isThinking = true;
        const string url = "https://api.deepseek.com/chat/completions";
        
        DeepSeekRequest deepSeekRequest = new DeepSeekRequest();
        deepSeekRequest.model = DeepSeekModel.Chat.GetAttributeOfType<InternalName>().internalName;
        deepSeekRequest.messages.Add(new DeepSeekMessage("user", prompt));

        string json = JsonConvert.SerializeObject(deepSeekRequest);
        byte[] body = Encoding.UTF8.GetBytes(json);
        
        using UnityWebRequest request = new (url, "POST");
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        
        yield return request.SendWebRequest();
        isThinking = false;
        if (request.result != UnityWebRequest.Result.Success)
        {
            OnError?.Invoke($"Failed to prompt DeepSeek: {request.error}");
            OnError?.Invoke($"Response: {request.downloadHandler.text}");
            yield break;
        }
        
        ParseDeepSeekResponse(request.downloadHandler.text, deathQuip, dayQuip);
    }
    
    public void ParseDeepSeekResponse(string json, bool deathQuip, bool dayQuip)
    {
        DeepSeekResponse? response = JsonConvert.DeserializeObject<DeepSeekResponse>(json);
        if (response?.choices == null || response.choices.Length == 0)
        {
            OnError?.Invoke("Failed to parse DeepSeek response");
        }
        else
        {
            string reply = response.choices[0].message.content.Trim();
            OnReply?.Invoke(reply, deathQuip, dayQuip);
        }
    }
    
    private IEnumerator PromptOpenRouter(string apiKey, string prompt, bool deathQuip, bool dayQuip)
    {
        isThinking = true;
        const string url = "https://openrouter.ai/api/v1/chat/completions";
        
        OpenRouterRequest openRouterRequest = new OpenRouterRequest();
        openRouterRequest.model = DiscordBotPlugin.GetOpenRouterOption().GetAttributeOfType<InternalName>().internalName;
        openRouterRequest.messages.Add(new OpenRouterMessage("user", prompt));

        string json = JsonConvert.SerializeObject(openRouterRequest);
        byte[] body = Encoding.UTF8.GetBytes(json);
        
        using UnityWebRequest request = new (url, "POST");
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        request.SetRequestHeader("HTTP-Referer", "https://github.com/RustyMods/DiscordBot");
        request.SetRequestHeader("X-Title", "DiscordBot");
        
        yield return request.SendWebRequest();
        isThinking = false;
        if (request.result != UnityWebRequest.Result.Success)
        {
            OnError?.Invoke($"Failed to prompt OpenRouter: {request.error}");
            OnError?.Invoke($"Response: {request.downloadHandler.text}");
            yield break;
        }
        
        ParseOpenRouterResponse(request.downloadHandler.text, deathQuip, dayQuip);
    }
    
    public void ParseOpenRouterResponse(string json, bool deathQuip, bool dayQuip)
    {
        OpenRouterResponse? response = JsonConvert.DeserializeObject<OpenRouterResponse>(json);
        if (response?.choices == null || response.choices.Length == 0)
        {
            OnError?.Invoke("Failed to parse OpenRouter response");
        }
        else
        {
            string reply = response.choices[0].message.content.Trim();
            OnReply?.Invoke(reply, deathQuip, dayQuip);
        }
    }

    #region OpenAI Objects
    [Serializable]
    public class GPTRequest
    {
        public string model = "gpt-4o-mini";
        public List<GPTMessage> messages = new();
    }
    
    [Serializable]
    public class GPTResponse
    {
        public GPTChoice[] choices;
    }

    [Serializable]
    public class GPTChoice
    {
        public GPTMessage message;
    }
    
    [Serializable]
    public class GPTMessage
    {
        public string role;
        public string content;
        
        
        public GPTMessage(){}

        public GPTMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }
    #endregion
    
    #region Gemini Objects
    [Serializable]
    public class GeminiRequest
    {
        public List<GeminiContent> contents = new();
    }
    [Serializable]
    public class GeminiContent
    {
        public List<GeminiPart> parts;
    }
    [Serializable]
    public class GeminiPart
    {
        public string text;
    }
    [Serializable]
    public class GeminiResponse
    {
        public GeminiCandidate[] candidates;
        public GeminiUsageMetadata? UsageMetadata;
    }

    [Serializable]
    public class GeminiUsageMetadata
    {
        public int promptTokenCount;
        public int candidatesTokenCount;
        public int totalTokenCount;
    }
    [Serializable]
    public class GeminiCandidate
    {
        public GeminiContent content;
    }
    #endregion
    
    #region DeepSeek Objects
    [Serializable]
    public class DeepSeekRequest
    {
        public string model = "deepseek-chat";
        public List<DeepSeekMessage> messages = new();
        public bool stream = false;
    }
    
    [Serializable]
    public class DeepSeekResponse
    {
        public DeepSeekChoice[] choices;
    }

    [Serializable]
    public class DeepSeekChoice
    {
        public DeepSeekMessage message;
    }
    
    [Serializable]
    public class DeepSeekMessage
    {
        public string role;
        public string content;
        
        public DeepSeekMessage(){}

        public DeepSeekMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }
    #endregion
    
    #region OpenRouter Objects
    [Serializable]
    public class OpenRouterRequest
    {
        public string model = "anthropic/claude-3.5-sonnet";
        public List<OpenRouterMessage> messages = new();
        public bool stream = false;
    }
    
    [Serializable]
    public class OpenRouterResponse
    {
        public OpenRouterChoice[] choices;
    }

    [Serializable]
    public class OpenRouterChoice
    {
        public OpenRouterMessage message;
    }
    
    [Serializable]
    public class OpenRouterMessage
    {
        public string role;
        public string content;
        
        public OpenRouterMessage(){}

        public OpenRouterMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }
    #endregion
}