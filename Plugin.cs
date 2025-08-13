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
using ServerSync;
using UnityEngine;

namespace DiscordBot
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class DiscordBotPlugin : BaseUnityPlugin
    {
        internal const string ModName = "DiscordBot";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "RustyMods";
        private const string ModGUID = Author + "." + ModName;
        private static readonly string ConfigFileName = ModGUID + ".cfg";
        private static readonly string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource DiscordBotLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };
        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        public enum Toggle { On = 1, Off = 0 }

        public static DiscordBotPlugin m_instance = null!;
        
        public static ConfigEntry<string> m_notificationWebhookURL = null!;
        public static ConfigEntry<Toggle> m_serverStartNotice = null!;
        public static ConfigEntry<Toggle> m_serverStopNotice = null!;
        public static ConfigEntry<Toggle> m_serverSaveNotice = null!;
        public static ConfigEntry<Toggle> m_deathNotice = null!;
        public static ConfigEntry<Toggle> m_newPlayerNotice = null!;

        public static ConfigEntry<string> m_chatWebhookURL = null!;
        public static ConfigEntry<string> m_chatChannelID = null!;
        public static ConfigEntry<Toggle> m_chatEnabled = null!;

        public static ConfigEntry<string> m_commandWebhookURL = null!;
        public static ConfigEntry<string> m_commandChannelID = null!;
        public static ConfigEntry<int> m_pollInterval = null!;
        public static ConfigEntry<Toggle> m_poll = null!;

        public static ConfigEntry<string> m_discordAdmins = null!;

        public void Awake()
        {
            m_instance = this;
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);
            m_pollInterval = config("1 - General", "Poll Interval", 5, new ConfigDescription("Set interval between check for messages in discord, in seconds", new AcceptableValueRange<int>(5, 300)));
            m_poll = config("1 - General", "Poll", Toggle.On, "If on, plugin will start polling discord for messages, turn off, change configs, then turn on again to reset");

            m_notificationWebhookURL = config("2 - Notifications", "Webhook URL", "", "Set webhook to receive notifications, like server start, stop, save etc...");
            m_serverStartNotice = config("2 - Notifications", "Startup", Toggle.On, "If on, bot will send message when server is starting");
            m_serverStopNotice = config("2 - Notifications", "Shutdown", Toggle.On, "If on, bot will send message when server is shutting down");
            m_serverSaveNotice = config("2 - Notifications", "Saving", Toggle.On, "If on, bot will send message when server is saving");
            m_deathNotice = config("2 - Notifications", "On Death", Toggle.On, "If on, bot will send message when player dies");
            m_newPlayerNotice = config("2 - Notifications", "New Connection", Toggle.On,
                "If on, bot will send message when new player connects");

            m_chatWebhookURL = config("3 - Chat", "Webhook URL", "https://discord.com/api/webhooks/1404119063046652035/OqBFopk29Cku3_4TiLCJVwaagkyLVPcE1m1OoCJw2i9pppKkOe1BMoHH7vHh_RfWy1d3", "Set discord webhook to display chat messages");
            m_chatChannelID = config("3 - Chat", "Channel ID", "983975176590983209", "Set channel ID to monitor for messages");
            m_chatEnabled = config("3 - Chat", "Enabled", Toggle.On, "If on, bot will send message when player shouts and monitor discord for messages");
            
            m_commandWebhookURL = config("4 - Commands", "Webhook URL", "https://discord.com/api/webhooks/1404941903144685779/gc8DFwfIO5eUnxzoJ1Dqsi-iX68GLUMWzk_7Au5YD6rZhD7kFsx2BxLdj7tKfB1qWuYN", "Set discord webhook to display feedback messages from commands");
            m_commandChannelID = config("4 - Commands", "Channel ID", "1106369492512165898", "Set channel ID to monitor for input commands");
            m_discordAdmins = config("4 - Commands", "Discord Admin", "", new ConfigDescription("List of discord admins, who can run commands", null, new ConfigurationManagerAttributes()
            {
                CustomDrawer = StringListConfig.Draw
            }));
            
            DiscordCommands.Setup();

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private class StringListConfig
        {
            public readonly List<string> list = new();

            public StringListConfig(){}
            public StringListConfig(params string[] items) => list = items.ToList();
            public StringListConfig(List<string> items) => list = items;
            
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

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
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