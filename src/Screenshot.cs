using System;
using System.Collections;
using System.Threading.Tasks;
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

    private Texture2D recordedFrame = null!;
    private RenderTexture? renderTexture;
    private bool isCapturing;

    [Header("Discord message")]
    private string playerName = string.Empty;
    private string message = string.Empty;
    private string thumbnail = string.Empty;
    
    public void Awake()
    {
        instance = this;
        renderTexture = new RenderTexture(width, height, depth);
        renderTexture.Create();
        recordedFrame = new Texture2D(width, height, TextureFormat.RGB24, false);
    }

    public void Update()
    {
        if (DiscordBotPlugin.SelfieKey is KeyCode.None) return;
        if (Input.GetKey(DiscordBotPlugin.SelfieKey) && !isCapturing) StartSelfie();
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
        DestroyImmediate(recordedFrame);
        recordedFrame = new Texture2D(width, height, TextureFormat.RGB24, false);
    }

    public void OnDestroy()
    {
        instance = null;
    }

    private IEnumerator DelayedCaptureFrame()
    {
        yield return new WaitForSeconds(DiscordBotPlugin.ScreenshotDelay);
        Capture();

        Task<byte[]?> encodingTask = Task.Run(() =>
        {
            try
            {
                return recordedFrame.EncodeToPNG();
            }
            catch (Exception ex)
            {
                DiscordBotPlugin.LogWarning($"Failed to encode recorded frame: {ex.Message}");
                return null;
            }
        });

        while (!encodingTask.IsCompleted)
        {
            yield return null;
        }
        
        byte[]? bytes = encodingTask.Result;
        
        if (bytes is null || bytes.Length == 0)
        {
            DiscordBotPlugin.LogWarning("Failed to encode recorded frame");
            isCapturing = false;
            yield break;
        }
        
        SendToDiscord(bytes);
        isCapturing = false;
    }

    private void Capture()
    {
        RenderTexture previousTarget = camera.targetTexture;
        try
        {
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            recordedFrame.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            recordedFrame.Apply();
        }
        catch (Exception ex)
        {
            DiscordBotPlugin.LogWarning($"Failed to capture frame: {ex.Message}");
        }
        RenderTexture.active = null;
        camera.targetTexture = previousTarget;
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
    }

    private IEnumerator DelayedSelfie()
    {
        yield return new WaitForSeconds(DiscordBotPlugin.ScreenshotDelay);
        Capture();

        var encodingTask = Task.Run(() =>
        {
            try
            {
                return recordedFrame.EncodeToPNG();
            }
            catch (Exception ex)
            {
                DiscordBotPlugin.LogWarning($"Failed to encode recorded frame: {ex.Message}");
                return null;
            }
        });

        while (!encodingTask.IsCompleted)
        {
            yield return null;
        }
        
        var bytes = encodingTask.Result;
        
        if (bytes is null || bytes.Length == 0)
        {
            DiscordBotPlugin.LogWarning("Failed to encode recorded frame");
            isCapturing = false;
            yield break;
        }
        
        SendSelfieToDiscord(bytes);
        isCapturing = false;
    }

    public void StartSelfie()
    {
        isCapturing = true;
        StartCoroutine(DelayedSelfie());
    }
        

    public void SendSelfieToDiscord(byte[] bytes)
    {
        Discord.instance?.SendImageMessage(Webhook.Chat, Player.m_localPlayer.GetPlayerName(), "Selfie!", bytes, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
    }
}