using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace DiscordBot.Jobs;

public class JobManager : MonoBehaviour
{
    public static readonly List<Job> jobs = new();
    private static readonly Dictionary<string, Job> fileJobMap = new();
    private static readonly Dir JobDir = new Dir(DiscordBotPlugin.directory.Path, "Jobs");
    
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    private static class ZNet_Awake_Patch
    {
        [UsedImplicitly]
        private static void Postfix(ZNet __instance)
        {
            if (!__instance.IsServer()) return;
            DiscordBotPlugin.m_instance.gameObject.AddComponent<JobManager>();
        }
    }
    public void Awake()
    {
        Read();
    }

    public void Read()
    {
        foreach (string file in JobDir.GetFiles("*.yml"))
        {
            if (!Parse(file, out string command, out float interval, out string[] args)) continue;
            var job = new Job(command, interval, args);
            fileJobMap[file] = job;
            DiscordBotPlugin.LogDebug("Registered job: " + Path.GetFileName(file));
        }
    }

    public void SetupFileWatch()
    {
        FileSystemWatcher watcher = new FileSystemWatcher(JobDir.Path, "*.yml");
        watcher.NotifyFilter = NotifyFilters.LastWrite;
        watcher.EnableRaisingEvents = true;
        watcher.IncludeSubdirectories = true;
        watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        watcher.Changed += OnChange;
        watcher.Created += OnChange;
        watcher.Deleted += OnChange;
    }

    public void OnChange(object sender, FileSystemEventArgs e)
    {
        var path = e.FullPath;
        float oldTimer = 0f;
        if (fileJobMap.TryGetValue(path, out var job))
        {
            oldTimer = job.timer;
            jobs.Remove(job);
            fileJobMap.Remove(path);
            
            DiscordBotPlugin.LogDebug("Removed job: " + Path.GetFileName(path));
        }

        if (!Parse(path, out var command, out var interval, out var args)) return;
        var j = new Job(command, interval, args)
        {
            timer = oldTimer
        };
        fileJobMap[path] = j;
        DiscordBotPlugin.LogDebug("Registered job: " + Path.GetFileName(path));
    }

    public void FixedUpdate()
    {
        if (!DiscordBotPlugin.JobsEnabled || jobs.Count == 0) return;
        float dt = Time.deltaTime;
        foreach (Job job in jobs) job.Update(dt);
    }

    private static bool Parse(string file, out string command, out float interval, out string[] args)
    {
        command = null;
        args = Array.Empty<string>();
        interval = 0f;
        
        string[] lines = File.ReadAllLines(file);
        
        foreach (string line in lines)
        {
            var lower = line.ToLower();
            if (lower.Contains("command:"))
            {
                var parts = line.Split(':');
                if (parts.Length < 2)
                {
                    DiscordBotPlugin.LogError($"Failed to parse job: {Path.GetFileName(file)}");
                    return false;
                }
                command = parts[1].Trim();
            }
            else if (lower.Contains("args:"))
            {
                var parts = line.Split(':');
                if (parts.Length < 2)
                {
                    DiscordBotPlugin.LogError($"Failed to parse job: {Path.GetFileName(file)}");
                    return false;
                }
                args = parts[1].Split(',');
            }
            else if (lower.Contains("interval:"))
            {
                var parts = line.Split(':');
                if (parts.Length < 2)
                {
                    DiscordBotPlugin.LogError($"Failed to parse job: {Path.GetFileName(file)}");
                    return false;
                }

                if (!float.TryParse(parts[1].Trim(), out interval))
                {
                    DiscordBotPlugin.LogError($"Failed to parse job: {Path.GetFileName(file)}");
                    return false;
                }
            }
        }

        if (command is null || interval == 0f)
        {
            DiscordBotPlugin.LogError($"Failed to parse job: {Path.GetFileName(file)}");
            return false;
        }
        
        return true;
    }
}