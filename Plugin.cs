using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using DiscordBot.Jobs;
using HarmonyLib;
using JetBrains.Annotations;
using LocalizationManager;
using ServerSync;
using UnityEngine;

namespace DiscordBot;
public static class Extensions
{
    public static string ToURL(this Webhook type) => type switch
    {
        Webhook.Chat => DiscordBotPlugin.ChatWebhookURL,
        Webhook.Notifications => DiscordBotPlugin.NoticeWebhookURL,
        Webhook.Commands =>DiscordBotPlugin.CommandWebhookURL,
        Webhook.DeathFeed => DiscordBotPlugin.DeathFeedWebhookURL,
        _ => DiscordBotPlugin.ChatWebhookURL
    };

    public static string ToID(this Channel type) => type switch
    {
        Channel.Chat => DiscordBotPlugin.ChatChannelID,
        Channel.Commands => DiscordBotPlugin.CommandChannelID,
        _ => DiscordBotPlugin.ChatChannelID,
    };
        
    public static T GetOrAddComponent<T>(this GameObject obj) where T : Component
    {
        return obj.TryGetComponent<T>(out var component) ? component : obj.AddComponent<T>();
    }
}
public enum Toggle { On = 1, Off = 0 }

public enum Webhook
{
    Notifications, 
    Chat, 
    Commands,
    DeathFeed
}

public enum Channel { Chat, Commands }
public enum ChatDisplay { Player, Bot }

