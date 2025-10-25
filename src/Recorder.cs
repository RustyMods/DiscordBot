using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using uGIF;
using UnityEngine;

namespace DiscordBot;

public class Recorder : MonoBehaviour
{
    private static Camera camera => Utils.GetMainCamera();

    [Header("Discord message")]
    private string playerName = string.Empty;
    private string message = string.Empty;
    private string thumbnail = string.Empty;

    [Header("GIF Settings")] 
    private readonly List<Color32[]> recordedFrameData = new();

    private readonly List<Image> recordedImages = new();
    
    // private Texture2D texture = null!;
    private bool isRecording;
    private float recordStartTime;
    private Coroutine? recordingCoroutine;
    // private RenderTexture? gifTexture;
    private byte[]? gifBytes;
    private static int gifHeight => DiscordBotPlugin.GifResolution.height;
    private static int gifWidth => DiscordBotPlugin.GifResolution.width;
    private static int fps => DiscordBotPlugin.GIF_FPS;
    private static float recordDuration => DiscordBotPlugin.GIF_DURATION;

    public static Recorder? instance;

    public void Awake()
    {
        instance = this;
        // gifTexture = new RenderTexture(gifWidth, gifHeight, 24);
        // gifTexture.Create();
        // texture = new Texture2D(gifTexture.width, gifTexture.height, TextureFormat.RGB24, false);
    }
    
    public void OnDestroy()
    {
        instance = null;
    }
    
    public void OnGifResolutionChange()
    {
        // if (isRecording)
        // {
        //     DiscordBotPlugin.LogWarning("Bot is recording GIF, cannot change settings");
        //     return;
        // }
        //
        // if (gifTexture != null)
        // {
        //     gifTexture.Release();
        //     DestroyImmediate(gifTexture);
        //     gifTexture = null;
        // }
        // gifTexture = new RenderTexture(gifWidth, gifHeight, 24);
        // gifTexture.Create();
        //
        // DestroyImmediate(texture);
        // texture = new  Texture2D(gifTexture.width, gifTexture.height, TextureFormat.RGB24, false);
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
    
    private IEnumerator Record()
    {
        Screenshot.instance?.HideHud();
        float interval = 1f / fps;
        
        while (isRecording && Time.time - recordStartTime < recordDuration)
        {
            yield return new WaitForEndOfFrame();
            // recordedFrameData.Add(CaptureFrame());
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
        // recordedFrameData.Clear();
        recordedImages.Clear();
        gifBytes = null;
    }
    
    // private Color32[] CaptureFrame()
    // {
    //     RenderTexture previousTarget = camera.targetTexture;
    //     camera.targetTexture = gifTexture;
    //     camera.Render();
    //     RenderTexture.active = gifTexture;
    //     texture.ReadPixels(new Rect(0, 0, gifWidth, gifHeight), 0, 0, false);
    //     texture.Apply();
    //     RenderTexture.active = null;
    //     camera.targetTexture = previousTarget;
    //
    //     texture = ScreenCapture.CaptureScreenshotAsTexture();
    //     return texture.GetPixels32();
    // }
    
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

        // foreach (Color32[]? pixels in recordedFrameData)
        // {
        //     Image img = new Image(pixels, size.x, size.y);
        //     img.ResizeBilinear(gifWidth, gifHeight);
        //     img.Flip();
        //     encoder.AddFrame(img);
        // }

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
    }
}