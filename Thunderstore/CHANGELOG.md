# 1.2.5
- prompts now are broadcasted and sent to discord as well
- new discord command: `!prompt` [text] (uses server's key)
- modified how messages sent from discord are parsed to allow for chat and command channel to share same ID
- if chat and command channel are the same, I check if first argument is a command and try to run it, else runs as a chat message
- fixed new day notifications not working on dedicated servers
- updated manifest bepinex dependency
- added beta feature: jobs - which can run commands on intervals (see readme for details)

# 1.2.4
- Added more details to events
- Added new day notifications
- Added ChatAI, with multiple AI API provider options
- Required: `add your own AI API key to use ChatAI`, not synced with server, so each client will need to provide their own API Key
- Added chat command: `/prompt`
- removed local player check for chat, will use world name if no local player, this allows other mods that sends text to trigger forwarding to discord.

# 1.2.3
- Fixed screenshot resizing
- Added feature request for multiple webhook notifications
- Added random event notification
- Added author to !whisper command, to know who send whisper

# 1.2.2
- Overhaul screen capture to use built-in Unity Screen Capture instead of Camera RenderTexture
- to avoid render texture related crashes (perhaps due Graphic API differences)
- Downside, I need to hide Hud, Chat and Console while capturing to hide UI overlays
- Upside, it is simpler resource management and no direct camera manipulation

# 1.2.1
- show chat panel whenever discord sends message to game

# 1.2.0
- fixed wrong update version

# 1.1.3
- Added null checks to new chat messages

# 1.1.21
- Configurable hotkey to take screenshot (default: `None`), requested.

# 1.1.2
- Added death screenshot `configurable On/Off`
- Added config for death webhook URL, to separate from notifications
- If you want to keep using notification URL, just input same URL
- new in-game chat command: `/selfie`
- Tweaked config layout, added category `Death Feed`
- Added death GIF, if `On`, records death and creates gif instead of taking a delayed screenshot
- GIF Configs `FPS`, `Duration`, `Resolution`
- If feed is not appearing, GIF file size might be too large, try lowering settings

# 1.1.1
- **Fixed**: Resolved TaskCanceledException during WebSocket reconnection attempts
    - Caused stack trace errors when heartbeat acknowledgment failed
    - Now handles task cancellation by checking connection state changes

# 1.1.0
- Switched to websocket for security reasons
- **REQUIRED ACTION**: You must enable `Message Content Intent` in Discord Developer Portal
    - Go to https://discord.com/developers/applications
    - Select your bot application
    - Navigate to the "Bot" section
    - Enable "Message Content Intent" under "Privileged Gateway Intents"
    - **Your bot will not receive message content without this setting enabled**

**Important**: Bots in 100+ servers require Discord verification to use Message Content Intent.

# 1.0.11
- fixed (in-game) printing as $label_ingame

# 1.0.1
- small code clean-up
- fixed `give item` command `food` showing up when eating
- added API for other plugin's to add commands
- added death quips, configurable, name of file must not be changed or it won't be able to update

# 1.0.0
- Initial release