[BepInPlugin(ModGUID, ModName, ModVersion)]
public class DiscordBotPlugin : BaseUnityPlugin
{
    internal const string ModName = "DiscordBot";
    internal const string ModVersion = "1.2.6";
    internal const string Author = "RustyMods";
    private const string ModGUID = Author + "." + ModName;
    private const string ConfigFileName = ModGUID + ".cfg";
    private static readonly string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
    internal static string ConnectionError = "";
    private readonly Harmony _harmony = new(ModGUID);
    public static readonly ManualLogSource DiscordBotLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
    private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };
    public static readonly Dir directory = new(Paths.ConfigPath, "DiscordBot");
    public static DiscordBotPlugin m_instance = null!;
    
    private static ConfigEntry<Toggle> _serverConfigLocked = null!;
    #region extra webhooks
    private static ConfigEntry<string> m_startWorldHook = null!;
    private static ConfigEntry<string> m_saveWebhook = null!;
    private static ConfigEntry<string> m_shutdownWebhook = null!;
    private static ConfigEntry<string> m_loginWebhook = null!;
    private static ConfigEntry<string> m_logoutWebhook = null!;
    private static ConfigEntry<string> m_eventWebhook = null!;
    private static ConfigEntry<string> m_newDayWebhook = null!;
    #endregion
    #region notices
    private static ConfigEntry<string> m_notificationWebhookURL = null!;
    private static ConfigEntry<Toggle> m_serverStartNotice = null!;
    private static ConfigEntry<Toggle> m_serverStopNotice = null!;
    private static ConfigEntry<Toggle> m_serverSaveNotice = null!;
    private static ConfigEntry<Toggle> m_deathNotice = null!;
    private static ConfigEntry<Toggle> m_loginNotice = null!;
    private static ConfigEntry<Toggle> m_logoutNotice = null!;
    private static ConfigEntry<Toggle> m_eventNotice = null!;
    private static ConfigEntry<Toggle> m_newDayNotice = null!;
    private static ConfigEntry<Toggle> m_coordinateDetails = null!;
    private static ConfigEntry<Toggle> m_commandNotice = null!;
    #endregion
    #region chat
    private static ConfigEntry<string> m_chatWebhookURL = null!;
    private static ConfigEntry<string> m_chatChannelID = null!;
    private static ConfigEntry<Toggle> m_chatEnabled = null!;
    private static ConfigEntry<ChatDisplay> m_chatType = null!;
    #endregion
    #region commands
    private static ConfigEntry<string> m_commandWebhookURL = null!;
    private static ConfigEntry<string> m_commandChannelID = null!;
    #endregion
    #region death
    private static ConfigEntry<string> m_deathFeedURL = null!;
    #endregion
    #region general
    private static ConfigEntry<string> m_discordAdmins = null!;
    private static ConfigEntry<Toggle> m_logErrors = null!;
    private static ConfigEntry<string> m_botToken = null!;
    private static ConfigEntry<Toggle> m_showDetailedLogs = null!;
    #endregion
    #region screenshot
    private static ConfigEntry<Toggle> m_screenshotDeath = null!;
    private static ConfigEntry<float> m_screenshotDelay = null!;
    private static ConfigEntry<string> m_screenshotResolution = null!;
    private static ConfigEntry<Toggle> m_screenshotGif = null!;
    private static ConfigEntry<int> m_gifFPS = null!;
    private static ConfigEntry<float> m_gifDuration = null!;
    private static ConfigEntry<string> m_gifResolution = null!;
    private static ConfigEntry<KeyCode> m_selfieKey = null!;
    #endregion
    #region ai
    private static ConfigEntry<AIService> m_aiService = null!;
    private static ConfigEntry<string> m_chatGPTAPIKEY = null!;
    private static ConfigEntry<string> m_geminiAPIKEY = null!;
    private static ConfigEntry<GeminiModel> m_geminiModel = null!;
    private static ConfigEntry<string> m_deepSeekAPIKEY = null!;
    private static ConfigEntry<string> m_openRouterAPIKEY = null!;
    private static ConfigEntry<OpenRouterModel> m_openRouterModel = null!;
    private static ConfigEntry<Toggle> m_useServerKeys = null!;
    private static readonly CustomSyncedValue<string> m_serverKeys = new(ConfigSync, "RustyMods.DiscordBot.ServerKeys", "");
    private static readonly CustomSyncedValue<string> m_serverOptions = new(ConfigSync, "RustyMods.DiscordBot.ServerOptions", "");
    private static ConfigEntry<Toggle> m_allowDiscordPrompt = null!;
    #endregion

    private static ConfigEntry<Toggle> m_enableJobs = null!;
    public static bool ShowServerStart => m_serverStartNotice.Value is Toggle.On;
    public static bool ShowChat => m_chatEnabled.Value is Toggle.On;
    private static bool LogErrors => m_logErrors.Value is Toggle.On;
    public static bool ShowServerStop => m_serverStopNotice.Value is Toggle.On;
    public static bool ShowServerSave => m_serverSaveNotice.Value is Toggle.On;
    public static bool ShowOnDeath => m_deathNotice.Value is Toggle.On;
    public static bool ShowOnLogin => m_loginNotice.Value is Toggle.On;
    public static bool ShowOnLogout => m_logoutNotice.Value is Toggle.On;
    public static bool ShowEvent => m_eventNotice.Value is Toggle.On;
    public static bool ShowNewDay => m_newDayNotice.Value is Toggle.On;
    public static bool ShowCoordinates => m_coordinateDetails.Value is Toggle.On;
    private static bool ShowDetailedLogs => m_showDetailedLogs.Value is Toggle.On;
    public static bool ShowCommandUse => m_commandNotice.Value is Toggle.On;
    public static ChatDisplay ChatType => m_chatType.Value;
    public static string DiscordAdmins => m_discordAdmins.Value;
    public static void SetDiscordAdmins(string value) => m_discordAdmins.Value = value;
    public static string BOT_TOKEN => m_botToken.Value;
    public static string ChatChannelID => m_chatChannelID.Value;
    public static string CommandChannelID => m_commandChannelID.Value;
    public static string ChatWebhookURL => m_chatWebhookURL.Value;
    public static string CommandWebhookURL => m_commandWebhookURL.Value;
    public static string NoticeWebhookURL => m_notificationWebhookURL.Value;
    public static string DeathFeedWebhookURL => m_deathFeedURL.Value;
    public static bool ScreenshotDeath => m_screenshotDeath.Value is Toggle.On;
    public static bool ScreenshotGif => m_screenshotGif.Value is Toggle.On;
    public static float ScreenshotDelay => m_screenshotDelay.Value;
    public static int GIF_FPS => m_gifFPS.Value;
    public static float GIF_DURATION => m_gifDuration.Value;
    public static Resolution ScreenshotResolution => resolutions[m_screenshotResolution.Value];
    public static Resolution GifResolution => resolutions[m_gifResolution.Value];
    public static KeyCode SelfieKey => m_selfieKey.Value;
    public static AIService AIService => m_aiService.Value;
    private static string ChatGPT_KEY => m_chatGPTAPIKEY.Value;
    private static string Gemini_KEY => m_geminiAPIKEY.Value;
    private static string DeepSeek_KEY => m_deepSeekAPIKEY.Value;
    private static string OpenRouter_KEY => m_openRouterAPIKEY.Value;
    private static bool UseServerKeys => m_useServerKeys.Value is Toggle.On;
    private static OpenRouterModel OpenRouterModel => m_openRouterModel.Value;
    private static GeminiModel GeminiModel => m_geminiModel.Value;
    public static bool AllowDiscordPrompt => m_allowDiscordPrompt.Value is Toggle.On;

    public static void LogWarning(string message)
    {
        records.Log(LogLevel.Warning, message);
    }

    public static void LogDebug(string message)
    {
        records.Log(LogLevel.Debug, message);
    }

    public static void LogError(string message)
    {
        records.Log(LogLevel.Error, message);
    }

    private static readonly Dictionary<string, Resolution> resolutions = new();
    public static List<string> OnWorldStartHooks => m_startWorldHook.Value.IsNullOrWhiteSpace() ? new List<string>() : new StringListConfig(m_startWorldHook.Value).list;
    public static List<string> OnWorldSaveHooks => m_saveWebhook.Value.IsNullOrWhiteSpace() ? new List<string>() : new StringListConfig(m_saveWebhook.Value).list;
    public static List<string> OnWorldShutdownHooks => m_shutdownWebhook.Value.IsNullOrWhiteSpace() ?  new List<string>() : new StringListConfig(m_shutdownWebhook.Value).list;
    public static List<string> OnLoginHooks => m_loginWebhook.Value.IsNullOrWhiteSpace() ?  new List<string>() : new StringListConfig(m_loginWebhook.Value).list;
    public static List<string> OnLogoutHooks => m_logoutWebhook.Value.IsNullOrWhiteSpace() ? new List<string>() : new StringListConfig(m_logoutWebhook.Value).list;
    public static List<string> OnEventHooks => m_eventWebhook.Value.IsNullOrWhiteSpace() ? new List<string>() : new StringListConfig(m_eventWebhook.Value).list;
    public static List<string> OnNewDayHooks => m_newDayWebhook.Value.IsNullOrWhiteSpace() ? new List<string>() : new StringListConfig(m_newDayWebhook.Value).list;
    public static bool JobsEnabled => m_enableJobs.Value is Toggle.On;

    private static ServerKeys SyncedAIKeys = new();
    private static ServerAIOption SyncedAIOption = new();

    public static AIService GetAIServiceOption() => AIService switch
    {
        AIService.ChatGPT => string.IsNullOrEmpty(ChatGPT_KEY) && UseServerKeys ? SyncedAIOption.service : AIService.ChatGPT,
        AIService.Gemini => string.IsNullOrEmpty(Gemini_KEY) && UseServerKeys ? SyncedAIOption.service : AIService.Gemini,
        AIService.DeepSeek => string.IsNullOrEmpty(DeepSeek_KEY) && UseServerKeys ? SyncedAIOption.service : AIService.DeepSeek,
        AIService.OpenRouter => string.IsNullOrEmpty(OpenRouter_KEY) && UseServerKeys ? SyncedAIOption.service : AIService.OpenRouter,
        _ => AIService
    };
    public static OpenRouterModel GetOpenRouterOption() => string.IsNullOrEmpty(OpenRouter_KEY) && UseServerKeys ? SyncedAIOption.openRouterModel : OpenRouterModel;
    public static GeminiModel GetGeminiOption() => string.IsNullOrEmpty(Gemini_KEY) && UseServerKeys ? SyncedAIOption.geminiModel : GeminiModel;
    public static string GetChatGPTKey() => string.IsNullOrEmpty(ChatGPT_KEY) && UseServerKeys ? SyncedAIKeys.ChatGPT : ChatGPT_KEY;
    public static string GetGeminiKey() => string.IsNullOrEmpty(Gemini_KEY) && UseServerKeys ? SyncedAIKeys.Gemini : Gemini_KEY;
    public static string GetDeepSeekKey() => string.IsNullOrEmpty(DeepSeek_KEY) && UseServerKeys ? SyncedAIKeys.DeepSeek : DeepSeek_KEY;
    public static string GetOpenRouterKey() => string.IsNullOrEmpty(OpenRouter_KEY) && UseServerKeys ? SyncedAIKeys.OpenRouter : OpenRouter_KEY;

    public static readonly Record records = new();
    
    // TODO : Figure out to make sure connecting peer is connecting to the right server

    public void Awake()
    {
        Keys.Write();
        Localizer.Load();
        m_instance = this;
        _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
        _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);
        m_logErrors = config("1 - General", "Log Errors", Toggle.Off, "If on, caught errors will log to console");
        m_showDetailedLogs = config("1 - General", "Detailed Logs", Toggle.Off, "Show detailed logs");
        m_notificationWebhookURL = config("2 - Notifications", "Webhook URL", "", "Set webhook to receive notifications, like server start, stop, save etc...");
        m_serverStartNotice = config("2 - Notifications", "Startup", Toggle.On, "If on, bot will send message when server is starting");
        m_serverStopNotice = config("2 - Notifications", "Shutdown", Toggle.On, "If on, bot will send message when server is shutting down");
        m_serverSaveNotice = config("2 - Notifications", "Saving", Toggle.On, "If on, bot will send message when server is saving");
        m_loginNotice = config("2 - Notifications", "Login", Toggle.On, "If on, bot will send message when player logs in");
        m_logoutNotice = config("2 - Notifications", "Logout", Toggle.On, "If on, bot will send message when player logs out");
        m_eventNotice = config("2 - Notifications", "Random Events", Toggle.On, "If on, bot will send message when random event starts");
        m_newDayNotice = config("2 - Notifications", "New Day", Toggle.Off, "If on, bot will send message when a new day begins");
        m_coordinateDetails = config("2 - Notifications", "Show Coordinates", Toggle.On, "If on, coordinates will be added to login/logout notifications");
        m_commandNotice = config("2 - Notifications", "Show Command Use", Toggle.Off, "If on, bot will send message when a player uses a cheat terminal command");
        m_chatWebhookURL = config("3 - Chat", "Webhook URL", "", "Set discord webhook to display chat messages");
        m_chatChannelID = config("3 - Chat", "Channel ID", "", "Set channel ID to monitor for messages");
        m_chatEnabled = config("3 - Chat", "Enabled", Toggle.On, "If on, bot will send message when player shouts and monitor discord for messages");
        m_chatType = config("3 - Chat", "Display As", ChatDisplay.Player, "Set how chat messages appear, if Player, message sent by player, else sent by bot with a prefix that player is saying");
        
        m_commandWebhookURL = config("4 - Commands", "Webhook URL", "", "Set discord webhook to display feedback messages from commands");
        m_commandChannelID = config("4 - Commands", "Channel ID", "", "Set channel ID to monitor for input commands");
        m_discordAdmins = config("4 - Commands", "Discord Admin", "", new ConfigDescription("List of discord admins, who can run commands", null, new ConfigurationManagerAttributes()
        {
            CustomDrawer = StringListConfig.Draw
        }));
        m_enableJobs = config("4 - Commands", "Jobs", Toggle.On, "If on, jobs are enabled");

        m_botToken = config("5 - Setup", "BOT TOKEN", "", "Add bot token here, server only", false);
        
        m_deathNotice = config("6 - Death Feed", "Enabled", Toggle.On, "If on, bot will send message when player dies");
        m_deathFeedURL = config("6 - Death Feed", "Webhook URL", "", "Set webhook to receive death feed messages");
        m_screenshotDeath = config("6 - Death Feed", "Screenshot", Toggle.On, "If on, bot will post screenshot of death", false);
        m_screenshotDelay = config("6 - Death Feed", "Screenshot Delay", 0.3f, new ConfigDescription("Set delay", new AcceptableValueRange<float>(0.1f, 5f)), false);

        Resolution med = new(800, 600);
        Resolution medium = new(960, 540);
        Resolution hd = new(1280, 720);
        Resolution super = new(1920, 1080);
        
        m_screenshotResolution = config("6 - Death Feed", "Screenshot Resolution", medium.ToString(),
            new ConfigDescription("Set resolution",
                new AcceptableValueList<string>(
                    med.ToString(),
                    medium.ToString(),
                    hd.ToString(),
                    super.ToString()
                    )), 
            false);
        m_screenshotGif = config("6 - Death Feed", "Screenshot GIF", Toggle.On, "If on, bot will post gif of death", false);
        m_gifFPS = config("6 - Death Feed", "GIF FPS", 30, new ConfigDescription("Set frames per second", new AcceptableValueRange<int>(1, 30)), false);
        m_gifDuration = config("6 - Death Feed", "GIF Record Duration", 3f, new ConfigDescription("Set recording duration for gif, in seconds", new AcceptableValueRange<float>(1f, 3f)), false);

        Resolution thumbnail = new(256, 144);
        Resolution small = new(320, 180);
        Resolution standard = new(480, 270);
        Resolution banner = new(640, 360);
        
        m_gifResolution = config("6 - Death Feed", "GIF Resolution", standard.ToString(),
            new ConfigDescription("Set resolution",
                new AcceptableValueList<string>(
                    thumbnail.ToString(),
                    small.ToString(), 
                    standard.ToString(),
                    banner.ToString()
                )), 
            false);

        m_selfieKey = config("1 - General", "Selfie", KeyCode.None, "Hotkey to take selfie and send to discord", false);

        m_startWorldHook = config("7 - Webhooks", "On World Start", "", new ConfigDescription("If empty, will use default notification webhook", null, new ConfigurationManagerAttributes() { CustomDrawer = StringListConfig.Draw }));
        m_saveWebhook = config("7 - Webhooks", "On World Save", "", new ConfigDescription("If empty, will use default notification webhook", null, new ConfigurationManagerAttributes(){ CustomDrawer = StringListConfig.Draw}));
        m_shutdownWebhook = config("7 - Webhooks", "On World Shutdown", "", new ConfigDescription("If empty, will use default notification webhook", null, new ConfigurationManagerAttributes(){ CustomDrawer = StringListConfig.Draw}));
        m_loginWebhook = config("7 - Webhooks", "On Login",  "", new ConfigDescription("If empty, will use default notification webhook", null, new ConfigurationManagerAttributes(){ CustomDrawer = StringListConfig.Draw}));
        m_logoutWebhook = config("7 - Webhooks", "On Logout", "", new ConfigDescription("If empty, will use default notification webhook", null, new ConfigurationManagerAttributes(){ CustomDrawer = StringListConfig.Draw}));
        m_eventWebhook = config("7 - Webhooks", "On Event", "", new ConfigDescription("If empty, will use default notification webhook", null, new ConfigurationManagerAttributes(){ CustomDrawer = StringListConfig.Draw}));
        m_newDayWebhook = config("7 - Webhooks", "On New Day", "", new ConfigDescription("If empty, will use default notification webhook", null, new ConfigurationManagerAttributes() { CustomDrawer = StringListConfig.Draw }));

        m_aiService = config("8 - AI", "Provider", AIService.Gemini, "Set which Artificial Intelligence API to use", false);
        m_chatGPTAPIKEY = config("8 - AI", "ChatGPT", "", "Set ChatGPT key", false);
        m_geminiAPIKEY = config("8 - AI", "Gemini", "", "Set Gemini key", false);
        m_deepSeekAPIKEY = config("8 - AI", "DeepSeek", "", "Set DeepSeek key", false);
        m_openRouterAPIKEY = config("8 - AI", "OpenRouter", "", "Set OpenRouter key", false);
        m_openRouterModel = config("8 - AI", "OpenRouter Model", OpenRouterModel.Claude3_5Sonnet, "Set OpenRouter Model", false);
        m_useServerKeys = config("8 - AI", "Use Server Keys", Toggle.On, "If on and client does not have API Keys, will try to use server's API Keys");
        m_geminiModel = config("8 - AI", "Gemini Model", GeminiModel.Flash2_0, "Set Gemini Model", false);
        m_allowDiscordPrompt = config("8 - AI", "Discord Prompt", Toggle.Off, "If on, users can prompt server's AI using command !prompt");
        m_chatGPTAPIKEY.SettingChanged += (_, _) => UpdateServerAIKeys();
        m_geminiAPIKEY.SettingChanged += (_, _) => UpdateServerAIKeys();
        m_deepSeekAPIKEY.SettingChanged += (_, _) => UpdateServerAIKeys();
        m_openRouterAPIKEY.SettingChanged += (_, _) => UpdateServerAIKeys();
        m_aiService.SettingChanged += (_, _) => UpdateServerAIOption();
        m_openRouterModel.SettingChanged += (_, _) => UpdateServerAIOption();
        m_geminiModel.SettingChanged += (_, _) => UpdateServerAIOption();
        m_serverKeys.ValueChanged += () => SyncedAIKeys = new ServerKeys(m_serverKeys.Value);
        m_serverOptions.ValueChanged += () => SyncedAIOption = new ServerAIOption(m_serverOptions.Value);
        DiscordCommands.Setup();
        DeathQuips.Setup();

        Assembly assembly = Assembly.GetExecutingAssembly();
        _harmony.PatchAll(assembly);
        SetupWatcher();
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    private static class ZNet_Awake_Patch
    {
        [UsedImplicitly]
        private static void Postfix(ZNet __instance)
        {
            m_instance.gameObject.GetOrAddComponent<Discord>();
            m_instance.gameObject.GetOrAddComponent<Screenshot>();
            m_instance.gameObject.GetOrAddComponent<Recorder>();
            m_instance.gameObject.GetOrAddComponent<ChatAI>();
            
            if (!__instance.IsServer()) return;
            m_instance.gameObject.GetOrAddComponent<DiscordGatewayClient>();
            m_instance.gameObject.GetOrAddComponent<JobManager>();
            UpdateServerAIKeys();
            UpdateServerAIOption();
        }
    }

    private static void UpdateServerAIKeys()
    {
        if (!(ZNet.instance?.IsServer() ?? false)) return;
        ServerKeys serverKeys = new ServerKeys(ChatGPT_KEY, Gemini_KEY, DeepSeek_KEY, OpenRouter_KEY);
        m_serverKeys.Value = serverKeys.ToString();
        records.Log(LogLevel.Info, "Updating server AI API keys");
    }

    private static void UpdateServerAIOption()
    {
        if (!(ZNet.instance?.IsServer() ?? false)) return;
        ServerAIOption option = new(AIService, OpenRouterModel, GeminiModel);
        m_serverOptions.Value = option.ToString();
        records.Log(LogLevel.Info, "Updating server AI options");
    }

    private void OnDestroy()
    {
        Config.Save();
        records.Write();
    }

    public class StringListConfig
    {
        public readonly List<string> list;
        public StringListConfig(List<string> items) => list = items;
        public StringListConfig(string items) => list = items.Split(',').ToList();
        public static void Draw(ConfigEntryBase cfg)
        {
            bool locked = cfg.Description.Tags
                .Select(a =>
                    a.GetType().Name == "ConfigurationManagerAttributes"
                        ? (bool?)a.GetType().GetField("ReadOnly")?.GetValue(a)
                        : null).FirstOrDefault(v => v != null) ?? false;
            bool wasUpdated = false;
            List<string> strings = new();
            GUILayout.BeginVertical();
            foreach (var prefab in new StringListConfig((string)cfg.BoxedValue).list)
            {
                GUILayout.BeginHorizontal();
                var prefabName = prefab;
                var nameField = GUILayout.TextField(prefab);
                if (nameField != prefab && !locked)
                {
                    wasUpdated = true;
                    prefabName = nameField;
                }

                if (GUILayout.Button("x", new GUIStyle(GUI.skin.button) { fixedWidth = 21 }) && !locked)
                {
                    wasUpdated = true;
                }
                else
                {
                    strings.Add(prefabName);
                }

                if (GUILayout.Button("+", new GUIStyle(GUI.skin.button) { fixedWidth = 21 }) && !locked)
                {
                    strings.Add("");
                    wasUpdated = true;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            if (wasUpdated)
            {
                cfg.BoxedValue = new StringListConfig(strings).ToString();
            }
        }

        public override string ToString() => string.Join(",", list);
    }

    public class Resolution
    {
        public readonly int width;
        public readonly int height;

        public Resolution(int width, int height)
        {
            this.width = width;
            this.height = height;
            resolutions[ToString()] = this;
        }

        public sealed override string ToString() => $"{width}x{height}";
    }

    private void SetupWatcher()
    {
        FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
        watcher.Changed += ReadConfigValues;
        watcher.Created += ReadConfigValues;
        watcher.Renamed += ReadConfigValues;
        watcher.IncludeSubdirectories = true;
        watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        watcher.EnableRaisingEvents = true;
    }

    private void ReadConfigValues(object sender, FileSystemEventArgs e)
    {
        if (!File.Exists(ConfigFileFullPath)) return;
        try
        {
            DiscordBotLogger.LogDebug("ReadConfigValues called");
            Config.Reload();
        }
        catch
        {
            DiscordBotLogger.LogError($"There was an issue loading your {ConfigFileName}");
            DiscordBotLogger.LogError("Please check your config entries for spelling and format!");
        }
    }


    private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
    {
        ConfigDescription extendedDescription =
            new(
                description.Description +
                (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                description.AcceptableValues, description.Tags);
        ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);

        SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
        syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

        return configEntry;
    }

    public ConfigEntry<T> config<T>(string group, string name, T value, string description,
        bool synchronizedSetting = true)
    {
        return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
    }
    
    public class ConfigurationManagerAttributes
    {
        [UsedImplicitly] public int? Order = null!;
        [UsedImplicitly] public bool? Browsable = null!;
        [UsedImplicitly] public string? Category = null!;
        [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer = null!;
    }
    
    public class ServerKeys
    {
        public readonly string ChatGPT = "";
        public readonly string Gemini = "";
        public readonly string DeepSeek = "";
        public readonly string OpenRouter = "";
        public ServerKeys(){}
        public ServerKeys(string ChatGPT, string Gemini, string DeepSeek, string OpenRouter)
        {
            this.ChatGPT = ChatGPT;
            this.Gemini = Gemini;
            this.DeepSeek = DeepSeek;
            this.OpenRouter = OpenRouter;
        }
        public ServerKeys(string keys)
        {
            string[] parts = keys.Split(';');
            if (parts.Length < 4) return;
            ChatGPT = parts[0];
            Gemini = parts[1];
            DeepSeek = parts[2];
            OpenRouter = parts[3];
        }

        public override string ToString() => $"{ChatGPT};{Gemini};{DeepSeek};{OpenRouter}";
    }

    public class ServerAIOption
    {
        public readonly AIService service = AIService.Gemini;
        public readonly OpenRouterModel openRouterModel = OpenRouterModel.Claude3_5Sonnet;
        public readonly GeminiModel geminiModel = GeminiModel.Flash2_0;
        public ServerAIOption(){}
        public ServerAIOption(string config)
        {
            var parts = config.Split(';');
            if (parts.Length < 3) return;
            Enum.TryParse(parts[0], true, out service);
            Enum.TryParse(parts[1], true, out openRouterModel);
            Enum.TryParse(parts[2], true, out geminiModel);
        }
        public ServerAIOption(AIService service, OpenRouterModel openRouterModel, GeminiModel geminiModel)
        {
            this.service = service;
            this.openRouterModel = openRouterModel;
            this.geminiModel = geminiModel;
        }

        public override string ToString() => $"{service};{openRouterModel};{geminiModel}";
    }

    public class Record
    {
        private readonly List<string> logs = new();
        public void Log(LogLevel level, string log)
        {
            logs.Add($"[{DateTime.Now:HH:mm:ss}][{level}]: {log}");
            switch (level)
            {
                case LogLevel.Error:
                    if (LogErrors) DiscordBotLogger.Log(level, log);
                    break;
                default:
                    if (ShowDetailedLogs) DiscordBotLogger.Log(level, log);
                    break;
            }
        }
        public void Write()
        {
            directory.WriteAllLines("RustyMods.DiscordBot.log", logs);
        }
    }
}
