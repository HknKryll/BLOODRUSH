using UnityEngine;
using TMPro;

namespace Bloodrush.UI.Menu
{
// Menunun TEK gorsel kaynagi. Renk, font, olcek, ritim ve hareket suresi — hepsi burada.
// Kod icinde hicbir yerde ham renk/sayi yok; boylece butun menuyu tek asset'ten
// degistirebilirsin.
//
// Assets/Resources/MenuTheme.asset olarak olustur (Create > Bloodrush > Menu Temasi).
// Asset yoksa varsayilanlarla calisir, menu yine dogru gorunur.
[CreateAssetMenu(menuName = "Bloodrush/Menü Teması", fileName = "MenuTheme")]
public class MenuTheme : ScriptableObject
{
    [Header("Renkler — VERITAS")]
    public Color background    = new Color32(0xF5, 0xF1, 0xE8, 0xFF);  // krem
    public Color surface       = new Color32(0xFF, 0xFF, 0xFF, 0xFF);  // panel
    public Color textPrimary   = new Color32(0x2A, 0x2A, 0x28, 0xFF);  // krem üstünde 13.9:1
    public Color textSecondary = new Color32(0x6B, 0x68, 0x62, 0xFF);  // 4.9:1
    [Tooltip("Altın vurgu — ÇİZGİ/DOLGU için. Metin olarak kullanma (krem üstünde 2.9:1).")]
    public Color accent        = new Color32(0xB8, 0x87, 0x3B, 0xFF);
    [Tooltip("Altın METİN gerektiğinde — krem üstünde 4.6:1, okunabilir.")]
    public Color accentText    = new Color32(0x8A, 0x64, 0x28, 0xFF);
    public Color destructive   = new Color32(0x9B, 0x3A, 0x2F, 0xFF);

    [Header("Durumlar")]
    public Color hairline  = new Color(0.165f, 0.165f, 0.157f, 0.12f);
    public Color hoverFill = new Color(0.722f, 0.529f, 0.231f, 0.06f);
    public Color focusRing = new Color(0.722f, 0.529f, 0.231f, 0.90f);
    [Range(0.1f, 1f)] public float disabledAlpha = 0.38f;
    public float focusRingWidth = 2f;
    [Tooltip("Hover'da solda beliren altın çubuğun kalınlığı.")]
    public float accentBarWidth = 2f;

    [Header("Tipografi")]
    [Tooltip("Başlıklar için ince/geniş bir sans. Boşsa TMP varsayılanı kullanılır.")]
    public TMP_FontAsset displayFont;
    [Tooltip("Gövde metni. Boşsa TMP varsayılanı kullanılır.")]
    public TMP_FontAsset bodyFont;

    [Header("Punto ölçeği")]
    public float sizeCaption = 12f;   // üst etiket
    public float sizeBody    = 14f;
    public float sizeLabel   = 18f;   // buton
    public float sizeHeading = 28f;   // sekme/panel başlığı
    public float sizeTitle   = 56f;   // BLOODRUSH

    [Header("Harf aralığı")]
    public float trackingTitle   = 18f;
    public float trackingCaption = 8f;
    public float trackingLabel   = 4f;

    [Header("Aralık ritmi (8'in katları)")]
    public float unit = 8f;
    public float U1 => unit;          // 8
    public float U2 => unit * 2f;     // 16
    public float U3 => unit * 3f;     // 24
    public float U5 => unit * 5f;     // 40
    public float U8 => unit * 8f;     // 64

    [Header("Hareket — sadece fade + kısa kayma")]
    [Tooltip("Giriş süresi (sn). 150-300 ms arası doğal hissettirir.")]
    public float enterDuration = 0.18f;
    [Tooltip("Çıkış girişten kısa olmalı (~%65) ki arayüz çevik hissedilsin.")]
    public float exitDuration  = 0.12f;
    [Tooltip("Liste öğeleri arası gecikme (sn).")]
    public float stagger       = 0.04f;
    [Tooltip("Kayma mesafesi (px). Küçük tut — abartılı hareket istenmiyor.")]
    public float slideDistance = 14f;

    [Header("Ölçü")]
    public Vector2 buttonSize = new Vector2(320f, 48f);
    public Vector2 panelSize  = new Vector2(960f, 620f);

    static MenuTheme cached;

    // Resources'tan yükler; yoksa varsayılan bir örnek döner (menü yine çalışır).
    public static MenuTheme Load()
    {
        if (cached != null) return cached;
        cached = Resources.Load<MenuTheme>("MenuTheme");
        if (cached == null)
        {
            Debug.Log("[MenuTheme] Resources/MenuTheme bulunamadı — varsayılan tema kullanılıyor.");
            cached = CreateInstance<MenuTheme>();
        }
        return cached;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearCache() => cached = null;
}
}
