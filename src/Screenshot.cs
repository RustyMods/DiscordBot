using System;
using System.Collections;
using System.Threading.Tasks;
using HarmonyLib;
using uGIF;
using UnityEngine;

namespace DiscordBot;

public class Screenshot : MonoBehaviour
{
    [Header("Screenshot Settings")]
    private static int width => DiscordBotPlugin.ScreenshotResolution.width;
    private static int height => DiscordBotPlugin.ScreenshotResolution.height;
    public static Screenshot? instance;

    private Texture2D recordedFrame = null!;
    private bool isCapturing;

    [Header("Discord message")]
    private string playerName = string.Empty;
    public string message = string.Empty;
    private string thumbnail = string.Empty;

    private GameObject m_chatWindow = null!;
    
    public void Awake()
    {
        instance = this;
        recordedFrame = new Texture2D(width, height, TextureFormat.RGB24, false);
    }

    public void Start()
    {
        m_chatWindow = Chat.instance.m_chatWindow.Find("root").gameObject;
    }

    public void Update()
    {
        if (DiscordBotPlugin.SelfieKey is KeyCode.None) return;
        if (Input.GetKey(DiscordBotPlugin.SelfieKey) && !isCapturing) StartSelfie();
    }

    public void OnDestroy()
    {
        instance = null;
    }

    private IEnumerator DelayedCaptureFrame()
    {
        HideHud();
        yield return new WaitForSeconds(DiscordBotPlugin.ScreenshotDelay);
        yield return new WaitForEndOfFrame();
        Texture2D? frame = ScreenCapture.CaptureScreenshotAsTexture();

        ShowHud();

        try
        {
            Image img = new(frame);
            img.ResizeBilinear(width, height);
            recordedFrame.Reinitialize(width, height);
            recordedFrame.SetPixels32(img.pixels);
            recordedFrame.Apply();

        }
        catch (Exception ex)
        {
            DiscordBotPlugin.LogWarning($"Failed to resize recorded frame: {ex.Message}");
            isCapturing = false;
            yield break;
        }
        
        byte[]? bytes = recordedFrame.EncodeToPNG();
        if (bytes is null || bytes.Length == 0)
        {
            DiscordBotPlugin.LogWarning("Failed to encode recorded frame");
            isCapturing = false;
            yield break;
        }
        
        SendToDiscord(bytes);
        isCapturing = false;
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

    public void HideHud()
    {
        Hud.instance.m_userHidden = true;
        Hud.instance.m_hudPressed = 0.0f;
        m_chatWindow.SetActive(false);
        Console.instance.gameObject.SetActive(false);
    }

    public void ShowHud()
    {
        Hud.instance.m_userHidden = false;
        Hud.instance.m_hudPressed = 0.0f;
        m_chatWindow.SetActive(true);
        Console.instance.gameObject.SetActive(true);
    }

    private IEnumerator DelayedSelfie()
    {
        HideHud();
        yield return new WaitForSeconds(DiscordBotPlugin.ScreenshotDelay);
        yield return new WaitForEndOfFrame();
        Texture2D? frame = ScreenCapture.CaptureScreenshotAsTexture();

        ShowHud();

        try
        {
            Image img = new(frame);
            img.ResizeBilinear(width, height);
            recordedFrame.Reinitialize(width, height);
            recordedFrame.SetPixels32(img.pixels);
            recordedFrame.Apply();
        }
        catch (Exception ex)
        {
            DiscordBotPlugin.LogWarning($"Failed to resize recorded frame: {ex.Message}");
            isCapturing = false;
            yield break;
        }
        
        byte[] bytes = recordedFrame.EncodeToPNG();
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
        
    public void SendToDiscord(byte[] data)
    {
        Discord.instance?.SendImageMessage(Webhook.DeathFeed, playerName, message, data, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}.png", thumbnail: thumbnail);
    }
    public void SendSelfieToDiscord(byte[] bytes)
    {
        Discord.instance?.SendImageMessage(Webhook.Chat, Player.m_localPlayer.GetPlayerName(), "Selfie!", bytes, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
    }
}