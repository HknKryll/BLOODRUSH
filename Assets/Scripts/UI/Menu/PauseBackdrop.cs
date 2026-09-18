using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Bloodrush.UI.Menu
{
// Pause acilinca oyun dunyasini ORTEN tam ekran arka plan. Duz renk (varsayilan siyah)
// ya da bir Sprite — secim, opaklik ve fade suresi MenuTheme asset'inden gelir.
//
// Eski surum ekrani 1/8 cozunurlukte yakalayip bulanik gosteriyordu (ScreenCapture +
// RenderTexture); istenen artik opak bir arka plan oldugu icin o yol kaldirildi.
// Gerekirse git gecmisinde (d960cf1) duruyor.
public class PauseBackdrop : MonoBehaviour
{
    // SIRALAMA: tum HUD'un (en yuksek Notification = 150) ONUNDE, pause panelinin (200),
    // ayarlarin (300) ve onay penceresinin (400) ARKASINDA. Ayri canvas oldugu icin
    // siralama acikca belirli ve fade'i panelden bagimsiz.
    public const int SortingOrder = 190;

    MenuTheme   theme;
    Canvas      canvas;
    CanvasGroup group;
    Coroutine   fade;

    static bool warnedMissingSprite;

    public void Build(MenuTheme theme)
    {
        this.theme = theme;

        canvas = MenuUI.CreateCanvas("PauseBackdropCanvas", SortingOrder);
        canvas.transform.SetParent(transform, false);

        var root = MenuUI.Stretch(canvas.transform, "Backdrop");
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha          = 0f;
        group.blocksRaycasts = true;   // arkadaki HUD'a tiklanmasin
        group.interactable   = false;

        bool useSprite = theme.pauseBackdropMode == MenuTheme.PauseBackdropMode.Sprite;
        if (useSprite && theme.pauseBackdropSprite == null)
        {
            if (!warnedMissingSprite)
            {
                Debug.LogWarning("[PauseBackdrop] Mod 'Sprite' ama MenuTheme'de sprite atanmamis — " +
                                 "duz renge dusuluyor.");
                warnedMissingSprite = true;
            }
            useSprite = false;
        }

        // TABAN: her iki modda da tam ekran, opak. Sprite modunda SIYAH olur ki gorselde
        // seffaf piksel varsa bile oyun dunyasi arkadan sizmasin.
        var baseImg = root.gameObject.AddComponent<Image>();
        baseImg.color         = useSprite ? Color.black : theme.pauseBackdropColor;
        baseImg.raycastTarget = true;

        if (useSprite) BuildSprite(root, theme.pauseBackdropSprite);
    }

    // Gorsel ekrani KAPLAR ama bozulmaz: EnvelopeParent en-boy oranini koruyarak ebeveyni
    // tamamen doldurur, tasan kisim ekran disinda kalir. 16:9 / 21:9 / 16:10'da ayni sonuc.
    static void BuildSprite(RectTransform parent, Sprite sprite)
    {
        var go = new GameObject("Sprite");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);

        var img = go.AddComponent<Image>();
        img.sprite        = sprite;
        img.raycastTarget = false;

        var fitter = go.AddComponent<AspectRatioFitter>();
        fitter.aspectMode  = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;
    }

    public void FadeIn()  => StartFade(theme.pauseBackdropOpacity);

    // Kapanis: bitince cagiran (PauseMenuController) yok eder.
    public IEnumerator FadeOut()
    {
        StartFade(0f);
        while (fade != null) yield return null;
    }

    // TEK COROUTINE YUVASI: fade bitmeden ESC'ye yeniden basilirsa yenisi MEVCUT alpha'dan
    // devam eder — iki fade ayni CanvasGroup uzerinde kavga etmez, alpha sicramaz.
    void StartFade(float target)
    {
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(Fade(target));
    }

    IEnumerator Fade(float target)
    {
        float start = group.alpha;
        float dur   = Mathf.Max(0.0001f, theme.pauseBackdropFade);
        float t     = 0f;

        while (t < dur)
        {
            // Pause'da Time.timeScale = 0 — scaled deltaTime ile fade HIC oynamazdi.
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / dur));
            yield return null;
        }

        group.alpha = target;
        fade = null;
    }
}
}
