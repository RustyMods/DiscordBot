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