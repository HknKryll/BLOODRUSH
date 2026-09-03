using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Bloodrush.UI
{
// Kitap/not okuma + seçim menüsü paneli. Kendi canvas'ını kurar, statik metodlarla
// sürülür — BossHealthUI/Notification/DialogueUI ile aynı desen.
//
// Görsel dil UITheme'den okunur (Resources/UITheme.asset) — asset yoksa/silinmişse HER
// renk/font alanı kendi sabit varsayılanına düşer, hiçbir şey kırılmaz.
//
// TEK GÜNCELLEME NOKTASI: ShowBook()/ShowMenu() dışında hiçbir yerden metin yazılmaz.
public class BookUI : MonoBehaviour
{
    const int MaxMenuLines = 9;    // sayı tuşu 1-9 ile eşleşir
    const int MaxPageDots  = 12;   // bundan uzun kitaplarda nokta yerine sadece sayaç anlamlı kalır

    static BookUI instance;
    UITheme theme;

    // ── Sabit varsayılanlar — theme yoksa/alan boşsa bunlara düşülür ──
    Color Background    => theme != null ? theme.background    : new Color(0.078f, 0.070f, 0.063f);
    Color TextPrimary   => theme != null ? theme.textPrimary   : new Color(0.937f, 0.906f, 0.847f);
    Color TextSecondary => theme != null ? theme.textSecondary : new Color(0.659f, 0.620f, 0.549f);
    Color Accent        => theme != null ? theme.accent        : new Color(0.788f, 0.635f, 0.290f);
    float HairlineAlpha => theme != null ? theme.hairlineAlpha : 0.10f;
    float VignetteStr   => theme != null ? theme.vignetteStrength : 0.55f;
    TMP_FontAsset SerifFont => theme != null ? theme.serifFont : null;
    TMP_FontAsset SansFont  => theme != null ? theme.sansFont  : null;
    TMP_FontAsset MonoFont  => theme != null ? theme.monoFont  : null;
    string ArchiveCode  => theme != null ? theme.archiveCode   : "KONSEY ARŞİVİ / SEC.1";

    // ── Okuma görünümü ──
    GameObject      panel;
    Image           titleIcon;
    TextMeshProUGUI titleLabel, subtitleLabel, bodyLabel;
    Transform       readFooter;   // her ShowBook'ta temizlenip yeniden dolduruluyor

    // ── Menü görünümü ──
    GameObject        menuPanel;
    TextMeshProUGUI   menuTitle;
    Image[]           rowBar, rowTint, rowIcon;
    TextMeshProUGUI[] rowNumber, rowTitle;
    Transform         menuFooter;

    public static bool IsOpen     => instance != null && instance.panel.activeSelf;
    public static bool IsMenuOpen => instance != null && instance.menuPanel.activeSelf;

    static BookUI Ensure()
    {
        if (instance == null)
            instance = new GameObject("BookUI").AddComponent<BookUI>();
        return instance;
    }

    // Panelin TEK yazma noktası. key/hintDesc ayrı verilir: "[E]" kutusu + "Kapat" metni
    // ayrı render edilsin diye (eskiden tek düz string'di). hintIcon opsiyonel — null
    // gelirse ipucu ikonsuz, sadece tuş kutusu+metin olarak kalır.
    public static void ShowBook(string title, string subtitle, string pageText,
                                 int pageIndex, int pageCount, string key, string hintDesc,
                                 Sprite hintIcon = null)
    {
        var i = Ensure();
        i.titleLabel.text = title;
        i.titleIcon.sprite  = UIIcons.Book;
        i.titleIcon.enabled = UIIcons.Book != null;
        i.bodyLabel.text  = pageText;

        bool hasSub = !string.IsNullOrEmpty(subtitle);
        i.subtitleLabel.text    = subtitle;
        i.subtitleLabel.enabled = hasSub;

        i.ClearChildren(i.readFooter);
        i.BuildKeyHints(i.readFooter, new Vector2(0.02f, 0.15f), new Vector2(0.55f, 0.85f),
                        new[] { (key, hintDesc, hintIcon) });
        if (pageCount > 1)
            i.BuildPageIndicator(i.readFooter, new Vector2(0.55f, 0f), new Vector2(0.98f, 1f),
                                  pageIndex, pageCount);

        i.panel.SetActive(true);
    }

    public static void HideBook()
    {
        if (instance != null) instance.panel.SetActive(false);
    }

    // Seçim menüsü. hints: alt bilgi çubuğunda sırayla gösterilecek (tuş, açıklama, ikon) üçlüleri.
    public static void ShowMenu(string[] titles, int selectedIndex, string header,
                                 (string key, string desc, Sprite icon)[] hints)
    {
        var i = Ensure();
        i.panel.SetActive(false);
        i.menuTitle.text = header;

        for (int n = 0; n < i.rowTitle.Length; n++)
        {
            bool has = titles != null && n < titles.Length;
            bool sel = has && n == selectedIndex;

            i.rowNumber[n].enabled = has;
            i.rowTitle[n].enabled  = has;
            i.rowIcon[n].enabled   = has && UIIcons.Book != null;
            if (has)
            {
                i.rowNumber[n].text  = (n + 1).ToString("00");
                i.rowTitle[n].text   = titles[n];
                i.rowTitle[n].color  = sel ? i.TextPrimary : i.TextSecondary;
                i.rowIcon[n].sprite  = UIIcons.Book;
            }
            i.rowBar[n].enabled  = sel;
            i.rowTint[n].enabled = sel;
        }

        i.ClearChildren(i.menuFooter);
        i.BuildKeyHints(i.menuFooter, new Vector2(0.02f, 0.15f), new Vector2(0.98f, 0.85f), hints);

        i.menuPanel.SetActive(true);
    }

    public static void HideMenu()
    {
        if (instance != null) instance.menuPanel.SetActive(false);
    }

    public static void HideAll()
    {
        HideBook();
        HideMenu();
    }

    void Awake()
    {
        instance = this;
        theme    = UITheme.Load();
        Build();
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 65;   // DialogueUI'nin (60) üstünde
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        BuildReadingPanel();
        BuildMenuPanel();
    }

    // ───────────────────────── Okuma paneli ─────────────────────────
    void BuildReadingPanel()
    {
        panel = new GameObject("Panel");
        panel.transform.SetParent(transform, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.275f, 0.22f);
        rt.anchorMax = new Vector2(0.725f, 0.78f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        panel.AddComponent<Image>().color = Background;

        AddVignette(panel.transform);

        titleIcon = MakeIcon(panel.transform, new Vector2(0.06f, 0.878f), new Vector2(0.115f, 0.955f));

        titleLabel = MakeTmp("Baslik", panel.transform, 32, TextPrimary, SerifFont,
            TextAlignmentOptions.Left, new Vector2(0.135f, 0.875f), new Vector2(0.94f, 0.96f));
        titleLabel.fontStyle = FontStyles.Bold;

        subtitleLabel = MakeTmp("AltBaslik", panel.transform, 15, Accent, MonoFont,
            TextAlignmentOptions.Left, new Vector2(0.06f, 0.815f), new Vector2(0.94f, 0.865f));
        subtitleLabel.characterSpacing = 3f;
        subtitleLabel.enabled = false;

        AddHairline(panel.transform, new Vector2(0.06f, 0.803f), new Vector2(0.94f, 0.807f));

        bodyLabel = MakeTmp("Govde", panel.transform, 22, TextSecondary, SansFont,
            TextAlignmentOptions.TopLeft, new Vector2(0.08f, 0.11f), new Vector2(0.92f, 0.79f));
        bodyLabel.enableAutoSizing = true;    // uzun sayfalar otomatik küçülür, taşma yok
        bodyLabel.fontSizeMin      = 13f;
        bodyLabel.fontSizeMax      = 22f;
        bodyLabel.lineSpacing      = 28f;     // mockup'taki ~1.85 satır aralığına yakın
        bodyLabel.paragraphSpacing = 10f;

        var footerGo = new GameObject("Footer");
        footerGo.transform.SetParent(panel.transform, false);
        var frt = footerGo.AddComponent<RectTransform>();
        frt.anchorMin = new Vector2(0.06f, 0.02f);
        frt.anchorMax = new Vector2(0.94f, 0.09f);
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        readFooter = footerGo.transform;

        panel.SetActive(false);
    }

    // ───────────────────────── Menü paneli ─────────────────────────
    void BuildMenuPanel()
    {
        menuPanel = new GameObject("MenuPanel");
        menuPanel.transform.SetParent(transform, false);
        var rt = menuPanel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.33f, 0.22f);
        rt.anchorMax = new Vector2(0.67f, 0.78f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        menuPanel.AddComponent<Image>().color = Background;

        AddVignette(menuPanel.transform);

        menuTitle = MakeTmp("MenuBaslik", menuPanel.transform, 28, TextPrimary, SerifFont,
            TextAlignmentOptions.Left, new Vector2(0.07f, 0.895f), new Vector2(0.62f, 0.96f));
        menuTitle.fontStyle = FontStyles.Bold;

        // Sağ üst: küçük amber nokta + mono "belge kodu"
        var dot = new GameObject("BelgeNokta");
        dot.transform.SetParent(menuPanel.transform, false);
        var dotRt = dot.AddComponent<RectTransform>();
        dotRt.anchorMin = new Vector2(0.635f, 0.925f);
        dotRt.anchorMax = new Vector2(0.655f, 0.945f);
        dotRt.offsetMin = dotRt.offsetMax = Vector2.zero;
        var dotImg = dot.AddComponent<Image>();
        dotImg.sprite = UITheme.DotSprite();
        dotImg.color  = Accent;

        var code = MakeTmp("BelgeKodu", menuPanel.transform, 12, TextSecondary, MonoFont,
            TextAlignmentOptions.Left, new Vector2(0.665f, 0.905f), new Vector2(0.94f, 0.95f));
        code.characterSpacing = 2f;
        code.text = ArchiveCode;

        AddHairline(menuPanel.transform, new Vector2(0.07f, 0.855f), new Vector2(0.93f, 0.859f));

        rowBar    = new Image[MaxMenuLines];
        rowTint   = new Image[MaxMenuLines];
        rowIcon   = new Image[MaxMenuLines];
        rowNumber = new TextMeshProUGUI[MaxMenuLines];
        rowTitle  = new TextMeshProUGUI[MaxMenuLines];

        float top = 0.83f, bottom = 0.14f;
        float slot = (top - bottom) / MaxMenuLines;
        for (int n = 0; n < MaxMenuLines; n++)
        {
            float yMax = top - slot * n;
            float yMin = yMax - slot * 0.82f;   // satırlar arası küçük boşluk

            var tint = new GameObject($"Vurgu_{n}");
            tint.transform.SetParent(menuPanel.transform, false);
            var trt = tint.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.045f, yMin);
            trt.anchorMax = new Vector2(0.955f, yMax);
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            rowTint[n] = tint.AddComponent<Image>();
            rowTint[n].color = new Color(Accent.r, Accent.g, Accent.b, 0.10f);
            rowTint[n].enabled = false;

            var bar = new GameObject($"Cubuk_{n}");
            bar.transform.SetParent(menuPanel.transform, false);
            var brt = bar.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.045f, yMin);
            brt.anchorMax = new Vector2(0.06f, yMax);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            rowBar[n] = bar.AddComponent<Image>();
            rowBar[n].color = Accent;
            rowBar[n].enabled = false;

            rowNumber[n] = MakeTmp($"No_{n}", menuPanel.transform, 15, TextSecondary, MonoFont,
                TextAlignmentOptions.Left, new Vector2(0.09f, yMin), new Vector2(0.19f, yMax));

            rowIcon[n] = MakeIcon(menuPanel.transform, new Vector2(0.20f, yMin + slot * 0.06f), new Vector2(0.20f + slot * 0.6f, yMax - slot * 0.06f));

            rowTitle[n] = MakeTmp($"Satir_{n}", menuPanel.transform, 21, TextSecondary, SansFont,
                TextAlignmentOptions.Left, new Vector2(0.27f, yMin), new Vector2(0.93f, yMax));
        }

        var footerGo = new GameObject("Footer");
        footerGo.transform.SetParent(menuPanel.transform, false);
        var frt = footerGo.AddComponent<RectTransform>();
        frt.anchorMin = new Vector2(0.07f, 0.02f);
        frt.anchorMax = new Vector2(0.93f, 0.115f);
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        menuFooter = footerGo.transform;

        menuPanel.SetActive(false);
    }

    // ───────────────────────── Ortak yardımcılar ─────────────────────────

    void AddVignette(Transform parent)
    {
        var go = new GameObject("Vinyet");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.sprite = UITheme.VignetteSprite();
        img.type   = Image.Type.Simple;
        img.color  = new Color(0f, 0f, 0f, VignetteStr);
        img.raycastTarget = false;
    }

    void AddHairline(Transform parent, Vector2 aMin, Vector2 aMax)
    {
        var go = new GameObject("Ayirac");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = new Color(TextPrimary.r, TextPrimary.g, TextPrimary.b, HairlineAlpha);
        img.raycastTarget = false;
    }

    // Küçük borderlı "[TUŞ]" kutusu + (varsa) ikon + açıklama — birden fazlası yan yana
    // dizilir. Sabit karakter-sayısı tahminine göre genişlik hesaplanır (kısa, bilinen
    // metinler için yeterli — dış ölçüm gerektirmez). icon null ise o slot ikonsuz kalır.
    void BuildKeyHints(Transform container, Vector2 areaMin, Vector2 areaMax, (string key, string desc, Sprite icon)[] hints)
    {
        if (hints == null || hints.Length == 0) return;

        float slotW = (areaMax.x - areaMin.x) / hints.Length;

        for (int h = 0; h < hints.Length; h++)
        {
            float slotStart = areaMin.x + slotW * h;
            float slotEnd   = slotStart + slotW;

            float boxW = Mathf.Min(0.045f, slotW * 0.32f);
            var box = new GameObject("TusKutusu");
            box.transform.SetParent(container, false);
            var brt = box.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(slotStart, areaMin.y);
            brt.anchorMax = new Vector2(slotStart + boxW, areaMax.y);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            var border = box.AddComponent<Image>();
            border.color = new Color(TextSecondary.r, TextSecondary.g, TextSecondary.b, 0.5f);

            var inner = new GameObject("Ic");
            inner.transform.SetParent(box.transform, false);
            var irt = inner.AddComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.08f, 0.1f);
            irt.anchorMax = new Vector2(0.92f, 0.9f);
            irt.offsetMin = irt.offsetMax = Vector2.zero;
            inner.AddComponent<Image>().color = Background;

            var keyLabel = MakeTmp("Tus", inner.transform, 14, Accent, MonoFont,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            keyLabel.text = hints[h].key;

            // İkon (varsa) tuş kutusunun hemen sağında, açıklama metni ondan sonra başlar.
            float descStart = slotStart + boxW + 0.008f;
            if (hints[h].icon != null)
            {
                float iconW = Mathf.Min(0.03f, slotW * 0.18f);
                var iconImg = MakeIcon(container, new Vector2(descStart, areaMin.y + 0.05f),
                    new Vector2(descStart + iconW, areaMax.y - 0.05f));
                iconImg.sprite  = hints[h].icon;
                iconImg.enabled = true;
                descStart += iconW + 0.006f;
            }

            var descLabel = MakeTmp("Aciklama", container, 15, TextSecondary, SansFont,
                TextAlignmentOptions.Left,
                new Vector2(descStart, areaMin.y), new Vector2(slotEnd, areaMax.y));
            descLabel.text = hints[h].desc;
        }
    }

    // Sayfa noktaları (aktif=accent dolu, pasif=textSecondary soluk) + mono "01 / 03" sayaç.
    void BuildPageIndicator(Transform container, Vector2 areaMin, Vector2 areaMax, int pageIndex, int pageCount)
    {
        int dots = Mathf.Min(pageCount, MaxPageDots);
        float dotSize = 0.018f;
        float gap     = 0.010f;
        float totalDotsW = dots * dotSize + (dots - 1) * gap;

        float counterW = 0.10f;
        float startX = areaMax.x - counterW - 0.02f - totalDotsW;

        for (int d = 0; d < dots; d++)
        {
            float x = startX + d * (dotSize + gap);
            var go = new GameObject($"Nokta_{d}");
            go.transform.SetParent(container, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(x, 0.35f);
            rt.anchorMax = new Vector2(x + dotSize, 0.65f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = UITheme.DotSprite();
            bool active = d == Mathf.Clamp(pageIndex, 0, dots - 1);
            img.color = active ? Accent : new Color(TextSecondary.r, TextSecondary.g, TextSecondary.b, 0.4f);
        }

        var counter = MakeTmp("Sayac", container, 15, TextSecondary, MonoFont,
            TextAlignmentOptions.Right, new Vector2(areaMax.x - counterW, areaMin.y), new Vector2(areaMax.x, areaMax.y));
        counter.characterSpacing = 2f;
        counter.text = $"{(pageIndex + 1):00} / {pageCount:00}";
    }

    // Küçük ikon kutusu — sprite null gelirse (kullanıcı PNG'leri henüz Sprite'a
    // çevirmediyse) çağıran taraf enabled=false bırakır, hiçbir hata çıkmaz.
    Image MakeIcon(Transform parent, Vector2 aMin, Vector2 aMax)
    {
        var go = new GameObject("Ikon");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.raycastTarget  = false;
        img.preserveAspect = true;
        img.enabled = false;
        var rt = img.rectTransform;
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return img;
    }

    void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }

    TextMeshProUGUI MakeTmp(string name, Transform parent, float size, Color color, TMP_FontAsset font,
                            TextAlignmentOptions align, Vector2 aMin, Vector2 aMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;   // null bırakılırsa TMP kendi varsayılanını kullanır
        t.fontSize      = size;
        t.color         = color;
        t.alignment     = align;
        t.raycastTarget = false;
        t.margin        = new Vector4(4f, 2f, 4f, 2f);
        var rt = t.rectTransform;
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return t;
    }
}
}
