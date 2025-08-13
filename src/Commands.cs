using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace DiscordBot;

public static class DiscordCommands
{
    
    public static readonly Dictionary<string, DiscordCommand> m_commands = new();
    
    public static void Setup()
    {
        var listPlayers = new DiscordCommand("listplayers", _ =>
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("List of active players: \n");
            foreach (var player in ZNet.instance.m_players)
            {
                stringBuilder.AppendFormat("{0} ({1}) \n", player.m_name, $"{player.m_position.x}, {player.m_position.y}, {player.m_position.z}");
            }
            Discord.instance.SendEmbedMessage(DiscordBotPlugin.m_commandWebhookURL.Value, "Active Players", stringBuilder.ToString(), ZNet.instance.GetWorldName());
        }, adminOnly: true);

        var kick = new DiscordCommand("kick", args =>
        {
            if (args.Length < 2) return;
            var playerName = args[1];
            if (ZNet.instance.GetPeerByPlayerName(playerName) is not { } peer)
            {
                Discord.instance.SendMessage(DiscordBotPlugin.m_commandWebhookURL.Value, ZNet.instance.GetWorldName(), "Failed to find " + playerName);
                return;
            }
            ZNet.instance.Disconnect(peer);
            Discord.instance.SendMessage(DiscordBotPlugin.m_commandWebhookURL.Value, ZNet.instance.GetWorldName(), $"Kicked {playerName} from server !");
        }, adminOnly:true);

