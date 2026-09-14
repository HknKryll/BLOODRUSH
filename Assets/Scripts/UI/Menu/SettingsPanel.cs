using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Bloodrush.Settings;

namespace Bloodrush.UI.Menu
{
// TEK ayarlar paneli — hem ana menu hem pause bunu acar. Ikinci bir kopya YOK.
// (Eski sistemde MainMenuManager ve PauseMenu ayri ayri kendi panellerini kuruyordu;
// bir tarafta degistirilen ayar digerinde gorunmuyordu.)
//
// Duzen: solda dikey sekme listesi, sagda o sekmenin icerigi. Her sekmenin altinda
// "VARSAYILANA DÖNDÜR". Her degisiklik ANINDA uygulanir ve diske yazilir — uygula
// butonu yok.
public partial class SettingsPanel : MonoBehaviour
{
    const int SortingOrder = 300;

    static SettingsPanel active;
    public static bool IsOpen => active != null;

    MenuTheme   theme;
    Canvas      canvas;
    CanvasGroup group;
    RectTransform panel;
    RectTransform content;
    ScrollRect    scrollRect;

    Action onClosed;
    GameObject previousSelection;

    SettingsSection currentTab = SettingsSection.Display;
    readonly List<MenuButton> tabButtons = new List<MenuButton>();
    readonly List<Selectable> contentSelectables = new List<Selectable>();

    static readonly (SettingsSection tab, string label)[] Tabs =
    {
        (SettingsSection.Display,  "GÖRÜNTÜ"),
        (SettingsSection.Audio,    "SES"),
        (SettingsSection.Controls, "KONTROLLER"),
        (SettingsSection.Gameplay, "OYUN"),
    };

    public static void Open(Action onClosed = null)
    {
        if (active != null) return;
        var go  = new GameObject("SettingsPanel");
        var sp  = go.AddComponent<SettingsPanel>();
        sp.onClosed = onClosed;
        sp.Build();
        active = sp;
    }

    void Build()
    {
        theme = MenuTheme.Load();
        MenuNavigation.Ensure();
        UISounds.Ensure();

        previousSelection = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject : null;

        canvas = MenuUI.CreateCanvas("SettingsCanvas", SortingOrder);
        canvas.transform.SetParent(transform, false);

        MenuUI.Fill(canvas.transform, "Scrim", new Color(0.165f, 0.165f, 0.157f, 0.35f))
              .raycastTarget = true;

        panel = MenuUI.Box(canvas.transform, "Panel", new Vector2(0.5f, 0.5f),
                           Vector2.zero, theme.panelSize);
        panel.gameObject.AddComponent<Image>().color = theme.background;
        group = panel.gameObject.AddComponent<CanvasGroup>();

        // Ust altin cizgi + baslik
        var rule = MenuUI.Box(panel, "TopRule", new Vector2(0.5f, 1f), new Vector2(0f, -1f),
                              new Vector2(theme.panelSize.x, 2f));
        rule.gameObject.AddComponent<Image>().color = theme.accent;

        var capRT = MenuUI.Box(panel, "Caption", new Vector2(0f, 1f),
                               new Vector2(theme.U5 + 90f, -theme.U3), new Vector2(220f, 18f));
        MenuUI.Text(capRT, "VERITAS INITIATIVE", theme.sizeCaption, theme.textSecondary,
                    theme.trackingCaption, TextAlignmentOptions.Left, theme.bodyFont);

        var titleRT = MenuUI.Box(panel, "Title", new Vector2(0f, 1f),
                                 new Vector2(theme.U5 + 130f, -theme.U5 - 12f), new Vector2(320f, 40f));
        MenuUI.Text(titleRT, "AYARLAR", theme.sizeHeading, theme.textPrimary,
                    theme.trackingCaption, TextAlignmentOptions.Left, theme.displayFont);

        BuildTabs();

        // ICERIK ALANI — kenar payiyla panelin ICINE oturur (anchor+pivot aritmetigi
        // panelin disina tasiyordu). Ayrica KIRPILIR ve KAYDIRILIR: Kontroller sekmesinde
        // 11 tus satiri var, hicbir panele sigmaz.
        var scrollArea = MenuUI.Inset(panel, "ScrollArea",
                                      left: 290f, right: 40f, top: 115f, bottom: 95f);
        scrollRect = scrollArea.gameObject.AddComponent<ScrollRect>();

        var viewport = MenuUI.Stretch(scrollArea, "Viewport");
        viewport.gameObject.AddComponent<RectMask2D>();   // panel disina TASMAZ

        // KAYDIRMANIN CALISMASI ICIN SART: fare tekerlegi olayinin ScrollRect'e ulasmasi,
        // imlecin ScrollRect'in ALTINDAKI bir raycast hedefine denk gelmesine bagli.
        // Viewport'ta hic Graphic yoktu; panel arka plani ise ScrollRect'in EBEVEYNI
        // oldugu icin olay yukari kabarip gidiyordu. Sonuc: kaydirma hic calismiyor ve
        // Kontroller sekmesindeki 11 tus satiri kirpilmis halde erisilemez kaliyordu.
        var catcher = viewport.gameObject.AddComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f);   // gorunmez
        catcher.raycastTarget = true;

