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

    private static int width => (int)DiscordBotPlugin.ScreenshotResolution.x;
    private static int height => (int)DiscordBotPlugin.ScreenshotResolution.y;
    // private static int width = 960;
    // private static int height = 540;
    // 0, 16, 24, 32
    private static int depth => DiscordBotPlugin.ScreenshotDepth;
    public static Screenshot? instance;

    private Texture2D? recordedFrame;
    private RenderTexture? renderTexture;
    private bool isCapturing;

    private string playerName = string.Empty;
    private string message = string.Empty;
    private string thumbnail = string.Empty;

    private readonly List<Texture2D> recordedFrames = new();
    private bool isRecording;
    private float recordStartTime;
    private Coroutine? recordingCoroutine;
    private RenderTexture? gifTexture;
    private byte[]? gifBytes = null;
    public void Awake()
    {
        instance = this;
        renderTexture = new RenderTexture(width, height, depth);
        renderTexture.Create();

        gifTexture = new RenderTexture(640, 360, 24);
        gifTexture.Create();
    }

    public void OnResolutionChange()
    {
        if (isCapturing)
        {
            DiscordBotPlugin.LogWarning("Screenshot is capturing, cannot change settings");
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
        Texture2D frame = new Texture2D(640, 360, TextureFormat.RGB24, false);
        frame.ReadPixels(new Rect(0, 0, 640, 360), 0, 0, false);
        frame.Apply();
        RenderTexture.active = null;
        camera.targetTexture = previousTarget;
        return frame;
    }

    private IEnumerator Record()
    {
        while (isRecording && Time.time - recordStartTime < 3f)
        {
            recordedFrames.Add(CaptureFrame());
            yield return new WaitForSeconds(0.08f);
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
        var encoder = new GIFEncoder();
        encoder.useGlobalColorTable = true;
        encoder.repeat = 0;
        encoder.FPS = 30;
        encoder.transparent = new Color32(255, 0, 255, 255);
        encoder.dispose = 1;
        
        var stream = new MemoryStream();
        encoder.Start(stream);
        foreach (var texture in recordedFrames)
        {
            var img = new Image(texture);
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