using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DiscordBot;

public class ImageHud : MonoBehaviour
{
    private static bool m_loaded;
    [HarmonyPatch(typeof(Tutorial), nameof(Tutorial.Awake))]
    private static class Tutorial_Awake_Patch
    {
        private static void Postfix(Tutorial __instance)
        {
            if (m_loaded) return;
            var root = Object.Instantiate(__instance.transform.Find("Tutorial_wnd").gameObject, __instance.transform.parent);
            root.name = "DiscordImage";
            root.AddComponent<ImageHud>();
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            root.SetActive(true);
            m_loaded = true;
        }
    }
    public static ImageHud? instance;
    public RectTransform? m_rect;
    public Image m_bkg = null!;
    public TMP_Text? m_topic;
    public TMP_Text? m_text;
    public TMP_Text? m_closedText;
    
    private readonly float m_fadeDuration = 0.5f;
    private bool m_fading = false;
    private Color m_currentColor = Color.clear;
    private Color m_targetColor = Color.clear;

    public void Awake()
    {
        instance = this;
        m_rect = GetComponent<RectTransform>();
        m_bkg = transform.Find("bkg").GetComponent<Image>();
        m_topic = transform.Find("Topic").GetComponent<TMP_Text>();
        m_text = transform.Find("Text").GetComponent<TMP_Text>();
        m_closedText = transform.Find("CloseText").GetComponent<TMP_Text>();

        m_topic.gameObject.SetActive(false);
        m_text.gameObject.SetActive(false);
        m_closedText.gameObject.SetActive(false);

        m_bkg.preserveAspect = true;
        m_bkg.type = Image.Type.Simple;
        m_bkg.color = Color.clear;
    }

    public void Update()
    {
        if (!m_fading) return;

        m_currentColor = Color.Lerp(
            m_currentColor,
            m_targetColor,
            Time.deltaTime / m_fadeDuration
        );

        m_bkg.color = m_currentColor;

        if (!(Mathf.Abs(m_currentColor.a - m_targetColor.a) < 0.01f)) return;
        m_currentColor = m_targetColor;
        m_bkg.color = m_targetColor;
        m_targetColor = Color.clear;

        if (m_currentColor == Color.clear)
        {
            m_fading = false;
        }
    }

    public void OnDestroy()
    {
        instance = null;
    }

    public void ShowInstant(Sprite image)
    {
        m_bkg.sprite = image;
        m_currentColor = Color.white;
        m_bkg.color = Color.white;
        Invoke(nameof(StartFadeOut), 0.5f);
    }

    private void StartFadeOut()
    {
        m_targetColor = Color.clear;
        m_fading = true;
    }

    public void Show(Sprite image)
    {
        m_bkg.sprite = image;
        m_targetColor = Color.white;
        m_fading = true;
    }

    public void Hide()
    {
        m_targetColor = Color.clear;
        m_fading = true;
    }
}