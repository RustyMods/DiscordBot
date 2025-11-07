using System;
using System.Linq;

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
    
    public static string ObfuscateURL(this string url)
    {
        if (string.IsNullOrEmpty(url)) return url;
    
        string[] parts = url.Split('/');
        int webhookIndex = Array.IndexOf(parts, "webhooks");
    
        if (webhookIndex == -1 || webhookIndex >= parts.Length - 2) return url;
    
        string prefix = string.Join("/", parts.Take(webhookIndex + 1));
        string webhookId = parts[webhookIndex + 1];
        string token = parts[webhookIndex + 2];
    
        string safeId = webhookId.Length > 6 
            ? webhookId.Substring(0, 6) + "***" 
            : new string('*', webhookId.Length);
        
        string safeToken = token.Length > 4 
            ? token.Substring(0, 4) + "***" 
            : new string('*', token.Length);
    
        return $"{prefix}/{safeId}/{safeToken}";
    }
}