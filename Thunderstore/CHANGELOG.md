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