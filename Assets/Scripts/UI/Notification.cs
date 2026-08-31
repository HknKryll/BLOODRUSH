using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Bloodrush.UI
{
// Kısa ekran bildirimi (ör. "SHOTGUN ELE GEÇİRİLDİ"). BossHealthUI deseni: kendi
// canvas'ını kurar, statik Show() ile sürülür — sahneye bir şey koymana gerek yok.
public class Notification : MonoBehaviour
{
    static Notification instance;

    Text        label;
    CanvasGroup group;
    Coroutine   routine;

    public static void Show(string message, float duration = 2.5f)
    {
        if (instance == null)
            instance = new GameObject("Notification").AddComponent<Notification>();
        instance.Display(message, duration);
    }

    void Awake()
    {
        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    void Display(string message, float duration)
    {
        if (label) label.text = message;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(FadeRoutine(duration));
    }

    IEnumerator FadeRoutine(float duration)
    {
        group.alpha = 1f;
        yield return new WaitForSecondsRealtime(duration);   // duraklamada da akar
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = 1f - t / 0.6f;
            yield return null;
        }
        group.alpha = 0f;
    }

    void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(transform, false);
        group = labelGO.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable   = false;
        group.blocksRaycasts = false;

        label = labelGO.AddComponent<Text>();
        label.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize      = 40;
        label.fontStyle     = FontStyle.Bold;
        label.color         = new Color(1f, 0.85f, 0.3f, 1f);   // altın/sarı
        label.alignment     = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow   = VerticalWrapMode.Overflow;

        var rt = label.rectTransform;
        rt.anchorMin        = new Vector2(0.5f, 0.76f);
        rt.anchorMax        = new Vector2(0.5f, 0.76f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(1400f, 90f);
    }
}
}
