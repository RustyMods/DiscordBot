using System.Collections.Generic;

namespace DiscordBot.Jobs;

public class Job
{
    private readonly string command;
    private readonly string[] args;
    private readonly float interval;

    public float timer;

    public Job(string command, float interval, params string[] args)
    {
        this.command = command;
        this.interval = interval;
        var arguments = new List<string>(){command};
        arguments.AddRange(args);
        this.args = arguments.ToArray();
        
        JobManager.jobs.Add(this);
    }

    private void Run()
    {
        if (!DiscordCommands.m_commands.TryGetValue(command, out var method)) return;
        method.Run(args, ZNet.instance?.GetWorldName());
    }

    public void Update(float dt)
    {
        timer += dt;
        if (timer < interval) return;
        timer = 0.0f;
        
        Run();
    }
}