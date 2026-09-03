using UnityEngine;
using TMPro;

namespace Bloodrush.UI
{
// Merkezi görsel dil (renk/font/içerik) — kurumsal-arşiv + sıcak-kütüphane hissi.
// Assets/Resources/UITheme.asset olarak oluşturulur (Create → Bloodrush → UI Teması):
// Resources klasöründe olmalı ki BookUI gibi kendi kendini kuran (sahnede referansı
// olmayan) UI script'leri Resources.Load ile bulabilsin.
//
// Asset eksikse/silinmişse Load() null döner — HER kullanan taraf null-check yapıp
// kendi sabit varsayılanına düşer (hiçbir şey kırılmaz, sadece stilsiz görünür).
//
// Fontlar boş bırakılabilir: TMP o zaman TMP_Settings'in varsayılan font asset'ine
// düşer. Fraunces/IBM Plex gibi gerçek fontlar TTF olarak indirip Unity'de
// "Create → TextMeshPro → Font Asset" ile SDF asset'ine çevrilip buraya atanmalı —
// bu adım Editor GUI gerektirir, kod/dosya ile üretilemez.
[CreateAssetMenu(menuName = "Bloodrush/UI Teması", fileName = "UITheme")]
public class UITheme : ScriptableObject
{
    [Header("Renkler")]
    public Color background    = new Color(0.078f, 0.070f, 0.063f);   // #141210
    public Color textPrimary   = new Color(0.937f, 0.906f, 0.847f);   // #efe7d8
    public Color textSecondary = new Color(0.659f, 0.620f, 0.549f);   // #a89e8c
    public Color accent        = new Color(0.788f, 0.635f, 0.290f);   // #c9a24a
    [Range(0f, 1f)] public float hairlineAlpha    = 0.10f;
    [Range(0f, 1f)] public float vignetteStrength = 0.55f;

    [Header("Fontlar (boşsa TMP varsayılanına düşer)")]
    [Tooltip("Başlıklarda kullanılır (ör. Fraunces).")]
    public TMP_FontAsset serifFont;
    [Tooltip("Gövde metni/etiketlerde kullanılır (ör. IBM Plex Sans).")]
    public TMP_FontAsset sansFont;
    [Tooltip("Tuş ipucu / sayaç / belge kodunda kullanılır (ör. IBM Plex Mono).")]
    public TMP_FontAsset monoFont;

    [Header("İçerik")]
    [Tooltip("Menü panelinin sağ üst köşesindeki dekoratif belge kodu.")]
    public string archiveCode = "KONSEY ARŞİVİ / SEC.1";

    static UITheme cached;
    static bool    triedLoad;

    // null dönebilir — çağıran taraf mutlaka null-check yapıp kendi sabit rengine/
    // fontuna düşmeli. Bu yüzden burada "varsayılan asset oluştur" gibi bir şey YOK.
    public static UITheme Load()
    {
        if (!triedLoad)
        {
            cached    = Resources.Load<UITheme>("UITheme");
            triedLoad = true;
        }
        return cached;
    }

    // ───── Paylaşılan prosedürel dokular (shader/asset gerektirmez, bir kez üretilir) ─────
    // Merkezde burada tutuluyor ki ileride başka bir UI ekranı da aynı vinyet/nokta
    // görselini üretmek için texture kodu tekrarlamasın.

    static Sprite vignetteSprite, dotSprite;

    // Merkezde şeffaf, kenarlarda opak siyah radial gradient — panel arka planına
    // "kitap gibi" bir vinyet/derinlik katmanı olarak konur.
    public static Sprite VignetteSprite()
    {
        if (vignetteSprite != null) return vignetteSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.Alpha8, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float   maxDist = center.magnitude;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
            float a = Mathf.Clamp01(Mathf.SmoothStep(0f, 1f, (d - 0.35f) / 0.65f));
            tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
        }
        tex.Apply();

        vignetteSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return vignetteSprite;
    }

    // Dolu daire, alpha maskeli — sayfa göstergesi noktaları için.
    public static Sprite DotSprite()
    {
        if (dotSprite != null) return dotSprite;

        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.Alpha8, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float   radius = size * 0.42f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), center);
            float a = Mathf.Clamp01(1f - (d - radius) / 1.5f);   // ince kenar yumuşatma
            tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
        }
        tex.Apply();

        dotSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return dotSprite;
    }
}
}
