using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Bloodrush.UI.Menu
{
// Pause acilinca arkadaki oyun goruntusunu BULANIK ve KOYU gosterir.
//
// NEDEN SHADER YOK: HDRP'de tam ekran bulanıklık normalde bir CustomPass ya da ozel
// post-process gerektirir — kurulumu kirilgan ve surum bagimli. Bunun yerine kareyi
// 1/8 cozunurluklu bir RenderTexture'a alip bilinear filtreyle tekrar buyutuyoruz:
// kucultup buyutmek ZATEN bir kutu bulanıklığıdır. Tek karelik islem, sifir shader,
// HDRP'de guvenilir.
//
// Ustune krem tonlu koyu bir perde biniyor (skill: modal scrim, %40-60 arasi).
public class PauseBackdrop : MonoBehaviour
{
    [Tooltip("Bölme katsayısı — büyük = daha bulanık ve daha ucuz.")]
    const int Downscale = 8;

    RenderTexture rt;
    RawImage      image;
    Image         scrim;
    MenuTheme     theme;

    public void Build(Transform parent, MenuTheme theme)
    {
        this.theme = theme;

        var rawRT = MenuUI.Stretch(parent, "Backdrop");
        image = rawRT.gameObject.AddComponent<RawImage>();
        image.color = new Color(1f, 1f, 1f, 0f);   // yakalanana kadar görünmez
        image.raycastTarget = true;                // arkadaki dünyaya tıklanmasın

        var scrimRT = MenuUI.Stretch(parent, "Scrim");
        scrim = scrimRT.gameObject.AddComponent<Image>();
        // Krem tonlu koyu perde — soğuk siyah yerine tema ile uyumlu.
        scrim.color = new Color(0.165f, 0.160f, 0.150f, 0f);
        scrim.raycastTarget = false;

        StartCoroutine(Capture());
    }

    IEnumerator Capture()
    {
        // Kareyi ancak cizim bittikten sonra alabiliriz. Time.timeScale 0 olsa bile
        // WaitForEndOfFrame calisir (zaman tabanli degil, kare tabanli).
        yield return new WaitForEndOfFrame();

        int w = Mathf.Max(16, Screen.width  / Downscale);
        int h = Mathf.Max(16, Screen.height / Downscale);

        rt = new RenderTexture(w, h, 0) { filterMode = FilterMode.Bilinear };
        rt.Create();

        try
        {
            ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);
            image.texture = rt;
            image.color   = Color.white;
        }
        catch (System.Exception e)
        {
            // Yakalama basarisiz olursa sadece koyu perde kalir — menu yine calisir.
            Debug.LogWarning($"[PauseBackdrop] Ekran yakalanamadı ({e.Message}) — düz perde kullanılıyor.");
            image.color = new Color(0f, 0f, 0f, 0f);
        }

        // Perdeyi yumuşakça getir
        float t = 0f, dur = theme.enterDuration;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);
            var c = scrim.color; c.a = Mathf.Lerp(0f, 0.55f, p); scrim.color = c;
            yield return null;
        }
    }

    void OnDestroy()
    {
        if (rt != null)
        {
            if (image != null) image.texture = null;
            rt.Release();
            Destroy(rt);
            rt = null;
        }
    }
}
}
