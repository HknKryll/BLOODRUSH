using UnityEngine;
using UnityEngine.UI;
using Bloodrush.Flow;
using Bloodrush.Arena;
using Bloodrush.Shared;

// Not: BEDEN (StimulantSystem collapse) ve YÜKLEME (upload) barları kaldırıldı —
// HUD artık sadece CH3'ün "VERİ x/y" terminal sayacını gösteriyor. Upload
// progress'in kendisi (statik API) hâlâ var; DataTerminal/UploadTerminal/GameFlow
// buna yazıyor, sadece görsel bar yok.
namespace Bloodrush.UI
{
public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    static float uploadProgress = 0f;

    Text  veriText;         // "2/4  —  %62"
    GameObject veriRow;     // WaveDirector olmayan sahnelerde gizlenir

    RectTransform healthFillRT;   // genişliği cana göre değişir
    Image         healthFill;     // renk cana göre (yeşil/sarı/kırmızı)
    Text          healthText;     // sayı
    Health        playerHealth;

    GameObject hudRoot;

    void Awake()
    {
        Instance = this;
        BuildHUD();
    }

    // Bölüm başına göster/gizle (GameFlow çağırır). Ch1/Ch2'de kapalı, Ch3+'te açık.
    public void SetVisible(bool visible)
    {
        if (hudRoot) hudRoot.SetActive(visible);
    }

    void BuildHUD()
    {
        var cgo    = new GameObject("GameHUD_Canvas");
        hudRoot    = cgo;
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        cgo.AddComponent<CanvasScaler>();

        // Sol alt köşe kapsayıcı (sağ alt WeaponHUD'a ait)
        var containerGO = new GameObject("HUDBars");
        var container    = containerGO.AddComponent<RectTransform>();
        container.SetParent(cgo.transform, false);
        container.anchorMin        = new Vector2(0f, 0f);
        container.anchorMax        = new Vector2(0f, 0f);
        container.pivot            = new Vector2(0f, 0f);
        container.anchoredPosition = new Vector2(16f, 16f);
        container.sizeDelta        = new Vector2(210f, 0f);

        var vlg = containerGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding                = new RectOffset(6, 6, 6, 6);
        vlg.spacing                = 6f;
        vlg.childAlignment         = TextAnchor.UpperLeft;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var fitter = containerGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Opak arka plan paneli — 3D sahneyle (yeşil çim vs.) karışmasın diye
        var panelGO = new GameObject("Panel");
        var panel   = panelGO.AddComponent<Image>();
        panelGO.transform.SetParent(container, false);
        panel.color = new Color(0.05f, 0.05f, 0.05f, 0.92f);
        var panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;
        panelGO.AddComponent<LayoutElement>().ignoreLayout = true; // layout hesabının dışında tut

        BuildVeriRow(container);     // üstte
        BuildHealthBar(container);   // altta
    }

    // Can barı satırı: "CAN" etiketi + dolgu bar (renk cana göre) + sayı
    void BuildHealthBar(RectTransform parent)
    {
        var row = new GameObject("CAN_Row");
        row.transform.SetParent(parent, false);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = 6f;
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;

        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.minHeight       = 22f;
        rowLE.preferredHeight = 22f;

        // Etiket
        var lblGO = new GameObject("Label");
        lblGO.transform.SetParent(row.transform, false);
        var lbl = lblGO.AddComponent<Text>();
        lbl.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lbl.text      = "CAN";
        lbl.fontSize  = 10;
        lbl.color     = new Color(0.8f, 0.8f, 0.8f, 0.9f);
        lbl.alignment = TextAnchor.MiddleLeft;
        lbl.raycastTarget = false;
        var lblLE = lblGO.AddComponent<LayoutElement>();
        lblLE.minWidth       = 58f;
        lblLE.preferredWidth = 58f;

        // Bar arka planı (esnek genişlik)
        var barGO = new GameObject("Bar");
        barGO.transform.SetParent(row.transform, false);
        barGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);
        var barLE = barGO.AddComponent<LayoutElement>();
        barLE.flexibleWidth   = 1f;
        barLE.minHeight       = 16f;
        barLE.preferredHeight = 16f;