        content = new GameObject("Content").AddComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot     = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(0f, 0f);
        content.offsetMax = new Vector2(0f, 0f);

        scrollRect.viewport         = viewport;
        scrollRect.content          = content;
        scrollRect.horizontal       = false;
        scrollRect.vertical         = true;
        scrollRect.movementType     = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 34f;
        scrollRect.inertia          = false;   // kurumsal his: ataletsiz, net

        // Dikey ayrac — sekme listesiyle icerigi ayirir
        MenuUI.Box(panel, "VRule", new Vector2(0f, 0.5f), new Vector2(262f, -18f),
                   new Vector2(1f, theme.panelSize.y - 210f))
              .gameObject.AddComponent<Image>().color = theme.hairline;

        // Alt satir: kapat + varsayilana dondur
        MenuUI.Hairline(panel, theme, new Vector2(0.5f, 0f), new Vector2(0f, 68f),
                        theme.panelSize.x - theme.U8);

        MenuUI.Button(panel, theme, "◄  GERİ", new Vector2(0f, 0f),
                      new Vector2(theme.U5 + 100f, theme.U5), new Vector2(200f, 40f), Close);

        MenuUI.Button(panel, theme, "VARSAYILANA DÖNDÜR", new Vector2(1f, 0f),
                      new Vector2(-theme.U5 - 130f, theme.U5), new Vector2(260f, 40f),
                      ResetCurrentTab, TextAlignmentOptions.Right);

        ShowTab(SettingsSection.Display);

        StartCoroutine(MenuUI.FadeSlideIn(group, panel, theme.slideDistance, theme.enterDuration));
    }

    void BuildTabs()
    {
        tabButtons.Clear();
        float y = -120f;

        for (int i = 0; i < Tabs.Length; i++)
        {
            var tab = Tabs[i].tab;
            var btn = MenuUI.Button(panel, theme, Tabs[i].label, new Vector2(0f, 1f),
                                    new Vector2(theme.U5 + 100f, y), new Vector2(200f, 44f),
                                    () => ShowTab(tab));
            btn.OnHighlighted += UISounds.Hover;
            tabButtons.Add(btn);
            y -= 52f;
        }

        var sels = new Selectable[tabButtons.Count];
        for (int i = 0; i < tabButtons.Count; i++) sels[i] = tabButtons[i].GetComponent<Selectable>();
        MenuNavigation.LinkVertical(sels);
    }

    void ShowTab(SettingsSection tab)
    {
        currentTab = tab;
        UISounds.Click();

        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        contentSelectables.Clear();

        switch (tab)
        {
            case SettingsSection.Display:  BuildDisplayTab();  break;
            case SettingsSection.Audio:    BuildAudioTab();    break;
            case SettingsSection.Controls: BuildControlsTab(); break;
            case SettingsSection.Gameplay: BuildGameplayTab(); break;
        }

        MenuNavigation.LinkVertical(contentSelectables.ToArray());

        // Sekme listesi <-> icerik yatay gecisi
        if (contentSelectables.Count > 0)
            for (int i = 0; i < tabButtons.Count; i++)
                MenuNavigation.LinkHorizontal(tabButtons[i].GetComponent<Selectable>(),
                                              contentSelectables[0]);

        // Icerik yuksekligini kurulan satirlara gore ayarla — ScrollRect bunu okuyup
        // kaydirilacak mesafeyi belirliyor. Sekme degisince en basa don.
        content.sizeDelta = new Vector2(0f, Mathf.Max(0f, -rowY) + 12f);
        content.anchoredPosition = Vector2.zero;
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;

        // Aktif sekmeyi vurgula
        for (int i = 0; i < tabButtons.Count; i++)
            tabButtons[i].SetActiveTab(Tabs[i].tab == tab);

        var cg = content.GetComponent<CanvasGroup>();
        if (cg == null) cg = content.gameObject.AddComponent<CanvasGroup>();
        StartCoroutine(MenuUI.FadeSlideIn(cg, null, 0f, theme.enterDuration));
    }

    void ResetCurrentTab()
    {
        SettingsStore.ResetSection(currentTab);

        // Tuş atamaları KeyBindings'in kendi önbelleğinde; JSON'u sıfırlamak yetmiyordu,
        // önbellek eski değerleri tutup geri yazıyordu. ResetDefaults ikisini de temizler.
        if (currentTab == SettingsSection.Controls)
        {
            Player.KeyBindings.ResetDefaults();
            rebindIndex  = -1;
            conflictNote = null;
        }

        ShowTab(currentTab);   // arayuzu yeni degerlerle yeniden kur
    }

    // ───────────────── Satir yardimcilari (sekmeler kullanir) ─────────────────

    float rowY;
    // Genislik LAYOUT'tan degil hesaptan geliyor: rect.width ilk karede henuz 0 olabilir
    // ve satirlar sifir genislikte kurulurdu.
    float ContentWidth => theme.panelSize.x - 290f - 40f;

