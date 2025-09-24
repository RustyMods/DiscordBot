using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using uGIF;
using UnityEngine;

namespace DiscordBot;

public class Screenshot : MonoBehaviour
{
    private static Camera camera => Utils.GetMainCamera();
    [Header("Screenshot Settings")]
    private static int width => DiscordBotPlugin.ScreenshotResolution.width;
    private static int height => DiscordBotPlugin.ScreenshotResolution.height;
    private static int depth => DiscordBotPlugin.ScreenshotDepth;
    public static Screenshot? instance;

    private Texture2D? recordedFrame;
    private RenderTexture? renderTexture;
    private bool isCapturing;

    [Header("Discord message")]
    private string playerName = string.Empty;
    private string message = string.Empty;
    private string thumbnail = string.Empty;

    [Header("GIF Settings")]
    private readonly List<Texture2D> recordedFrames = new();
    private bool isRecording;
    private float recordStartTime;
    private Coroutine? recordingCoroutine;
    private RenderTexture? gifTexture;
    private byte[]? gifBytes;
    private static int gifHeight => DiscordBotPlugin.GifResolution.height;
    private static int gifWidth => DiscordBotPlugin.GifResolution.width;
    private static int fps => DiscordBotPlugin.GIF_FPS;
    private static float recordDuration => DiscordBotPlugin.GIF_DURATION;
    
    public void Awake()
    {
        instance = this;
        renderTexture = new RenderTexture(width, height, depth);
        renderTexture.Create();

        gifTexture = new RenderTexture(gifWidth, gifHeight, 24);
        gifTexture.Create();
    }

    public void OnResolutionChange()
    {
        if (isCapturing)
        {
            DiscordBotPlugin.LogWarning("Bot is capturing screenshot, cannot change settings");
            return;
        }
        if (renderTexture != null)
        {
            renderTexture.Release();
            DestroyImmediate(renderTexture);
            renderTexture = null;
        }
        renderTexture = new RenderTexture(width, height, depth);
        renderTexture.Create();
    }

    public void OnGifResolutionChange()
    {
        if (isRecording)
        {
            DiscordBotPlugin.LogWarning("Bot is recording GIF, cannot change settings");
            return;
        }

        if (gifTexture != null)
        {
            gifTexture.Release();
            DestroyImmediate(gifTexture);
            gifTexture = null;
        }
        gifTexture = new RenderTexture(gifWidth, gifHeight, 24);
        gifTexture.Create();
    }

    public void OnDestroy()
    {
        instance = null;
    }

    private IEnumerator DelayedCaptureFrame()
    {
        yield return new WaitForSeconds(DiscordBotPlugin.ScreenshotDelay);
        recordedFrame = Capture();
        if (recordedFrame is null)
        {
            isCapturing = false;
            yield break;
        }
        SendToDiscord(recordedFrame.EncodeToPNG());
        isCapturing = false;
    }

    private Texture2D Capture()
    {
        RenderTexture previousTarget = camera.targetTexture;
        camera.targetTexture = renderTexture;
        camera.Render();
        RenderTexture.active = renderTexture;
        Texture2D frame = new Texture2D(width, height, TextureFormat.RGB24, false);
        frame.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
        frame.Apply();
        RenderTexture.active = null;
        camera.targetTexture = previousTarget;
        return frame;
    }

    public void StartCapture(string player, string quip, string avatar)
    {
        if (isCapturing) return;
        playerName = player;
        message = quip;
        thumbnail = avatar;
        isCapturing = true;
        StartCoroutine(DelayedCaptureFrame());
    }

    public void SendToDiscord(byte[] data)
    {
        Discord.instance?.SendImageMessage(Webhook.DeathFeed, playerName, message, data, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}.png", thumbnail: thumbnail);
        DestroyImmediate(recordedFrame);
        recordedFrame = null;
    }

    private IEnumerator DelayedSelfie()
    {
        yield return new WaitForSeconds(DiscordBotPlugin.ScreenshotDelay);
        recordedFrame = Capture();
        if (recordedFrame is null)
        {
            isCapturing = false;
            yield break;
        }
        SendSelfieToDiscord();
        isCapturing = false;
    }

    public void StartSelfie()
    {
        isCapturing = true;
        StartCoroutine(DelayedSelfie());
    }
        

    public void SendSelfieToDiscord()
    {
        Discord.instance?.SendImageMessage(Webhook.Chat, Player.m_localPlayer.GetPlayerName(), "Selfie!", recordedFrame.EncodeToPNG(), $"{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
        DestroyImmediate(recordedFrame);
        recordedFrame = null;
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
    }
    
    private Texture2D CaptureFrame()
    {
        RenderTexture previousTarget = camera.targetTexture;
        camera.targetTexture = gifTexture;
        camera.Render();
        RenderTexture.active = gifTexture;
        Texture2D frame = new Texture2D(gifWidth, gifHeight, TextureFormat.RGB24, false);
        frame.ReadPixels(new Rect(0, 0, gifWidth, gifHeight), 0, 0, false);
        frame.Apply();
        RenderTexture.active = null;
        camera.targetTexture = previousTarget;
        return frame;
    }

    private IEnumerator Record()
    {
        float interval = 1f / fps;
        
        while (isRecording && Time.time - recordStartTime < recordDuration)
        {
            recordedFrames.Add(CaptureFrame());
            yield return new WaitForSeconds(interval);
        }
        isRecording = false;
        Thread thread = new Thread(CreateGif);
        thread.Start();
        StartCoroutine(WaitForBytes());
    }

    private IEnumerator WaitForBytes()
    {
        while (gifBytes == null) yield return null;
        SendGif(gifBytes);
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
        foreach (Texture2D? texture in recordedFrames)
        {
            Image img = new Image(texture);
            img.Flip();
            encoder.AddFrame(img);
        }
        encoder.Finish();
        gifBytes = stream.ToArray();
        stream.Close();
        recordedFrames.Clear();
    }

    private void SendGif(byte[] bytes)
    {
        Discord.instance?.SendGifMessage(Webhook.DeathFeed, playerName, message, bytes, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}.gif", thumbnail: thumbnail);
    }
}