        // Dolgu (anchor tabanlı — sprite gerektirmez; genişliği anchorMax.x ile ayarlanır)
        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(barGO.transform, false);
        healthFill = fillGO.AddComponent<Image>();
        healthFill.color = new Color(0.85f, 0.15f, 0.15f, 1f);   // kırmızı
        healthFill.raycastTarget = false;
        healthFillRT = fillGO.GetComponent<RectTransform>();
        healthFillRT.anchorMin = Vector2.zero;
        healthFillRT.anchorMax = Vector2.one;
        healthFillRT.offsetMin = Vector2.zero;
        healthFillRT.offsetMax = Vector2.zero;

        // Sayı (bar üzerinde ortalı)
        var txtGO = new GameObject("Value");
        txtGO.transform.SetParent(barGO.transform, false);
        healthText = txtGO.AddComponent<Text>();
        healthText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        healthText.fontSize  = 11;
        healthText.color     = Color.white;
        healthText.alignment = TextAnchor.MiddleCenter;
        healthText.raycastTarget = false;
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;
    }

    // Terminal sayacı satırı: "VERİ  2/4" — oyuncu terminal içindeyken "— %62" eklenir.
    // Bar yok, sadece metin; WaveDirector'lı sahnelerde (CH3) görünür.
    void BuildVeriRow(RectTransform parent)
    {
        veriRow = new GameObject("VERI_Row");
        veriRow.transform.SetParent(parent, false);

        var hlg = veriRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = 6f;
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;

        var rowLE = veriRow.AddComponent<LayoutElement>();
        rowLE.minHeight       = 20f;
        rowLE.preferredHeight = 20f;

        var lblGO = new GameObject("Label");
        lblGO.transform.SetParent(veriRow.transform, false);
        var lbl = lblGO.AddComponent<Text>();
        lbl.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lbl.text      = "VERİ";
        lbl.fontSize  = 10;
        lbl.color     = new Color(0.8f, 0.8f, 0.8f, 0.9f);
        lbl.alignment = TextAnchor.MiddleLeft;
        lbl.raycastTarget = false;
        var lblLE = lblGO.AddComponent<LayoutElement>();
        lblLE.minWidth       = 58f;
        lblLE.preferredWidth = 58f;

        var valGO = new GameObject("Value");
        valGO.transform.SetParent(veriRow.transform, false);
        veriText = valGO.AddComponent<Text>();
        veriText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        veriText.fontSize  = 12;
        veriText.color     = new Color(0.55f, 0.85f, 1f, 1f);   // terminal temasıyla uyumlu soğuk mavi
        veriText.alignment = TextAnchor.MiddleLeft;
        veriText.raycastTarget = false;
        valGO.AddComponent<LayoutElement>().flexibleWidth = 1f;
    }

    void Update()
    {
        UpdateHealthBar();
        UpdateVeriRow();
    }

    void UpdateHealthBar()
    {
        if (healthFillRT == null) return;

        // Oyuncu Health'ini bul (respawn'da yeniden bulur)
        if (playerHealth == null)
        {
            var pgo = GameObject.FindGameObjectWithTag("Player");
            if (pgo) playerHealth = pgo.GetComponent<Health>();
            if (playerHealth == null) return;
        }

        float frac = playerHealth.Max > 0f ? Mathf.Clamp01(playerHealth.Current / playerHealth.Max) : 0f;

        var am = healthFillRT.anchorMax;
        am.x = frac;
        healthFillRT.anchorMax = am;

        if (healthText) healthText.text = Mathf.CeilToInt(playerHealth.Current).ToString();
    }

    void UpdateVeriRow()
    {
        if (veriRow == null) return;

        var director = WaveDirector.Instance;
        // Sabit-dalga modunda (Arena) da bir WaveDirector instance'ı var artık —
        // "VERİ x/y" satırı SADECE heat/terminal modunda (Ch3 Server Core) görünmeli.
        bool show = director != null && !director.IsFixedWaveMode;
        if (veriRow.activeSelf != show) veriRow.SetActive(show);
        if (!show) return;

        string text = $"{director.TerminalsDone}/{director.TotalTerminals}";
        var active = DataTerminal.Active;
        if (active != null)
            text += $"  —  %{Mathf.RoundToInt(active.Progress * 100)}";
        veriText.text = text;
    }

    public static void AddUploadProgress(float amount)
    {
        uploadProgress = Mathf.Clamp01(uploadProgress + amount);
    }

    public static float UploadProgress
    {
        get => uploadProgress;
        set => uploadProgress = Mathf.Clamp01(value);
    }

    public static void ResetProgress() => uploadProgress = 0f;
}
}