    void ResetRows() => rowY = 0f;

    RectTransform NextRow(string label)
    {
        var c = MenuUI.Row(content, theme, label, rowY, ContentWidth, out _);
        rowY -= MenuUI.RowHeight + 10f;
        return c;
    }

    void SectionHeader(string text)
    {
        var rt = MenuUI.Box(content, "Head", new Vector2(0f, 1f),
                            new Vector2(0f, rowY - 4f),
                            new Vector2(ContentWidth, 18f), new Vector2(0f, 1f));
        MenuUI.Text(rt, text, theme.sizeCaption, theme.accentText, theme.trackingCaption,
                    TextAlignmentOptions.Left, theme.bodyFont);
        rowY -= 30f;
    }

    Slider AddSlider(string label, float min, float max, float value, bool whole,
                     Func<float, string> format, Action<float> onChange)
    {
        var c = NextRow(label);
        var s = MenuUI.SliderControl(c, theme, min, max, value, whole, format, v =>
        {
            onChange(v);
            SettingsStore.NotifyChanged();
        });
        contentSelectables.Add(s);
        return s;
    }

    // Adimlayici: "< Deger >" — aslinda wholeNumbers Slider, gamepad/ok tuslari bedava.
    Slider AddStepper(string label, string[] options, int index, Action<int> onChange)
    {
        if (options == null || options.Length == 0) options = new[] { "—" };
        index = Mathf.Clamp(index, 0, options.Length - 1);

        var c = NextRow(label);
        var s = MenuUI.SliderControl(c, theme, 0, options.Length - 1, index, true,
            v => "‹  " + options[Mathf.Clamp((int)v, 0, options.Length - 1)] + "  ›",
            v =>
            {
                onChange(Mathf.Clamp((int)v, 0, options.Length - 1));
                SettingsStore.NotifyChanged();
            },
            showTrack: false);
        contentSelectables.Add(s);
        return s;
    }

    Slider AddToggle(string label, bool value, Action<bool> onChange)
        => AddStepper(label, new[] { "KAPALI", "AÇIK" }, value ? 1 : 0, i => onChange(i == 1));

    // Henuz bagli olmayan ayarlari isaretle — yalanci davranis olmasin.
    void MarkPending(string label) => SectionHeader(label + "   ·   HENÜZ ETKİN DEĞİL");

    // ───────────────── Kapanis ─────────────────

    void Update()
    {
        // Tuş atama bekleniyorsa Esc paneli KAPATMAZ — atamayı iptal eder (CaptureRebind).
        CaptureRebind();
        EnsureSelectionVisible();
        if (IsRebinding) return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        var gp = UnityEngine.InputSystem.Gamepad.current;
        if ((kb != null && kb.escapeKey.wasPressedThisFrame) ||
            (gp != null && gp.buttonEast.wasPressedThisFrame))
            Close();
    }

    // Klavye/gamepad ile asagi inerken secili satiri gorunur alana getirir.
    // ScrollRect bunu kendiliginden yapmaz — fare tekerlegi disinda kaydirma bilmiyor.
    static readonly Vector3[] cornerBuf = new Vector3[4];

    void EnsureSelectionVisible()
    {
        if (scrollRect == null || scrollRect.viewport == null || content == null) return;

        var es = EventSystem.current;
        var sel = es != null ? es.currentSelectedGameObject : null;
        if (sel == null) return;

        var selRT = sel.GetComponent<RectTransform>();
        if (selRT == null || !selRT.IsChildOf(content)) return;
        if (content.rect.height <= scrollRect.viewport.rect.height) return;

        selRT.GetWorldCorners(cornerBuf);
        float itemTop = cornerBuf[1].y, itemBottom = cornerBuf[0].y;

        scrollRect.viewport.GetWorldCorners(cornerBuf);
        float viewTop = cornerBuf[1].y, viewBottom = cornerBuf[0].y;

        float delta = 0f;
        if (itemTop > viewTop)         delta = itemTop - viewTop;
        else if (itemBottom < viewBottom) delta = itemBottom - viewBottom;
        if (Mathf.Abs(delta) < 0.5f) return;

        float scale = canvas != null ? canvas.scaleFactor : 1f;
        var pos = content.anchoredPosition;
        pos.y = Mathf.Clamp(pos.y - delta / Mathf.Max(0.0001f, scale),
                            0f, Mathf.Max(0f, content.rect.height - scrollRect.viewport.rect.height));
        content.anchoredPosition = pos;
    }

    public void Close()
    {
        if (active == this) active = null;
        UISounds.Back();
        SettingsStore.Save();
        StartCoroutine(CloseRoutine());
    }

    IEnumerator CloseRoutine()
    {
        yield return MenuUI.FadeOut(group, theme.exitDuration);
        var cb = onClosed;
        if (previousSelection != null) MenuNavigation.Focus(previousSelection);
        Destroy(gameObject);
        cb?.Invoke();
    }
}
}
