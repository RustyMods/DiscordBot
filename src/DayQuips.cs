using System;
using System.IO;
using System.Linq;
using BepInEx;

namespace DiscordBot;

public static class DayQuips
{
    private static readonly Dir QuipsDir = new Dir(DiscordBotPlugin.directory.Path, "DayQuips");
    private static readonly Random random = new Random();

    private static string[] GenericDayQuips =
    {
        "A new dawn rises on day {day}. Time to make it count!",
        "Day {day}, same world, new possibilities.",
        "The sun rises again. Welcome to day {day}!",
        "It’s day {day}. Still alive, still fighting.",
        "Another sunrise, another chance. Day {day} begins.",
        "Day {day}: May Odin grant you better luck than yesterday.",
        "The saga continues on day {day}. Let’s see what chaos unfolds."
    };

    private static string[] MilestoneQuips =
    {
        "Day {day}! That’s quite the milestone. Keep surviving!",
        "Day {day}: Legends are made from days like these.",
        "You've endured {day} days. Impressive. Or concerning.",
        "{day} days in — still standing tall. Mostly.",
        "Day {day}. You’ve come far, but the gods are watching closely..."
    };

    private static string[] EarlyDaysQuips =
    {
        "Day {day} and already off to a strong start.",
        "Ah, day {day}. Fresh, bright, and full of naive optimism.",
        "It’s only day {day}, and trouble’s already brewing.",
        "Welcome to day {day}. The adventure has just begun."
    };

    private static string[] LateDaysQuips =
    {
        "Day {day}. You've seen things no mortal should.",
        "After {day} days, you’ve earned your place among the hardy few.",
        "The world grows older with you. Day {day}.",
        "Day {day} — if the mead hasn’t run out, it’s a miracle.",
        "Surviving {day} days? The sagas will speak of this."
    };

    public static string GenerateNewDayQuip(int dayNumber)
    {
        string[] selectedTemplates;

        if (dayNumber <= 3)
        {
            selectedTemplates = EarlyDaysQuips;
        }
        else if (dayNumber % 10 == 0)
        {
            selectedTemplates = MilestoneQuips;
        }
        else if (dayNumber >= 50)
        {
            selectedTemplates = LateDaysQuips;
        }
        else
        {
            selectedTemplates = GenericDayQuips;
        }

        var template = selectedTemplates[random.Next(selectedTemplates.Length)];
        return template.Replace("{day}", dayNumber.ToString());
    }

    public static void Setup()
    {
        string[] files = QuipsDir.GetFiles(".txt", true);
        if (files.Length == 0) WriteDefaults();
        else
        {
            foreach (var file in files)
            {
                string? name = Path.GetFileNameWithoutExtension(file);
                string[] list = File.ReadAllLines(file);
                switch (name)
                {
                    case nameof(GenericDayQuips):
                        GenericDayQuips = list;
                        break;
                    case nameof(MilestoneQuips):
                        MilestoneQuips = list;
                        break;
                    case nameof(EarlyDaysQuips):
                        EarlyDaysQuips = list;
                        break;
                    case nameof(LateDaysQuips):
                        LateDaysQuips = list;
                        break;
                }
            }
        }

        FileSystemWatcher watcher = new FileSystemWatcher(QuipsDir.Path, "*.txt");
        watcher.EnableRaisingEvents = true;
        watcher.IncludeSubdirectories = true;
        watcher.NotifyFilter = NotifyFilters.LastWrite;
        watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;

        DiscordBotPlugin.LogDebug("Initializing day quips");
    }

    private static void OnChanged(object sender, FileSystemEventArgs e)
    {
        string name = Path.GetFileNameWithoutExtension(e.FullPath);
        string[] list = File.ReadAllLines(e.FullPath);
        switch (name)
        {
            case nameof(GenericDayQuips):
                GenericDayQuips = list;
                break;
            case nameof(MilestoneQuips):
                MilestoneQuips = list;
                break;
            case nameof(EarlyDaysQuips):
                EarlyDaysQuips = list;
                break;
            case nameof(LateDaysQuips):
                LateDaysQuips = list;
                break;
        }
    }

    private static void WriteDefaults()
    {
        QuipsDir.WriteAllLines(nameof(GenericDayQuips) + ".txt", GenericDayQuips.ToList());
        QuipsDir.WriteAllLines(nameof(MilestoneQuips) + ".txt", MilestoneQuips.ToList());
        QuipsDir.WriteAllLines(nameof(EarlyDaysQuips) + ".txt", EarlyDaysQuips.ToList());
        QuipsDir.WriteAllLines(nameof(LateDaysQuips) + ".txt", LateDaysQuips.ToList());
    }
}