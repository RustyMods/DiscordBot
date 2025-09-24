using System;
using System.Collections;
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
    public void Awake()
    {
        instance = this;
        renderTexture = new RenderTexture(width, height, depth);
        renderTexture.Create();
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
        frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
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
}