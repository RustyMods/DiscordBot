using System;
using System.Collections;
using UnityEngine;

namespace DiscordBot;

public class DeathRecorder : MonoBehaviour
{
    private static Camera camera => Utils.GetMainCamera();

    private static int width => 512;
    private static int height => 288;
    public static DeathRecorder? instance;

    private Texture2D? recordedFrame;
    private RenderTexture? renderTexture;

    private string playerName = string.Empty;
    private string message = string.Empty;
    private string thumbnail = string.Empty;

    private static readonly WaitForSeconds delay = new (0.3f);
    public void Awake()
    {
        instance = this;
        renderTexture = new RenderTexture(width, height, 24);
        renderTexture.Create();
    }

    public void OnDestroy()
    {
        instance = null;
    }

    private IEnumerator DelayedCaptureFrame()
    {
        yield return delay;
        RenderTexture previousTarget = camera.targetTexture;
        camera.targetTexture = renderTexture;
        
        camera.Render();

        RenderTexture.active = renderTexture;
        var frame = new Texture2D(width, height, TextureFormat.RGB24, false);
        frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        frame.Apply();

        recordedFrame = frame;

        RenderTexture.active = null;
        camera.targetTexture = previousTarget;
        
        SendToDiscord(playerName, message, thumbnail);
    }

    public void CaptureFrame(string player, string quip, string avatar)
    {
        playerName = player;
        message = quip;
        thumbnail = avatar;
        StartCoroutine(DelayedCaptureFrame());
    }

    public void SendToDiscord(string player, string quip, string avatar)
    {
        if (recordedFrame is null) return;
        Discord.instance?.SendImageMessage(Webhook.DeathFeed, player, quip, recordedFrame.EncodeToPNG(), $"{DateTime.UtcNow:yyyyMMdd_HHmmss}.png", thumbnail: avatar);
        DestroyImmediate(recordedFrame);
        recordedFrame = null;
    }
}