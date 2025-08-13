using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace DiscordBot;

public static class DiscordCommands
{
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
    private static class ZNet_OnNewConnection_Patch
    {
        private static void Postfix(ZNetPeer peer)
        {
            // Register RPC to clients to receive message from server
            peer.m_rpc.Register<ZPackage>(nameof(RPC_BotToClient), RPC_BotToClient);
        }
    }
    public static readonly Dictionary<string, DiscordCommand> m_commands = new();
    private static readonly Dictionary<string, string> m_descriptions = new();

    public static void Setup()
    {
        var help = new DiscordCommand("help", "List of commands", _ =>
        {
            Discord.instance.SendTableEmbed(DiscordBotPlugin.Webhook.Commands, "List of available commands", m_descriptions, ZNet.instance.GetWorldName());
        });
        var listAdmins = new DiscordCommand("listadmins", "List of discord admins registered to plugin", _ =>
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var username in new DiscordBotPlugin.StringListConfig(DiscordBotPlugin.m_discordAdmins.Value).list)
            {
                stringBuilder.Append($"`{username}`\n");
            }
            Discord.instance.SendEmbedMessage(DiscordBotPlugin.Webhook.Commands, "List of discord usernames who can use commands:", stringBuilder.ToString(), ZNet.instance.GetWorldName());
        });
        var addDiscordAdmin = new DiscordCommand("addadmin",
            "Adds discord username to admin list, to enable using commands, `<string:Username>`",
            args =>
            {
                if (args.Length < 2) return;
                var userName = args[1].Trim();
                var list = new DiscordBotPlugin.StringListConfig(DiscordBotPlugin.m_discordAdmins.Value);
                list.list.Add(userName);
                DiscordBotPlugin.m_discordAdmins.Value = list.ToString();
                listAdmins.Run(new []{"listadmins"});
            }, adminOnly: true);
        var removeDiscordAdmin = new DiscordCommand("removeadmin",
            "Remove discord username from admin list, to disable using commands, `<string:Username>`",
            args =>
            {
                var userName = args[1].Trim();
                var list = new DiscordBotPlugin.StringListConfig(DiscordBotPlugin.m_discordAdmins.Value);
                list.list.Remove(userName);
                DiscordBotPlugin.m_discordAdmins.Value = list.ToString();
                listAdmins.Run(new []{"listadmins"});
            }, adminOnly: true);
        var listEnv = new DiscordCommand("listenv", "List of available environments", _ =>
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var env in EnvMan.instance.m_environments) stringBuilder.Append($"`{env.m_name}`\n");
            Discord.instance.SendEmbedMessage(DiscordBotPlugin.Webhook.Commands, "List of available environments:", stringBuilder.ToString(), ZNet.instance.GetWorldName());
        });
        var forceEnv = new DiscordCommand("env", "Force environment on all players", args =>
            {
                if (args.Length < 2) return;
                var environment = args[1].Trim();
                if (EnvMan.instance.GetEnv(environment) is { } env)
                {
                    var pkg = new ZPackage();
                    pkg.Write("env");
                    pkg.Write(environment);
                    foreach(var peer in ZNet.instance.GetPeers()) peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
                    if (Player.m_localPlayer)
                    {
                        EnvMan.instance.m_debugEnv = environment;
                    }
                }
                else
                {
                    Discord.instance.SendMessage(DiscordBotPlugin.Webhook.Commands, ZNet.instance.GetWorldName(),
                        "Failed to find environment: " + environment);
                }
            }, pkg =>
            {
                string environment = pkg.ReadString();
                EnvMan.instance.m_debugEnv = environment;
            },
            adminOnly: true);
        var resetEnv = new DiscordCommand("resetenv", "Reset environment on all players", _ =>
            {
                var pkg = new ZPackage();
                pkg.Write("resetenv");
                foreach(var peer in ZNet.instance.GetPeers()) peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
                if (Player.m_localPlayer) EnvMan.instance.m_debugEnv = "";
            }, _ => EnvMan.instance.m_debugEnv = "",
            adminOnly: true);
        var listPlayers = new DiscordCommand("listplayers", "List of active players", _ =>
        {
            Dictionary<string, string> content = new Dictionary<string, string>();
            foreach (var player in ZNet.instance.m_players)
            {
                content[player.m_name] = $"Position: `{player.m_position.x} {player.m_position.y} {player.m_position.z}`";
            }
            Discord.instance.SendTableEmbed(DiscordBotPlugin.Webhook.Commands, "List of active players", content, ZNet.instance.GetWorldName());
        }, adminOnly: true);

        var kick = new DiscordCommand("kick", "Kicks player from server, `<string:PlayerName>`", args =>
        {
            if (args.Length < 2) return;
            var playerName = args[1].Trim();
            if (ZNet.instance.GetPeerByPlayerName(playerName) is not { } peer)
            {
                Discord.instance.SendMessage(DiscordBotPlugin.Webhook.Commands, ZNet.instance.GetWorldName(), "Failed to find " + playerName);
            }
            else
            {
                ZNet.instance.Disconnect(peer);
                Discord.instance.SendMessage(DiscordBotPlugin.Webhook.Commands, ZNet.instance.GetWorldName(), $"Kicked {playerName} from server !");
            }
        }, adminOnly:true);

        var give = new DiscordCommand("give", "Adds item directly into player inventory, `<string:PlayerName>` `<string:ItemName>` `<int:Stack>` `<int?:Quality>` `<int?:Variant>`", args =>
        {
            if (args.Length < 4) return;
            var playerName = args[1].Trim();
            var itemName = args[2].Trim();
            var amount = int.TryParse(args[3].Trim(), out int stack) ? stack : 1;
            int quality = args.Length > 4 ? int.TryParse(args[4].Trim(), out int level) ? level : 1 : 1;
            int variant = args.Length > 5 ? int.TryParse(args[5].Trim(), out int type) ? type : 0 : 0;
            
            if (Player.m_localPlayer && Player.m_localPlayer.GetPlayerName() == playerName)
            {
                GiveItem(itemName, amount,quality, variant);
            }
            else if (ZNet.instance.GetPeerByPlayerName(playerName) is { } peer)
            {
                var pkg = new ZPackage();
                pkg.Write("give");
                pkg.Write(itemName);
                pkg.Write(amount);
                pkg.Write(quality);
                pkg.Write(variant);
                
                peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
            }
            else
            {
                Discord.instance.SendMessage(DiscordBotPlugin.Webhook.Commands, ZNet.instance.GetWorldName(), "Failed to find " + playerName);
            }
        }, pkg =>
        {
            var itemName = pkg.ReadString();
            var amount = pkg.ReadInt();
            var quality = pkg.ReadInt();
            var variant = pkg.ReadInt();
                
            GiveItem(itemName, amount,quality,variant);
        }, adminOnly:true);

        var teleportTo = new DiscordCommand("teleport", "Teleport player to location, `<string:PlayerName>` `<string:bed>` or `<string:OtherPlayerName>` or `<float:x>` `<float:y>` `<float:z>`", args =>
        {
            if (args.Length < 5) return;
            var playerName = args[1].Trim();
            var otherPlayerName = args[2].Trim();

            if (otherPlayerName == "bed")
            {
                if (Player.m_localPlayer && Player.m_localPlayer.GetPlayerName() == playerName)
                {
                    Player.m_localPlayer.TeleportTo(Game.instance.GetPlayerProfile().GetCustomSpawnPoint(), Quaternion.identity, true);
                }
                else if (ZNet.instance.GetPeerByPlayerName(playerName) is { } peer)
                {
                    ZPackage pkg = new ZPackage();
                    pkg.Write("teleport");
                    pkg.Write("bed");
                    peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
                }
                else
                {
                    Discord.instance.SendMessage(DiscordBotPlugin.Webhook.Commands, ZNet.instance.GetWorldName(), "Failed to find " + playerName);
                }
            }
            else
            {
                Vector3 pos;
                if (Player.m_localPlayer && Player.m_localPlayer.GetPlayerName() == otherPlayerName)
                {
                    pos = Player.m_localPlayer.transform.position;
                }
                else if (ZNet.instance.GetPeerByPlayerName(otherPlayerName) is { } otherPeer)
                {
                    pos = otherPeer.m_refPos;
                }
                else if (float.TryParse(args[2].Trim(), out float x) && float.TryParse(args[3].Trim(), out float y) && float.TryParse(args[4].Trim(), out float z))
                {
                    pos = new Vector3(x, y, z);
                }
                else
                {
                    Discord.instance.SendMessage(DiscordBotPlugin.Webhook.Commands, ZNet.instance.GetWorldName(), "Incorrect teleport command format");
                    return;
                }
                
                if (Player.m_localPlayer && Player.m_localPlayer.GetPlayerName() == playerName)
                {
                    Player.m_localPlayer.TeleportTo(pos, Quaternion.identity, true);
                }
                else if (ZNet.instance.GetPeerByPlayerName(playerName) is { } peer)
                {
                    var pkg = new ZPackage();
                    pkg.Write("teleport");
                    pkg.Write("vector");
                    pkg.Write(pos);
                    peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
                }
                else
                {
                    Discord.instance.SendMessage(DiscordBotPlugin.Webhook.Commands, ZNet.instance.GetWorldName(), "Failed to find " + playerName);
                }
            }
        }, pkg =>
        {
            string type = pkg.ReadString();
            switch (type)
            {
                case "bed":
                    var bedPoint = Game.instance.GetPlayerProfile().GetCustomSpawnPoint();
                    Player.m_localPlayer.TeleportTo(bedPoint, Quaternion.identity, true);
                    break;
                case "vector":
                    Vector3 pos = pkg.ReadVector3();
                    Player.m_localPlayer.TeleportTo(pos, Quaternion.identity, true);
                    break;
            }
        },adminOnly:true);

        var spawn = new DiscordCommand("spawn", "spawns prefab at location, `<string:PrefabName>` `<int:Level>` `<string:PlayerName>` or `<float:x>` `<float:y>` `<float:z>`", args =>
        {
            if (args.Length < 4) return;
            var prefabName = args[1].Trim();
            var level = int.TryParse(args[2].Trim(), out int lvl) ? lvl : 1;

            if (args.Length == 6)
            {
                if (!float.TryParse(args[3].Trim(), out float x) || !float.TryParse(args[4].Trim(), out float y) || !float.TryParse(args[5].Trim(), out float z))
                {
                    Discord.instance.SendMessage(DiscordBotPlugin.Webhook.Commands, ZNet.instance.GetWorldName(), "Incorrect spawn command format");
                }
                else if (!ZoneSystem.instance.IsZoneLoaded(new Vector3(x, y, z)))
                {
                    Discord.instance.SendMessage(DiscordBotPlugin.Webhook.Commands, ZNet.instance.GetWorldName(), "Failed to spawn, location zone is not loaded!");
                }
                else if (!Spawn(prefabName, level, new Vector3(x, y, z)))
                {
                    Discord.instance.SendMessage(DiscordBotPlugin.Webhook.Commands, ZNet.instance.GetWorldName(), "Failed to spawn: " + prefabName);
                }
            }
            else
            {
                var playerName = args[3].Trim();

                if (Player.m_localPlayer && Player.m_localPlayer.GetPlayerName() == playerName)
                {
                    if (!Spawn(prefabName, level, Player.m_localPlayer.transform.position))
                    {
                        Discord.instance.SendMessage(DiscordBotPlugin.Webhook.Commands, ZNet.instance.GetWorldName(), "Failed to spawn: " + prefabName);
                    }
                }
                else if (ZNet.instance.GetPeerByPlayerName(playerName) is {} peer)
                {
                    var pkg = new ZPackage();
                    pkg.Write("spawn");
                    pkg.Write(prefabName);
                    pkg.Write(level);
                    peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
                }
                else
                {
                    Discord.instance.SendMessage(DiscordBotPlugin.Webhook.Commands, ZNet.instance.GetWorldName(), "Failed to find " + playerName);
                }
            }
            
        }, pkg =>
        {
            string creatureName = pkg.ReadString();
            int level = pkg.ReadInt();
            Spawn(creatureName, level, Player.m_localPlayer.transform.position);
        },adminOnly: true);

        var save = new DiscordCommand("save", "Save player profiles and world", _ =>
        {
            ZNet.instance.Save(true, true, true);
        }, adminOnly: true);

        var shutDown = new DiscordCommand("shutdown", "Save player profiles, save world and shutdown, bot cannot start server", _ =>
        {
            ZNet.instance.SaveOtherPlayerProfiles();
            ZNet.instance.Shutdown();
        }, adminOnly: true);

        var message = new DiscordCommand("message", "Broadcast message to all players which shows up center of screen", args =>
        {
            var message = string.Join(" ", args.Skip(1));
            var pkg = new ZPackage();
            pkg.Write("message");
            pkg.Write(message);
            foreach(var peer in ZNet.instance.GetPeers()) peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
            if (Player.m_localPlayer) Player.m_localPlayer.Message(MessageHud.MessageType.Center, message);
        }, pkg =>
        {
            string message = pkg.ReadString();
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, message);
        }, adminOnly: true);

        var setKey = new DiscordCommand("setkey", "Set global key", args =>
        {
            if (args.Length < 2) return;
            var key = args[1];
            if (!Enum.TryParse(key, out GlobalKeys globalKey))
            {
                Discord.instance.SendMessage(DiscordBotPlugin.Webhook.Commands, ZNet.instance.GetWorldName(), "Failed to find global key: " + key);
                return;
            }
            ZoneSystem.instance.SetGlobalKey(globalKey);
        }, adminOnly: true);

        var eventList = new DiscordCommand("listevents", "List of available event names", _ =>
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var randomEvent in RandEventSystem.instance.m_events)
            {
                stringBuilder.Append($"{randomEvent.m_name}\n");
            }
            Discord.instance.SendEmbedMessage(DiscordBotPlugin.Webhook.Commands, "Available events:", stringBuilder.ToString(), ZNet.instance.GetWorldName());
        }, adminOnly: true);

        var startEvent = new DiscordCommand("event", "Starts a event on a player, `<string:EventName>` `<string:PlayerName>`", args =>
        {
            if (args.Length < 3) return;
            var eventName = args[1].Trim();
            var playerName = args[2].Trim();

            Vector3 pos;
            if (Player.m_localPlayer && Player.m_localPlayer.GetPlayerName() == playerName)
            {
                pos = Player.m_localPlayer.transform.position;
            }
            else
            {
                if (ZNet.instance.GetPeerByPlayerName(playerName) is not { } peer)
                {
                    Discord.instance.SendMessage(DiscordBotPlugin.Webhook.Commands, ZNet.instance.GetWorldName(), "Failed to find " + playerName);
                    return;
                }

                pos = peer.m_refPos;
            }
            
            if (RandEventSystem.instance.GetEvent(eventName) is not {} randEvent)
            {
                Discord.instance.SendMessage(DiscordBotPlugin.Webhook.Commands, ZNet.instance.GetWorldName(), "");
                return;
            }
            RandEventSystem.instance.SetRandomEvent(randEvent, pos);
        }, adminOnly: true);
        
    }
    
    public static bool Spawn(string prefabName, int level, Vector3 pos)
    {
        if (!ZNetScene.instance) return false;
        if (ZNetScene.instance.GetPrefab(prefabName) is not { } prefab) return false;
        var random = (UnityEngine.Random.insideUnitSphere * 5f) with { y = 0.0f };
        var location = pos + random;
        var gameObject = UnityEngine.Object.Instantiate(prefab, location, Quaternion.identity);
        if (gameObject.TryGetComponent(out Character character)) character.SetLevel(level);
        return true;
    }

    public static bool GiveItem(string itemName, int amount, int quality, int variant)
    {
        if (!Player.m_localPlayer || !ObjectDB.instance) return false;
        if (ObjectDB.instance.GetItemPrefab(itemName) is not { } itemPrefab || !itemPrefab.TryGetComponent(out ItemDrop component)) return false;
        
        var itemData = component.m_itemData.Clone();
        itemData.m_dropPrefab = itemPrefab;
        itemData.m_stack = amount;
        itemData.m_quality = quality;
        itemData.m_variant = variant;

        Player.m_localPlayer.GetInventory().AddItem(itemData);
        return true;
    }

    public static void RPC_BotToClient(ZRpc rpc, ZPackage pkg)
    {
        // server sending to clients
        var messageType = pkg.ReadString();

        if (!m_commands.TryGetValue(messageType, out DiscordCommand command)) return;
        command.Run(pkg);
    }
    
    public class DiscordCommand
    {
        [Description("Action ran when Discord component receives a new command")]
        private readonly Action<string[]> m_action;
        [Description("Action ran when player receives package from RPC_BotToClient")]
        private readonly Action<ZPackage>? m_reaction;
        [Description("If only discord admins are allowed to run command")]
        private readonly bool m_adminOnly = false;

        [Description("Register a new discord command")]
        public DiscordCommand(string command, string description, Action<string[]> action, Action<ZPackage>? reaction = null, bool adminOnly = false)
        {
            m_action = action;
            m_reaction = reaction;
            m_adminOnly = adminOnly;
            m_commands[command] = this;
            m_descriptions[$"`{command}`"] = description;
        }

        public bool IsAllowed(string discordUserName) => !m_adminOnly || new DiscordBotPlugin.StringListConfig(DiscordBotPlugin.m_discordAdmins.Value).list.Contains(discordUserName);
        
        public void Run(string[] args) => m_action.Invoke(args);

        public void Run(ZPackage pkg) => m_reaction?.Invoke(pkg);
    }
}