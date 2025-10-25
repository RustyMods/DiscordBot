using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using LocalizationManager;
using ServerSync;
using UnityEngine;

namespace DiscordBot
{
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
        internal const string ModVersion = "1.2.2";
        internal const string Author = "RustyMods";
        private const string ModGUID = Author + "." + ModName;
        private static readonly string ConfigFileName = ModGUID + ".cfg";
        private static readonly string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource DiscordBotLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };
        private static ConfigEntry<Toggle> _serverConfigLocked = null!;

        public static readonly Dir directory = new(Paths.ConfigPath, "DiscordBot");
        
        public static DiscordBotPlugin m_instance = null!;
        
        private static ConfigEntry<string> m_notificationWebhookURL = null!;
        private static ConfigEntry<Toggle> m_serverStartNotice = null!;
        private static ConfigEntry<Toggle> m_serverStopNotice = null!;
        private static ConfigEntry<Toggle> m_serverSaveNotice = null!;
        private static ConfigEntry<Toggle> m_deathNotice = null!;
        private static ConfigEntry<Toggle> m_loginNotice = null!;
        private static ConfigEntry<Toggle> m_logoutNotice = null!;

        private static ConfigEntry<string> m_chatWebhookURL = null!;
        private static ConfigEntry<string> m_chatChannelID = null!;
        private static ConfigEntry<Toggle> m_chatEnabled = null!;
        private static ConfigEntry<ChatDisplay> m_chatType = null!;

        private static ConfigEntry<string> m_commandWebhookURL = null!;
        private static ConfigEntry<string> m_commandChannelID = null!;

        private static ConfigEntry<string> m_deathFeedURL = null!;

        private static ConfigEntry<string> m_discordAdmins = null!;
        private static ConfigEntry<Toggle> m_logErrors = null!;

        private static ConfigEntry<string> m_botToken = null!;
        private static ConfigEntry<Toggle> m_screenshotDeath = null!;
        private static ConfigEntry<float> m_screenshotDelay = null!;
        private static ConfigEntry<string> m_screenshotResolution = null!;
        private static ConfigEntry<int> m_screenshotDepth = null!;
        
        private static ConfigEntry<Toggle> m_screenshotGif = null!;
        private static ConfigEntry<int> m_gifFPS = null!;
        private static ConfigEntry<float> m_gifDuration = null!;
        private static ConfigEntry<string> m_gifResolution = null!;

        private static ConfigEntry<KeyCode> m_selfieKey = null!;
        
        public static bool ShowServerStart => m_serverStartNotice.Value is Toggle.On;
        public static bool ShowChat => m_chatEnabled.Value is Toggle.On;
        public static bool LogErrors => m_logErrors.Value is Toggle.On;
        public static bool ShowServerStop => m_serverStopNotice.Value is Toggle.On;
        public static bool ShowServerSave => m_serverSaveNotice.Value is Toggle.On;
        public static bool ShowOnDeath => m_deathNotice.Value is Toggle.On;
        public static bool ShowOnLogin => m_loginNotice.Value is Toggle.On;
        public static bool ShowOnLogout => m_logoutNotice.Value is Toggle.On;
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
        public static int ScreenshotDepth => m_screenshotDepth.Value;
        
        public static KeyCode SelfieKey => m_selfieKey.Value;
        
        public static void LogWarning(string message) => DiscordBotLogger.LogWarning(message);
        public static void LogDebug(string message) => DiscordBotLogger.LogDebug(message);

        private static readonly Dictionary<string, Resolution> resolutions = new();

        
        // TODO : Figure out to make sure connecting peer is connecting to the right server

        public void Awake()
        {
            Keys.Write();
            Localizer.Load();
            m_instance = this;
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);
            m_logErrors = config("1 - General", "Log Errors", Toggle.Off, "If on, errors will log to console as warnings");
            
            m_notificationWebhookURL = config("2 - Notifications", "Webhook URL", "", "Set webhook to receive notifications, like server start, stop, save etc...");
            m_serverStartNotice = config("2 - Notifications", "Startup", Toggle.On, "If on, bot will send message when server is starting");
            m_serverStopNotice = config("2 - Notifications", "Shutdown", Toggle.On, "If on, bot will send message when server is shutting down");
            m_serverSaveNotice = config("2 - Notifications", "Saving", Toggle.On, "If on, bot will send message when server is saving");
            m_loginNotice = config("2 - Notifications", "Login", Toggle.On, "If on, bot will send message when player logs in");
            m_logoutNotice = config("2 - Notifications", "Logout", Toggle.On, "If on, bot will send message when player logs out");

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
            m_screenshotDepth = config("6 - Death Feed", "Screenshot Depth", 32, new ConfigDescription("Set depth", new AcceptableValueList<int>(0, 16, 24, 32)), false);
            m_screenshotResolution.SettingChanged += (_, _) => Screenshot.instance?.OnResolutionChange();
            m_screenshotDepth.SettingChanged += (_, _) => Screenshot.instance?.OnResolutionChange();
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
            m_gifResolution.SettingChanged += (_, _) => Recorder.instance?.OnGifResolutionChange();

            m_selfieKey = config("1 - General", "Selfie", KeyCode.None, "Hotkey to take selfie and send to discord", false);
            
            DiscordCommands.Setup();
            DeathQuips.Setup();

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
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
            //var configEntry = Config.Bind(group, name, value, description);

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
    }
}