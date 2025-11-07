using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using uGIF;
using UnityEngine;

namespace DiscordBot;

public class Recorder : MonoBehaviour
{
    [Header("Discord message")]
    private string playerName = string.Empty;
    public string message = string.Empty;
    private string thumbnail = string.Empty;

    [Header("GIF Settings")] 
    private readonly List<Image> recordedImages = new();
    
    private bool isRecording;
    private float recordStartTime;
    private Coroutine? recordingCoroutine;
    private byte[]? gifBytes;
    private static int gifHeight => DiscordBotPlugin.GifResolution.height;
    private static int gifWidth => DiscordBotPlugin.GifResolution.width;
    private static int fps => DiscordBotPlugin.GIF_FPS;
    private static float recordDuration => DiscordBotPlugin.GIF_DURATION;

    public static Recorder? instance;

    public void Awake()
    {
        instance = this;
        
        DiscordBotPlugin.LogDebug("Initializing GIF recorder");
    }
    
    public void OnDestroy()
    {
        instance = null;
    }
    
    public void StartRecording(string player, string quip, string avatar)
    {
        if (isRecording) return;
        playerName = player;
        message = quip;
        thumbnail = avatar;
        isRecording = true;
        recordStartTime = Time.time;
        if (recordingCoroutine != null) StopCoroutine(recordingCoroutine);
        recordingCoroutine = StartCoroutine(Record());
        DiscordBotPlugin.LogDebug("Starting gif recording");
    }
    
    private IEnumerator Record()
    {
        Screenshot.instance?.HideHud();
        float interval = 1f / fps;
        
        while (isRecording && Time.time - recordStartTime < recordDuration)
        {
            yield return new WaitForEndOfFrame();
            Image img = new Image(ScreenCapture.CaptureScreenshotAsTexture());
            recordedImages.Add(img);
            yield return new WaitForSeconds(interval);
        }
        isRecording = false;
        Screenshot.instance?.ShowHud();
        
        Thread thread = new Thread(CreateGif);
        thread.Start();
        StartCoroutine(WaitForBytes());
    }
    
    private IEnumerator WaitForBytes()
    {
        while (gifBytes == null) yield return null;
        SendGif(gifBytes);
        Cleanup();
    }

    public void Cleanup()
    {
        recordedImages.Clear();
        gifBytes = null;
    }
    
    private void CreateGif()
    {
        GIFEncoder encoder = new GIFEncoder
        {
            useGlobalColorTable = true,
            repeat = 0,
            FPS = fps,
            transparent = new Color32(255, 0, 255, 255),
            dispose = 1
        };

        MemoryStream stream = new MemoryStream();
        encoder.Start(stream);
        foreach (Image? img in recordedImages)
        {
            img.ResizeBilinear(gifWidth, gifHeight);
            img.Flip();
            encoder.AddFrame(img);
        }
        encoder.Finish();
        gifBytes = stream.ToArray();
        stream.Close();
    }
    
    private void SendGif(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            DiscordBotPlugin.LogWarning("GIF bytes are null or empty");
            return;
        }
        Discord.instance?.SendGifMessage(Webhook.DeathFeed, playerName, message, bytes, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}.gif", thumbnail: thumbnail);
        var worldName = ZNet.instance?.GetWorldName() ?? "Server";
        Discord.instance?.BroadcastMessage(worldName, message, false);
    }
}