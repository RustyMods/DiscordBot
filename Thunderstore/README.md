# Discord Bot Plugin for Valheim

A BepInEx plugin that enables two-way communication between your Valheim server and Discord channels.

## Features

- 🗨️ **Chat Integration**: Send messages between Discord and in-game chat
- 🤖 **Discord Commands**: Execute server commands from Discord
- 📢 **Server Notifications**: Get notified when the server starts up
- 🔧 **Configurable**: Customize polling intervals, channels, and webhooks

## Prerequisites

- **BepInEx** installed on your Valheim server
- **Discord Webhooks** configured for your server

## Installation

### 1. Install BepInEx

If you haven't already installed BepInEx:

1. Download BepInEx from [GitHub releases](https://github.com/BepInEx/BepInEx/releases)
2. Extract the contents to your Valheim server directory
3. Run the server once to generate BepInEx folders

### 2. Install the Discord Bot Plugin

1. Download the latest release of the Discord Bot plugin
2. Place the `.dll` file in your `BepInEx/plugins/` folder
3. Restart your Valheim server

### 3. Configure the Plugin

After first run, configuration files will be generated in `BepInEx/config/`. Edit the Discord Bot config file to set up your:

- Bot Token
- Channel IDs
- Webhook URLs
- Polling intervals
- World Name

## Discord Setup

### Creating Discord Webhooks

Webhooks allow the plugin to send messages to Discord channels without using the bot's identity.

1. **Create Webhook**
    - Go to "Integrations" tab
    - Click "Create Webhook"
    - Give it a name (e.g., "Valheim Chat")

2. **Copy Webhook URL**
    - Click "Copy Webhook URL"
    - Save this URL for your plugin configuration

### Getting Channel IDs

You'll need Discord Channel IDs for the bot to read messages:

1. **Enable Developer Mode**
    - In Discord, go to User Settings (gear icon)
    - Go to "Advanced" settings
    - Enable "Developer Mode"

2. **Copy Channel IDs**
    - Right-click on each channel you want to use
    - Select "Copy ID"
    - Save these IDs for your configuration

## Configurations

Find DiscordBot plugin configurations in `BepinEx/config/RustyMods.DiscordBot.cfg`

Here's what your configuration might look like:

```ini
[1 - General]

## If on, the configuration is locked and can be changed by server admins only. [Synced with Server]
Lock Configuration = On

## Set interval between check for messages in discord, in seconds [Synced with Server]
Poll Interval = 5

## If on, errors will log to console as warnings [Synced with Server]
Log Errors = Off

## plugin checks against this name to make sure client is connecting to the correct server [Synced with Server]
World Name = MyWorld

[2 - Notifications]

## Set webhook to receive notifications, like server start, stop, save etc... [Synced with Server]
Webhook URL = https://discord.com/api/webhooks/1405043541192741007/X-WuWkr_0ApZ4JHq7_TOeMHfRCErXgUZkVnE_oh_yfy2mWKRShHK-wDzdasdWWDzdjk

## If on, bot will send message when server is starting [Synced with Server]
Startup = Off

## If on, bot will send message when server is shutting down [Synced with Server]
Shutdown = Off

## If on, bot will send message when server is saving [Synced with Server]
Saving = Off

## If on, bot will send message when player dies [Synced with Server]
On Death = On

## If on, bot will send message when new player connects [Synced with Server]
New Connection = Off

[3 - Chat]

## Set discord webhook to display chat messages [Synced with Server]
Webhook URL = https://discord.com/api/webhooks/1404119063046652035/OqBFopk29Cku3_4TiLCJVwaagkyLVsdlkjasd239-sdjzdHH7vHh_RfWy1d3

## Set channel ID to monitor for messages [Synced with Server]
Channel ID = 9839768234583209

## If on, bot will send message when player shouts and monitor discord for messages [Synced with Server]
Enabled = On

[4 - Commands]

## Set discord webhook to display feedback messages from commands [Synced with Server]
Webhook URL = https://discord.com/api/webhooks/1404941903144685779/gc8DFwfIO5eUnxzoJ1Dqsi-iX68GLUMWz_3sdlkjasd9DDAS7tKfB1qWuYN

## Set channel ID to monitor for input commands [Synced with Server]
Channel ID = 1106947857194165898

## List of discord admins, who can run commands [Synced with Server]
Discord Admin = .rusty,.warp


```

## Usage

### In-Game to Discord

- Any message sent as a `shout` in the in-game chat will appear in your configured Discord chat channel
- Server events (like startup) will be posted to the notification channel

### Discord to In-Game

- Messages sent in the configured Discord chat channel will appear in the in-game chat
- Commands sent in the configured Discord command channel will be executed on the server

### Discord Commands

Send commands in your designated command channel:
- Commands should start with a command prefix (configurable)
- Example: `listplayers` to list online players
- Example: `save` to save the world

## Commands

**Legend:**
```
- <string:Parameter> - Text parameter
- <int:Parameter> - Number parameter
- <float:Parameter> - Decimal number parameter
- <parameter?> - Optional parameter
- [Admin Only] - Command restricted to registered Discord admins
```
### General Commands

### ❓ `help`
**Description:** List of all available commands  
**Usage:** `!help`

### ⚠️ `listadmins` **[Admin Only]**
**Description:** List of discord admins registered to plugin  
**Usage:** `!listadmins`

---

### Player Management

### 🐉 `listplayers` **[Admin Only]**
**Description:** List of active players with their positions  
**Usage:** `!listplayers`

### ❌ `kick` **[Admin Only]**
**Description:** Kicks player from server  
**Usage:** `!kick <string:PlayerName>`

### 🎁 `give` **[Admin Only]**
**Description:** Adds item directly into player inventory  
**Usage:** `!give <string:PlayerName> <string:ItemName> <int:Stack> <int?:Quality> <int?:Variant>`  
**Example:** `!give PlayerName IronSword 1 3 0`

### 🌹 `pos` **[Admin Only]**
**Description:** Get player's current position coordinates  
**Usage:** `!pos <string:PlayerName>`

### 🐅 `die` **[Admin Only]**
**Description:** Kills specified player  
**Usage:** `!die <string:PlayerName>`

---

### Teleportation Commands

### 🏃 `teleport` **[Admin Only]**
**Description:** Teleport player to location, bed, or another player  
**Usage:**
- `!teleport <string:PlayerName> bed` - Teleport to bed
- `!teleport <string:PlayerName> <string:OtherPlayerName>` - Teleport to another player
- `!teleport <string:PlayerName> <float:x> <float:y> <float:z>` - Teleport to coordinates

### ⛳ `teleportall` **[Admin Only]**
**Description:** Teleports all players to specified coordinates  
**Usage:** `!teleportall <float:x> <float:y> <float:z>`

---

### Environment & Weather

### 🌪️ `listenv`
**Description:** List of available environments  
**Usage:** `!listenv`

### ✨ `env` **[Admin Only]**
**Description:** Force environment on all players  
**Usage:** `!env <string:EnvironmentName>`  
**Example:** `!env Twilight_Clear`

### ✨ `resetenv` **[Admin Only]**
**Description:** Reset environment on all players to default  
**Usage:** `!resetenv`

---

### Spawning & Creatures

### ❗ `spawn` **[Admin Only]**
**Description:** Spawns prefab at location  
**Usage:**
- `!spawn <string:PrefabName> <int:Level> <string:PlayerName>` - Spawn at player location
- `!spawn <string:PrefabName> <int:Level> <float:x> <float:y> <float:z>` - Spawn at coordinates  
  **Example:** `!spawn Troll 3 PlayerName`

---

### Events

### 🌙 `listevents` **[Admin Only]**
**Description:** List of available event names  
**Usage:** `!listevents`

### ⭐ `event` **[Admin Only]**
**Description:** Starts an event on a player  
**Usage:** `!event <string:EventName> <string:PlayerName>`  
**Example:** `!event Wolves PlayerName`

---

### Player Effects & Skills

### 🚀 `liststatus`
**Description:** List of available status effects  
**Usage:** `!liststatus`

### 🍕 `addstatus` **[Admin Only]**
**Description:** Add status effect to player  
**Usage:** `!addstatus <string:PlayerName> <string:StatusEffect> <float:Duration>`  
**Example:** `!addstatus PlayerName Rested 300`

### 🙏 `listskills`
**Description:** List of available skills  
**Usage:** `!listskills`

### 💪 `raiseskill` **[Admin Only]**
**Description:** Raises player's skill level  
**Usage:** `!raiseskill <string:PlayerName> <string:SkillType> <float:Amount>`  
**Example:** `!raiseskill PlayerName Swords 10`

---

### Server Management

### 💾 `save` **[Admin Only]**
**Description:** Save player profiles and world  
**Usage:** `!save`

### 🛑 `shutdown` **[Admin Only]**
**Description:** Save player profiles, save world and shutdown server  
**Usage:** `!shutdown`  
**Note:** Bot cannot restart the server after shutdown

### 😊 `message` **[Admin Only]**
**Description:** Broadcast message to all players (appears center screen)  
**Usage:** `!message <message text>`  
**Example:** `!message Server restart in 5 minutes!`

### 🦄 `setkey` **[Admin Only]**
**Description:** Set global key (affects world state)  
**Usage:** `!setkey <string:GlobalKeyName>`  
**Example:** `!setkey defeated_bonemass`

---

### Admin Management

### 🔑 `addadmin` **[Admin Only]**
**Description:** Adds discord username to admin list  
**Usage:** `!addadmin <string:Username>`

### 🔒 `removeadmin` **[Admin Only]**
**Description:** Remove discord username from admin list  
**Usage:** `!removeadmin <string:Username>`

---

### Notes

- **Admin Commands:** Commands marked with **[Admin Only]** can only be used by Discord users registered in the admin list
- **Player Names:** Use exact player names as they appear in-game (case sensitive)
- **Coordinates:** Use world coordinates (you can get these with the `pos` command)
- **Item Names:** Use exact prefab names from the game
- **Error Handling:** The bot will respond with error messages if commands fail or parameters are incorrect