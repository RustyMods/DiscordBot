using System.Collections.Generic;

namespace DiscordBot;

public static class EmojiHelper
{
    private static readonly Dictionary<string, string> Emojis = new ()
    {
        { "smile", "😊" }, { "grin", "😁" }, { "laugh", "😂" }, { "wink", "😉" },
        { "wave", "👋" }, { "clap", "👏" }, { "thumbsup", "👍" }, { "thumbsdown", "👎" },
        { "ok", "👌" }, { "pray", "🙏" }, { "muscle", "💪" }, { "facepalm", "🤦" },
        
        { "dog", "🐶" }, { "cat", "🐱" }, { "mouse", "🐭" }, { "fox", "🦊" },
        { "bear", "🐻" }, { "panda", "🐼" }, { "koala", "🐨" }, { "lion", "🦁" },
        { "tiger", "🐯" }, { "monkey", "🐵" }, { "unicorn", "🦄" }, { "dragon", "🐉" },

        { "tree", "🌳" }, { "palm", "🌴" }, { "flower", "🌸" }, { "rose", "🌹" },
        { "sun", "☀️" }, { "moon", "🌙" }, { "star", "⭐" }, { "rain", "🌧️" },
        { "snow", "❄️" }, { "fire", "🔥" }, { "lightning", "⚡" },

        { "pizza", "🍕" }, { "burger", "🍔" }, { "fries", "🍟" }, { "taco", "🌮" },
        { "cake", "🍰" }, { "donut", "🍩" }, { "coffee", "☕" }, { "tea", "🍵" },
        { "beer", "🍺" }, { "wine", "🍷" },

        { "rocket", "🚀" }, { "car", "🚗" }, { "bike", "🚲" }, { "airplane", "✈️" },
        { "train", "🚆" }, { "bus", "🚌" }, { "ship", "🚢" },
        { "book", "📖" }, { "pencil", "✏️" }, { "pen", "🖊️" }, { "paint", "🎨" },
        { "camera", "📷" }, { "phone", "📱" }, { "computer", "💻" },
        { "gift", "🎁" }, { "balloon", "🎈" }, { "key", "🔑" }, { "lock", "🔒" },

        { "soccer", "⚽" }, { "basketball", "🏀" }, { "football", "🏈" }, { "tennis", "🎾" },
        { "golf", "⛳" }, { "run", "🏃" }, { "swim", "🏊" }, { "ski", "⛷️" },
        { "game", "🎮" }, { "music", "🎵" }, { "guitar", "🎸" }, { "drum", "🥁" },

        { "check", "✅" }, { "x", "❌" }, { "warning", "⚠️" }, { "question", "❓" },
        { "exclamation", "❗" }, { "infinity", "♾️" }, { "heart", "❤️" },
        { "brokenheart", "💔" }, { "sparkle", "✨" }, { "starstruck", "🤩" },
        
        { "plus", "✚" }, { "minus", "━" }, { "tornado", "🌪️" }, { "storm", "⛈️" },
        { "save", "💾" }, { "stop", "🔴" } 
    };
    
    public static string Emoji(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        return Emojis.TryGetValue(name, out var emoji) ? emoji : name;
    }
}