        var give = new DiscordCommand("give", args =>
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
            else
            {
                if (ZNet.instance.GetPeerByPlayerName(playerName) is not { } peer)
                {
                    Discord.instance.SendMessage(DiscordBotPlugin.m_commandWebhookURL.Value, ZNet.instance.GetWorldName(), "Failed to find " + playerName);
                    return;
                }
                var pkg = new ZPackage();
                pkg.Write("give");
                pkg.Write(itemName);
                pkg.Write(amount);
                pkg.Write(quality);
                pkg.Write(variant);
                
                peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
            }
        }, adminOnly:true);

        var teleportTo = new DiscordCommand("teleport", args =>
        {
            if (args.Length < 5) return;
            var playerName = args[1].Trim();
            var otherPlayerName = args[2].Trim();
            Vector3 pos;

            if (otherPlayerName == "bed")
            {
                if (Player.m_localPlayer)
                {
                    pos = Game.instance.GetPlayerProfile().GetCustomSpawnPoint();
                    Player.m_localPlayer.TeleportTo(pos, Quaternion.identity, true);
                }
                else
                {
                    
                    ZPackage pkg = new ZPackage();
                    pkg.Write("bed");
                    if (ZNet.instance.GetPeerByPlayerName(playerName) is not { } peer) return;
                    peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
                }
                
                return;
            }
            
            
            
            if (Player.m_localPlayer && Player.m_localPlayer.GetPlayerName() == otherPlayerName)
            {
                pos = Player.m_localPlayer.transform.position;
            }
            else if (ZNet.instance.GetPeerByPlayerName(otherPlayerName) is { } otherPeer)
            {
                pos = otherPeer.m_refPos;
            }
            else
            {
                if (!float.TryParse(args[2].Trim(), out float x)) return;
                if (!float.TryParse(args[3].Trim(), out float y)) return;
                if (!float.TryParse(args[4].Trim(), out float z)) return;
                pos = new Vector3(x, y, z);
            }
            
            if (Player.m_localPlayer && Player.m_localPlayer.GetPlayerName() == playerName)
            {
                Player.m_localPlayer.TeleportTo(pos, Quaternion.identity, true);
            }
            else
            {
                if (ZNet.instance.GetPeerByPlayerName(playerName) is not { } peer)
                {
                    Discord.instance.SendMessage(DiscordBotPlugin.m_commandWebhookURL.Value, ZNet.instance.GetWorldName(), "Failed to find " + playerName);
                    return;
                }

                var pkg = new ZPackage();
                pkg.Write("teleport");
                pkg.Write(pos);
                peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
            }

        }, adminOnly:true);

        var spawn = new DiscordCommand("spawn", args =>
        {
            if (args.Length < 4) return;
            var creatureName = args[1];
            var level = int.TryParse(args[2], out int lvl) ? lvl : 1;
            var playerName = args[3];

            if (Player.m_localPlayer && Player.m_localPlayer.GetPlayerName() == playerName)
            {
                Spawn(creatureName, level, Player.m_localPlayer.transform.position);
            }
            else
            {
                if (ZNet.instance.GetPeerByPlayerName(playerName) is not { } peer)
                {
                    Discord.instance.SendMessage(DiscordBotPlugin.m_commandWebhookURL.Value, ZNet.instance.GetWorldName(), "Failed to find " + playerName);
                    return;
                }

                var pkg = new ZPackage();
                pkg.Write("spawn");
                pkg.Write(creatureName);
                pkg.Write(level);
                peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);

            }
        }, adminOnly: true);

        var save = new DiscordCommand("save", _ =>
        {
            ZNet.instance.Save(true, true, true);
        }, adminOnly: true);

        var shutDown = new DiscordCommand("shutdown", _ =>
        {
            ZNet.instance.SaveOtherPlayerProfiles();
            ZNet.instance.Shutdown();
        }, adminOnly: true);

        var scream = new DiscordCommand("message", args =>
        {
            var message = string.Join(" ", args.Skip(1));
            var pkg = new ZPackage();
            pkg.Write("message");
            pkg.Write(message);
            foreach(var peer in ZNet.instance.GetPeers()) peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
            if (Player.m_localPlayer) Player.m_localPlayer.Message(MessageHud.MessageType.Center, message);
        }, adminOnly: true);

        var setKey = new DiscordCommand("setkey", args =>
        {
            var key = args[1];
            if (!Enum.TryParse(key, out GlobalKeys globalKey))
            {
                Discord.instance.SendMessage(DiscordBotPlugin.m_commandWebhookURL.Value, ZNet.instance.GetWorldName(), "Failed to find global key: " + key);
                return;
            }
            ZoneSystem.instance.SetGlobalKey(globalKey);
        }, adminOnly: true);

        var eventList = new DiscordCommand("listevents", _ =>
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var randomEvent in RandEventSystem.instance.m_events)
            {
                stringBuilder.Append($"{randomEvent.m_name}\n");
            }
            Discord.instance.SendEmbedMessage(DiscordBotPlugin.m_commandWebhookURL.Value, "Available events:", stringBuilder.ToString(), ZNet.instance.GetWorldName());
        }, adminOnly: true);

        var startEvent = new DiscordCommand("event", args =>
        {
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
                    Discord.instance.SendMessage(DiscordBotPlugin.m_commandWebhookURL.Value, ZNet.instance.GetWorldName(), "Failed to find " + playerName);
                    return;
                }

                pos = peer.m_refPos;
            }
            
            if (RandEventSystem.instance.GetEvent(eventName) is not {} randEvent)
            {
                Discord.instance.SendMessage(DiscordBotPlugin.m_commandWebhookURL.Value, ZNet.instance.GetWorldName(), "");
                return;
            }
            RandEventSystem.instance.SetRandomEvent(randEvent, pos);
        }, adminOnly: true);
        
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
    private static class ZNet_OnNewConnection_Patch
    {
        private static void Postfix(ZNetPeer peer)
        {
            peer.m_rpc.Register<ZPackage>(nameof(RPC_BotToClient), RPC_BotToClient);
            // register rpc to client
        }
    }
    public static void Spawn(string creatureName, int level, Vector3 pos)
    {
        if (ZNetScene.instance.GetPrefab(creatureName) is not { } prefab || !prefab.TryGetComponent(out Character component)) return;
        var random = (UnityEngine.Random.insideUnitSphere * 5f) with { y = 0.0f };
        var location = pos + random;
        var creature = UnityEngine.Object.Instantiate(prefab, location, Quaternion.identity);
        if (creature.TryGetComponent(out Character character)) character.SetLevel(level);
    }

    public static void GiveItem(string itemName, int amount, int quality, int variant)
    {
        if (ObjectDB.instance.GetItemPrefab(itemName) is not { } itemPrefab || !itemPrefab.TryGetComponent(out ItemDrop component)) return;
        var itemData = component.m_itemData.Clone();
        itemData.m_dropPrefab = itemPrefab;
        itemData.m_stack = amount;
        itemData.m_quality = quality;
        itemData.m_variant = variant;

        Player.m_localPlayer.GetInventory().AddItem(itemData);
    }

    public static void RPC_BotToClient(ZRpc rpc, ZPackage pkg)
    {
        // server sending to clients
        var messageType = pkg.ReadString();
        switch (messageType)
        {
            case "give":
                if (!Player.m_localPlayer || !ObjectDB.instance) return;
                var itemName = pkg.ReadString();
                var amount = pkg.ReadInt();
                var quality = pkg.ReadInt();
                var variant = pkg.ReadInt();
                
                GiveItem(itemName, amount,quality,variant);
                break;
            case "teleport":
                Vector3 pos = pkg.ReadVector3();
                Player.m_localPlayer.TeleportTo(pos, Quaternion.identity, true);
                break;
            case "spawn":
                string creatureName = pkg.ReadString();
                int level = pkg.ReadInt();
                Spawn(creatureName, level, Player.m_localPlayer.transform.position);
                break;
            case "message":
                string message = pkg.ReadString();
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, message);
                break;
            case "bed":
                var bedPoint = Game.instance.GetPlayerProfile().GetCustomSpawnPoint();
                Player.m_localPlayer.TeleportTo(bedPoint, Quaternion.identity, true);
                break;
        }
        
    }
    
    public class DiscordCommand
    {
        public readonly Action<string[]> m_action;
        public readonly bool m_adminOnly = false;

        public DiscordCommand(string command, Action<string[]> action, bool adminOnly = false)
        {
            m_action = action;
            m_commands[command] = this;
            m_adminOnly = adminOnly;
        }

        public bool IsAllowed(string discordUserName)
        {
            if (!m_adminOnly) return true;
            return new DiscordBotPlugin.StringListConfig(DiscordBotPlugin.m_discordAdmins.Value).list.Contains(discordUserName);
        }
        public void Run(string[] args) => m_action.Invoke(args);
    }
}