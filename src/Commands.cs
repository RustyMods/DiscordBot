using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using BepInEx.Bootstrap;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace DiscordBot;

public static class DiscordCommands
{
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
    private static class ZNet_OnNewConnection_Patch
    {
        [UsedImplicitly]
        private static void Postfix(ZNetPeer peer)
        {
            // Register RPC to clients to receive message from server
            peer.m_rpc.Register<ZPackage>(nameof(RPC_BotToClient), RPC_BotToClient);
        }
    }

    [HarmonyPatch(typeof(Chat), nameof(Chat.Awake))]
    private static class Chat_Awake_Patch
    {
        [UsedImplicitly]
        private static void Postfix(Chat __instance)
        {
            __instance.AddString("/selfie - send a screenshot to discord");
        }
    }
    
    public static readonly Dictionary<string, DiscordCommand> m_commands = new();
    private static readonly List<CommandTooltip> m_tooltips = new();
    public static bool loaded;
    public static void Setup()
    {
        var selfie = new Terminal.ConsoleCommand("selfie", "Screenshots current game view", _ =>
        {
            Screenshot.instance?.StartSelfie();
        });
        var help = new DiscordCommand("!help", "List of commands", _ =>
        {
            // max 25 embed elements in a single message
            const int itemsPerEmbed = 25;
            List<CommandTooltip> adminCommands = new();
            List<CommandTooltip> otherCommands = new();
            foreach (var tooltip in m_tooltips)
            {
                if (tooltip.m_adminOnly) adminCommands.Add(tooltip);
                else otherCommands.Add(tooltip);
            }

            if (adminCommands.Count > 25)
            {
                int totalEmbeds = (int)Math.Ceiling((double)adminCommands.Count / itemsPerEmbed);
                for (int index = 0; index < totalEmbeds; ++index)
                {
                    var chunk = adminCommands.Skip(index * itemsPerEmbed).Take(itemsPerEmbed)
                        .ToDictionary(command => command.m_command, command => command.m_description);

                    string title = totalEmbeds == 1
                        ? "List of admin commands"
                        : $"List of admin commands (Part {index + 1} of {totalEmbeds})";

                    Discord.instance?.SendTableEmbed(
                        Webhook.Commands,
                        title,
                        chunk);
                }
            }
            else
            {
                Discord.instance?.SendTableEmbed(
                    Webhook.Commands,
                    "List of admin commands",
                    adminCommands.ToDictionary(command => command.m_command, command => command.m_description));
            }
            
            if (otherCommands.Count > 25)
            {
                int totalEmbeds = (int)Math.Ceiling((double)otherCommands.Count / itemsPerEmbed);
                for (int index = 0; index < totalEmbeds; ++index)
                {
                    var chunk = otherCommands.Skip(index * itemsPerEmbed).Take(itemsPerEmbed)
                        .ToDictionary(command => command.m_command, command => command.m_description);

                    string title = totalEmbeds == 1
                        ? "List of commands"
                        : $"List of commands (Part {index + 1} of {totalEmbeds}";

                    Discord.instance?.SendTableEmbed(
                        Webhook.Commands,
                        title,
                        chunk);
                }
            }
            else
            {
                Discord.instance?.SendTableEmbed(
                    Webhook.Commands,
                    "List of commands",
                    otherCommands.ToDictionary(command => command.m_command, command => command.m_description));
            }

        }, emoji: "question");
        var listAdmins = new DiscordCommand("!listadmins", "List of discord admins registered to plugin", _ =>
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var username in new DiscordBotPlugin.StringListConfig(DiscordBotPlugin.DiscordAdmins).list)
            {
                stringBuilder.Append($"`{username}`\n");
            }
            Discord.instance?.SendEmbedMessage(Webhook.Commands, "List of discord usernames who can use commands:", stringBuilder.ToString());
        }, emoji: "warning");
        var addDiscordAdmin = new DiscordCommand("!addadmin",
            "Adds discord username to admin list, to enable using commands, `username`",
            args =>
            {
                if (args.Length < 2) return;
                string userName = args[1].Trim();
                DiscordBotPlugin.StringListConfig admins = new DiscordBotPlugin.StringListConfig(DiscordBotPlugin.DiscordAdmins);
                admins.list.Add(userName);
                DiscordBotPlugin.SetDiscordAdmins(admins.ToString());
                listAdmins.Run(new []{"listadmins"});
            }, adminOnly: true, emoji: "key");
        var removeDiscordAdmin = new DiscordCommand("!removeadmin",
            "Remove discord username from admin list, to disable using commands, `username`",
            args =>
            {
                string userName = args[1].Trim();
                DiscordBotPlugin.StringListConfig admins = new DiscordBotPlugin.StringListConfig(DiscordBotPlugin.DiscordAdmins);
                admins.list.Remove(userName);
                DiscordBotPlugin.SetDiscordAdmins(admins.ToString());
                listAdmins.Run(new []{"listadmins"});
            }, adminOnly: true, emoji: "lock");
        var listEnv = new DiscordCommand("!listenv", "List of available environments", _ =>
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var env in EnvMan.instance.m_environments) stringBuilder.Append($"`{env.m_name}`\n");
            Discord.instance?.SendEmbedMessage(Webhook.Commands, "List of available environments:", stringBuilder.ToString());
        }, emoji: "tornado");
        var forceEnv = new DiscordCommand("!env", "Force environment on all players", args =>
            {
                if (args.Length < 2) return;
                var environment = args[1].Trim();
                if (EnvMan.instance.GetEnv(environment) is { } env)
                {
                    var pkg = new ZPackage();
                    pkg.Write("!env");
                    pkg.Write(environment);
                    foreach(var peer in ZNet.instance.GetPeers()) peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
                    if (Player.m_localPlayer)
                    {
                        EnvMan.instance.m_debugEnv = environment;
                    }
                }
                else
                {
                    Discord.instance?.SendMessage(Webhook.Commands, message:
                        "Failed to find environment: " + environment);
                }
            }, pkg =>
            {
                string environment = pkg.ReadString();
                EnvMan.instance.m_debugEnv = environment;
            },
            adminOnly: true, emoji: "sparkle");
        var resetEnv = new DiscordCommand("!resetenv", "Reset environment on all players", _ =>
            {
                var pkg = new ZPackage();
                pkg.Write("!resetenv");
                foreach(var peer in ZNet.instance.GetPeers()) peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
                if (Player.m_localPlayer) EnvMan.instance.m_debugEnv = "";
            }, _ => EnvMan.instance.m_debugEnv = "",
            adminOnly: true, emoji: "sparkle");
        var listPlayers = new DiscordCommand("!listplayers", "List of active players", _ =>
        {
            Dictionary<string, string> content = new Dictionary<string, string>();
            foreach (var player in ZNet.instance.m_players)
            {
                content[player.m_name] = $"Position: `{player.m_position.x} {player.m_position.y} {player.m_position.z}`";
            }
            Discord.instance?.SendTableEmbed(Webhook.Commands, "List of active players", content);
        }, adminOnly: true, emoji:"dragon");

        var kick = new DiscordCommand("!kick", "Kicks player from server, `playername`", args =>
        {
            if (args.Length < 2) return;
            var playerName = args[1].Trim();
            if (ZNet.instance.GetPeerByPlayerName(playerName) is not { } peer)
            {
                Discord.instance?.SendMessage(Webhook.Commands, message: "Failed to find " + playerName);
            }
            else
            {
                ZNet.instance.Disconnect(peer);
                Discord.instance?.SendMessage(Webhook.Commands, message: $"Kicked {playerName} from server !");
            }
        }, adminOnly:true, emoji:"x");

        var give = new DiscordCommand("!give", "Adds item directly into player inventory, `player name` `item name` `amount` `quality?` `variant?`", args =>
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
                pkg.Write("!give");
                pkg.Write(itemName);
                pkg.Write(amount);
                pkg.Write(quality);
                pkg.Write(variant);
                
                peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
            }
            else
            {
                Discord.instance?.SendMessage(Webhook.Commands, message: "Failed to find " + playerName);
            }
        }, pkg =>
        {
            var itemName = pkg.ReadString();
            var amount = pkg.ReadInt();
            var quality = pkg.ReadInt();
            var variant = pkg.ReadInt();
                
            GiveItem(itemName, amount,quality,variant);
        }, adminOnly:true, emoji:"gift");

        var teleportAll = new DiscordCommand("!teleportall",
            "Teleports all players to location, `x` `y` `z`",
            args =>
            {
                if (args.Length != 4) return;
                if (float.TryParse(args[1].Trim(), out float x) && float.TryParse(args[2].Trim(), out float y) &&
                    float.TryParse(args[3].Trim(), out float z))
                {
                    var pos = new Vector3(x, y, z);
                    var pkg = new ZPackage();
                    pkg.Write("!teleport"); // use teleport to discord command
                    pkg.Write("vector");
                    pkg.Write(pos);
                    foreach(var peer in ZNet.instance.GetPeers()) peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
                    if (Player.m_localPlayer) Player.m_localPlayer.TeleportTo(pos, Quaternion.identity, true);
                }
                else
                {
                    Discord.instance?.SendMessage(Webhook.Commands, message: "Incorrect teleport all command format");
                }
            }, adminOnly: true, emoji:"golf");

        var teleportTo = new DiscordCommand("!teleport", "Teleport player to location, `player name` `bed` or `other player name` or `x` `y` `z`", args =>
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
                    pkg.Write("!teleport");
                    pkg.Write("bed");
                    peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
                }
                else
                {
                    Discord.instance?.SendMessage(Webhook.Commands, message: "Failed to find " + playerName);
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
                    Discord.instance?.SendMessage(Webhook.Commands, message: "Incorrect teleport command format");
                    return;
                }
                
                if (Player.m_localPlayer && Player.m_localPlayer.GetPlayerName() == playerName)
                {
                    Player.m_localPlayer.TeleportTo(pos, Quaternion.identity, true);
                }
                else if (ZNet.instance.GetPeerByPlayerName(playerName) is { } peer)
                {
                    var pkg = new ZPackage();
                    pkg.Write("!teleport");
                    pkg.Write("vector");
                    pkg.Write(pos);
                    peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
                }
                else
                {
                    Discord.instance?.SendMessage(Webhook.Commands, message: "Failed to find " + playerName);
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
        },adminOnly:true, emoji:"run");

        var spawn = new DiscordCommand("!spawn", "spawns prefab at location, `prefab name` `level` `player name` or `x` `y` `z`", args =>
        {
            if (args.Length < 4) return;
            var prefabName = args[1].Trim();
            var level = int.TryParse(args[2].Trim(), out int lvl) ? lvl : 1;

            if (args.Length == 6)
            {
                if (!float.TryParse(args[3].Trim(), out float x) || !float.TryParse(args[4].Trim(), out float y) || !float.TryParse(args[5].Trim(), out float z))
                {
                    Discord.instance?.SendMessage(Webhook.Commands, message: "Incorrect spawn command format");
                }
                else if (!ZoneSystem.instance.IsZoneLoaded(new Vector3(x, y, z)))
                {
                    Discord.instance?.SendMessage(Webhook.Commands, message: "Failed to spawn, location zone is not loaded!");
                }
                else if (!Spawn(prefabName, level, new Vector3(x, y, z)))
                {
                    Discord.instance?.SendMessage(Webhook.Commands, message: "Failed to spawn: " + prefabName);
                }
            }
            else
            {
                var playerName = args[3].Trim();

                if (Player.m_localPlayer && Player.m_localPlayer.GetPlayerName() == playerName)
                {
                    if (!Spawn(prefabName, level, Player.m_localPlayer.transform.position))
                    {
                        Discord.instance?.SendMessage(Webhook.Commands, message: "Failed to spawn: " + prefabName);
                    }
                }
                else if (ZNet.instance.GetPeerByPlayerName(playerName) is {} peer)
                {
                    var pkg = new ZPackage();
                    pkg.Write("!spawn");
                    pkg.Write(prefabName);
                    pkg.Write(level);
                    peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
                }
                else
                {
                    Discord.instance?.SendMessage(Webhook.Commands, message: "Failed to find " + playerName);
                }
            }
            
        }, pkg =>
        {
            string creatureName = pkg.ReadString();
            int level = pkg.ReadInt();
            Spawn(creatureName, level, Player.m_localPlayer.transform.position);
        },adminOnly: true, emoji:"exclamation");

        var save = new DiscordCommand("!save", "Save player profiles and world", _ =>
        {
            ZNet.instance.Save(true, true, true);
        }, adminOnly: true, emoji:"save");

        // var shutDown = new DiscordCommand("!shutdown", "Save player profiles, save world and shutdown, bot cannot start server", _ =>
        // {
        //     ZNet.instance.SaveOtherPlayerProfiles();
        //     ZNet.instance.Shutdown();
        //     Application.Quit();
        // }, adminOnly: true, emoji:"stop");

        var message = new DiscordCommand("!message", "Broadcast message to all players which shows up center of screen", args =>
        {
            var message = string.Join(" ", args.Skip(1));
            MessageHud.instance.MessageAll(MessageHud.MessageType.Center, message);
        }, adminOnly: true, emoji:"smile");

        var image = new DiscordCommand("!image", "Broadcast image to all players which takes over entire screen",
            args =>
            {
                if (args.Length < 2) return;
                var url = args[1].Trim();
                var pkg = new ZPackage();
                pkg.Write("!image");
                pkg.Write(url);
                foreach(var peer in ZNet.instance.GetPeers()) peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
                if (Player.m_localPlayer)
                {
                    Discord.instance?.GetImage(url);
                }
            }, pkg =>
            {
                var url = pkg.ReadString();
                Discord.instance?.GetImage(url);
            },adminOnly: true, emoji: "paint");

        var forceSleep =
            new DiscordCommand("!sleep", "Forced everyone to sleep", _ =>
            {
                if (EnvMan.instance.IsTimeSkipping() || !EnvMan.IsAfternoon() && !EnvMan.IsNight() ||
                    ZNet.instance.GetTimeSeconds() - Game.instance.m_lastSleepTime < 10.0) return;
                EnvMan.instance.SkipToMorning();
                Game.instance.m_sleeping = true;
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "SleepStart");
            }, adminOnly: true, emoji: "moon");

        var listKeys = new DiscordCommand("!listkeys", "List of global keys", _ =>
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var key in Enum.GetValues(typeof(GlobalKeys)))
            {
                stringBuilder.Append($"`{key}`\n");
            }
            Discord.instance?.SendEmbedMessage(Webhook.Commands, "Global keys:", stringBuilder.ToString());
        }, emoji: "fox");

        var currentKeys = new DiscordCommand("!keys", "List of current global keys", _ =>
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var key in ZoneSystem.instance.GetGlobalKeys())
            {
                stringBuilder.Append($"`{key}`\n");
            }
            Discord.instance?.SendEmbedMessage(Webhook.Commands, "Active keys:", stringBuilder.ToString());
        }, emoji:"game");

        var setKey = new DiscordCommand("!setkey", "Set global key", args =>
        {
            if (args.Length < 2) return;
            var key = args[1].Trim();
            if (!Enum.TryParse(key, true, out GlobalKeys globalKey))
            {
                Discord.instance?.SendMessage(Webhook.Commands, message : "Failed to find global key: " + key);
            }
            else
            {
                ZoneSystem.instance.SetGlobalKey(globalKey);
            }
        }, adminOnly: true, emoji:"unicorn");

        var removeKey = new DiscordCommand("!removekey", "Remove global key", args =>
            {
                if (args.Length < 2) return;
                var key = args[1].Trim();
                if (!Enum.TryParse(key, true, out GlobalKeys globalKey))
                {
                    Discord.instance?.SendMessage(Webhook.Commands, message: "Failed to find global key: " + key);
                }
                else
                {
                    ZoneSystem.instance.RemoveGlobalKey(globalKey);
                }
            }, adminOnly: true,
            emoji: "pencil");

        var listPrefabs = new DiscordCommand("!listprefabs", "List of prefabs available to spawn, `filter`",
            args =>
            {
                var filter = args.Length > 1 ? args[1].Trim().ToLower() : "";
                StringBuilder stringBuilder = new StringBuilder();
                foreach (var prefab in ZNetScene.instance.m_prefabs.Where(x => x.name.ToLower().Contains(filter)))
                {
                    stringBuilder.Append($"`{prefab.name}`\n");
                }
                Discord.instance?.SendEmbedMessage(Webhook.Commands, "Prefab Names:", stringBuilder.ToString());
            }, adminOnly: true, emoji: "fire");

        var eventList = new DiscordCommand("!listevents", "List of available event names", _ =>
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var randomEvent in RandEventSystem.instance.m_events)
            {
                stringBuilder.Append($"`{randomEvent.m_name}`\n");
            }
            Discord.instance?.SendEmbedMessage(Webhook.Commands, "Available events:", stringBuilder.ToString());
        }, adminOnly: true, emoji:"moon");

        var startEvent = new DiscordCommand("!event", "Starts an event on a player, `event name` `player name`", args =>
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
                    Discord.instance?.SendMessage(Webhook.Commands, message: "Failed to find " + playerName);
                    return;
                }

                pos = peer.m_refPos;
            }
            
            if (RandEventSystem.instance.GetEvent(eventName) is not {} randEvent)
            {
                Discord.instance?.SendMessage(Webhook.Commands, message: "Failed to find event: " + eventName);
                return;
            }
            RandEventSystem.instance.SetRandomEvent(randEvent, pos);
        }, adminOnly: true, emoji:"star");
        var listStatus = new DiscordCommand("!liststatus", "List of available status effects", args =>
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var status in ObjectDB.instance.m_StatusEffects) stringBuilder.Append($"`{status.name}`\n");
            Discord.instance?.SendEmbedMessage(Webhook.Commands, "Available status effects:", stringBuilder.ToString());
        }, emoji: "rocket");
        var addStatus = new DiscordCommand("!addstatus", "Add status effect on player, `player name` `status effect` `duration`", args =>
            {
                if (args.Length != 4) return;
                var playerName = args[1].Trim();
                var statusName = args[2].Trim();
                float duration = float.TryParse(args[3].Trim(), out float time) ? time : 0f;

                if (ObjectDB.instance.GetStatusEffect(statusName.GetStableHashCode()) is not { } statusEffect)
                {
                    Discord.instance?.SendMessage(Webhook.Commands, message : "Failed to find status effect: " + statusName);
                }
                else
                {
                    if (Player.m_localPlayer && Player.m_localPlayer.GetPlayerName() == playerName)
                    {
                        var status = Player.m_localPlayer.GetSEMan().AddStatusEffect(statusEffect);
                        if (duration > 0f) status.m_ttl = duration;
                    }
                    else if (ZNet.instance.GetPeerByPlayerName(playerName) is { } peer)
                    {
                        var pkg = new ZPackage();
                        pkg.Write("!addstatus");
                        pkg.Write(statusName);
                        pkg.Write((double)duration);
                        peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
                    }
                    else
                    {
                        Discord.instance?.SendMessage(Webhook.Commands, message: "Failed to find " + playerName);
                    }
                }
            }, pkg =>
            {
                var statusName = pkg.ReadString();
                var duration = (float)pkg.ReadDouble();
                if (ObjectDB.instance.GetStatusEffect(statusName.GetStableHashCode()) is not { } statusEffect) return;
                var status = Player.m_localPlayer.GetSEMan().AddStatusEffect(statusEffect);
                if (duration > 0f) status.m_ttl = duration;
            },
            adminOnly: true, emoji: "pizza");
        var heal = new DiscordCommand("!heal", "Heals to full health & stamina, `player name`", args =>
            {
                if (args.Length < 2) return;
                var playerName = args[1].Trim();
                if (Player.m_localPlayer && Player.m_localPlayer.GetPlayerName() == playerName)
                {
                    Player.m_localPlayer.Heal(Player.m_localPlayer.GetMaxHealth());
                    Player.m_localPlayer.AddStamina(Player.m_localPlayer.GetMaxStamina());
                    Player.m_localPlayer.AddEitr(Player.m_localPlayer.GetMaxEitr());
                }
                else if (ZNet.instance.GetPeerByPlayerName(playerName) is { } peer)
                {
                    var pkg = new ZPackage();
                    pkg.Write("!heal");
                    peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
                }
                else
                {
                    Discord.instance?.SendMessage(Webhook.Commands, message: "Failed to find player: " + playerName);
                }

            },
            _ =>
            {
                Player.m_localPlayer.Heal(Player.m_localPlayer.GetMaxHealth());
                Player.m_localPlayer.AddStamina(Player.m_localPlayer.GetMaxStamina());
                Player.m_localPlayer.AddEitr(Player.m_localPlayer.GetMaxEitr());
            }, adminOnly: true, emoji: "heart");

        var die = new DiscordCommand("!die", "Kills player, `player name`", args =>
            {
                if (args.Length < 2) return;
                var playerName = args[1].Trim();
                if (Player.m_localPlayer && Player.m_localPlayer.GetPlayerName() == playerName)
                {
                    Player.m_localPlayer.Damage(new HitData()
                    {
                        m_damage = {m_damage =  99999f}, m_hitType = HitData.HitType.Self
                    });
                }
                else if (ZNet.instance.GetPeerByPlayerName(playerName) is { } peer)
                {
                    var pkg = new ZPackage();
                    pkg.Write("!die");
                    peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
                }
                else
                {
                    Discord.instance?.SendMessage(Webhook.Commands, message: "Failed to find player: " + playerName);
                }
            }, _ =>
            {
                Player.m_localPlayer.Damage(new HitData()
                {
                    m_damage = {m_damage =  99999f}, m_hitType = HitData.HitType.Self
                });
            },
            adminOnly: true, emoji: "tiger");
        var listSkills = new DiscordCommand("!listskills", "List of available skills", args =>
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var skill in Enum.GetValues(typeof(Skills.SkillType))) stringBuilder.Append($"{skill.ToString().Format(TextFormat.InlineCode)}" + "\n");
            Discord.instance?.SendEmbedMessage(Webhook.Commands, "Available skill types:", stringBuilder.ToString(), ZNet.instance.GetWorldName());
        }, emoji: "pray");
        var raiseSkill = new DiscordCommand("!raiseskill",
            "Raises skill level, `player name` `stkill type` `amount`",
            args =>
            {
                if (args.Length < 3) return;
                var playerName = args[1].Trim();
                var skillType = args[2].Trim();
                var amount = float.TryParse(args[3].Trim(), out float skillAmount) ? skillAmount : 1f;
                if (!Enum.TryParse(skillType, out Skills.SkillType type))
                {
                    Discord.instance?.SendMessage(Webhook.Commands, message: "Failed to find skill type: " + skillType);
                }
                else
                {
                    if (Player.m_localPlayer && Player.m_localPlayer.GetPlayerName() == playerName)
                    {
                        Player.m_localPlayer.GetSkills().CheatRaiseSkill(skillType, amount);
                    }
                    else if (ZNet.instance.GetPeerByPlayerName(playerName) is { } peer)
                    {
                        var pkg = new ZPackage();
                        pkg.Write("!raiseskill");
                        pkg.Write(skillType);
                        pkg.Write((double)amount);
                        peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
                    }
                    else
                    {
                        Discord.instance?.SendMessage(Webhook.Commands, message: "Failed to find player: " + playerName);
                    }
                }
            }, pkg =>
            {
                var skillType = pkg.ReadString();
                var amount = (float)pkg.ReadDouble();
                Player.m_localPlayer.GetSkills().CheatRaiseSkill(skillType, amount);
            }, adminOnly: true, emoji: "muscle");

        var pos = new DiscordCommand("!pos", "Player position, `player name`", args =>
        {
            if (args.Length < 2) return;
            var playerName = args[1].Trim();
            Vector3 pos;
            if (Player.m_localPlayer && Player.m_localPlayer.GetPlayerName() == playerName)
            {
                pos = Player.m_localPlayer.transform.position;
            }
            else if (ZNet.instance.GetPeerByPlayerName(playerName) is { } peer)
            {
                pos = peer.m_refPos;
            }
            else
            {
                Discord.instance?.SendMessage(Webhook.Commands, message: "Failed to find player: " + playerName);
                return;
            }
            
            Discord.instance?.SendMessage(Webhook.Commands, message: $"{playerName} position: {pos.x},{pos.y},{pos.z}");
            
        }, adminOnly: true, emoji: "rose");
        var stats = new DiscordCommand("!stats", "Player stats, player must be online, `player name`", args =>
        {
            if (args.Length < 2) return;
            var playerName = args[1].Trim();
            if (Player.m_localPlayer && Player.m_localPlayer.GetPlayerName() == playerName)
            {
                var profile = Game.instance.GetPlayerProfile();
                StringBuilder stringBuilder = new StringBuilder();
                foreach (var kvp in profile.m_playerStats.m_stats)
                {
                    if (kvp.Value > 0f)
                    {
                        stringBuilder.Append(
                            $"{kvp.Key.ToString().Format(TextFormat.Bold)}: {kvp.Value.ToString("0.0").Format(TextFormat.InlineCode)}\n");
                    }
                }
                Discord.instance?.SendEmbedMessage(Webhook.Commands, $"{playerName} Stats", stringBuilder.ToString());
            } 
            else if (ZNet.instance.GetPeerByPlayerName(playerName) is { } peer)
            {
                var pkg = new ZPackage();
                pkg.Write("!stats");
                peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
            }
            else
            {
                Discord.instance?.SendMessage(Webhook.Commands, message: "Failed to find player: " + playerName);
            }
        }, _ =>
        {
            // this works differently - player receives this RPC and then uses discord bot to send a webhook message
            var profile = Game.instance.GetPlayerProfile();
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var kvp in profile.m_playerStats.m_stats)
            {
                if (kvp.Value > 0f)
                {
                    stringBuilder.Append(
                        $"{kvp.Key.ToString().Format(TextFormat.Bold)}: {kvp.Value.ToString("0.0").Format(TextFormat.InlineCode)}\n");
                }
            }
            Discord.instance?.SendEmbedMessage(Webhook.Commands, $"{profile.m_playerName} Stats", stringBuilder.ToString());
        },emoji: "wine");

        var mods = new DiscordCommand("!mods", "List of plugin installed, `player name?`", args =>
            {
                if (args.Length > 1)
                {
                    var playerName = args[1].Trim();
                    if (Player.m_localPlayer && Player.m_localPlayer.GetPlayerName() == playerName)
                    {
                        StringBuilder stringBuilder = new StringBuilder();
                        foreach (var plugin in Chainloader.PluginInfos)
                        {
                            stringBuilder.Append($"{plugin.Value.Metadata.Name}-{plugin.Value.Metadata.Version}\n");
                        }
                        Discord.instance?.SendEmbedMessage(Webhook.Commands, $"{playerName} installed plugins", stringBuilder.ToString());
                    }
                    else if (ZNet.instance.GetPeerByPlayerName(playerName) is { } peer)
                    {
                        var pkg = new ZPackage();
                        pkg.Write("!mods");
                        peer.m_rpc.Invoke(nameof(RPC_BotToClient), pkg);
                    }
                    else
                    {
                        Discord.instance?.SendMessage(Webhook.Commands, message: "Failed to find player: " + playerName);
                    }
                }
                else
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    foreach (var plugin in Chainloader.PluginInfos)
                    {
                        stringBuilder.Append($"{plugin.Value.Metadata.Name}-{plugin.Value.Metadata.Version}\n");
                    }
                    Discord.instance?.SendEmbedMessage(Webhook.Commands, "Server installed plugins", stringBuilder.ToString());
                }
            },
            pkg =>
            {
                StringBuilder stringBuilder = new StringBuilder();
                foreach (var plugin in Chainloader.PluginInfos)
                {
                    stringBuilder.Append($"{plugin.Value.Metadata.Name}-{plugin.Value.Metadata.Version}\n");
                }
                Discord.instance?.SendEmbedMessage(Webhook.Commands, $"{Game.instance.GetPlayerProfile().m_playerName} installed plugins", stringBuilder.ToString());
            }, emoji: "guitar");
        
        loaded = true;
        foreach (var externalCommands in API.m_queue)
        {
            externalCommands.Invoke(); // for some reason, just directly adding commands removes !help command.
        }
    }
    
    public static bool Spawn(string prefabName, int level, Vector3 pos)
    {
        if (!ZNetScene.instance) return false;
        if (ZNetScene.instance.GetPrefab(prefabName) is not { } prefab) return false;
        Vector3 random = (UnityEngine.Random.insideUnitSphere * 5f) with { y = 0.0f };
        Vector3 location = pos + random;
        GameObject? gameObject = UnityEngine.Object.Instantiate(prefab, location, Quaternion.identity);
        if (gameObject.TryGetComponent(out Character character)) character.SetLevel(level);
        return true;
    }

    public static bool GiveItem(string itemName, int amount, int quality, int variant)
    {
        if (!Player.m_localPlayer || !ObjectDB.instance) return false;
        return Player.m_localPlayer.GetInventory().AddItem(itemName, amount, quality, variant, 0L, "") != null;
    }

    public static void RPC_BotToClient(ZRpc rpc, ZPackage pkg)
    {
        // server sending to clients
        var commandKey = pkg.ReadString();

        if (!m_commands.TryGetValue(commandKey, out DiscordCommand command)) return;
        command.Run(pkg);
    }
    public class DiscordCommand
    {
        [Description("Action runs when Discord component receives a new command")]
        private readonly Action<string[]>? action;
        [Description("Action runs when player receives package from RPC_BotToClient")]
        private readonly Action<ZPackage>? reaction;
        [Description("If only discord admins are allowed to run command")]
        private readonly bool adminOnly;

        [Description("Register a new discord command")]
        public DiscordCommand(string command, string description, Action<string[]>? action, Action<ZPackage>? reaction = null, bool adminOnly = false, bool isSecret = false, string emoji = "")
        {
            this.action = action;
            this.reaction = reaction;
            this.adminOnly = adminOnly;
            m_commands[command] = this;
            if (!isSecret)
            {
                _ = new CommandTooltip(command, description, adminOnly, emoji);
            }       
        }

        public bool IsAllowed(string discordUserName) => !adminOnly || new DiscordBotPlugin.StringListConfig(DiscordBotPlugin.DiscordAdmins).list.Contains(discordUserName);
        
        public void Run(string[] args) => action?.Invoke(args);

        public void Run(ZPackage pkg) => reaction?.Invoke(pkg);
    }

    public class CommandTooltip
    {
        public readonly string m_command;
        public readonly string m_description;
        public readonly bool m_adminOnly;

        public CommandTooltip(string command, string description, bool adminOnly, string emoji)
        {
            m_command = BuildCommandKey(command, emoji);
            m_description = description;
            m_adminOnly = adminOnly;
            m_tooltips.Add(this);
        }
        
        private static string BuildCommandKey(string command, string emoji)
        {
            string emojiPrefix = string.IsNullOrEmpty(emoji) ? "" : $"{EmojiHelper.Emoji(emoji)} ";
            return $"{emojiPrefix}`{command}`";
        }
    }
}