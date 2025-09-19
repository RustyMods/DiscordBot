namespace DiscordBot;

public enum TextFormat
{
    Bold,
    Italic,
    BoldItalic,
    Strikethrough,
    InlineCode,
    None
}
public static class Formatting
{
    public static string Format(this string text, TextFormat format)
    {
        return format switch
        {
            TextFormat.Bold => $"**{text}**",
            TextFormat.Italic => $"*{text}*",
            TextFormat.BoldItalic => $"***{text}***",
            TextFormat.Strikethrough => $"~~{text}~~",
            TextFormat.InlineCode => $"`{text}`",
            _ => text
        };
    }
}