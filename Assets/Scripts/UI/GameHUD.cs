using UnityEngine;
using UnityEngine.UI;

public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    static float uploadProgress = 0f;

    Image uploadFill;
    Image collapseFill;
    Text  uploadPct;
    Text  collapsePct;

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

        collapseFill = BuildBar(container, "BEDEN",   new Color(0.9f,  0.22f, 0.15f), out collapsePct);
        uploadFill   = BuildBar(container, "YÜKLEME", new Color(0.25f, 0.88f, 0.35f), out uploadPct);
    }

    Image BuildBar(RectTransform parent, string label, Color fillColor, out Text pctText)
    {
        var rowGO = new GameObject(label + "_Row");
        rowGO.transform.SetParent(parent, false);

        var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = 6f;
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;

        var rowLE = rowGO.AddComponent<LayoutElement>();
        rowLE.minHeight       = 20f;
        rowLE.preferredHeight = 20f;

        // Etiket metni
        var lblGO = new GameObject("Label");
        lblGO.transform.SetParent(rowGO.transform, false);
        var lbl = lblGO.AddComponent<Text>();
        lbl.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lbl.text      = label;
        lbl.fontSize  = 10;
        lbl.color     = new Color(0.8f, 0.8f, 0.8f, 0.9f);
        lbl.alignment = TextAnchor.MiddleLeft;
        lbl.raycastTarget = false;
        var lblLE = lblGO.AddComponent<LayoutElement>();
        lblLE.minWidth       = 58f;
        lblLE.preferredWidth = 58f;

        // Bar arka planı
        var bgGO = new GameObject("BarBG");
        bgGO.transform.SetParent(rowGO.transform, false);
        var bg = bgGO.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        var bgLE = bgGO.AddComponent<LayoutElement>();
        bgLE.flexibleWidth = 1f;

        // Bar dolumu
        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(bgGO.transform, false);
        var fill = fillGO.AddComponent<Image>();
        fill.color      = fillColor;
        fill.type       = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 1f;
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = new Vector2(1f, 1f);
        fillRT.offsetMax = new Vector2(-1f, -1f);

        // Yüzde metni
        var pctGO = new GameObject("Pct");
        pctGO.transform.SetParent(bgGO.transform, false);
        var pct = pctGO.AddComponent<Text>();
        pct.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        pct.fontSize  = 9;
        pct.color     = new Color(1f, 1f, 1f, 0.7f);
        pct.alignment = TextAnchor.MiddleRight;
        pct.raycastTarget = false;
        var pctRT = pctGO.GetComponent<RectTransform>();
        pctRT.anchorMin = Vector2.zero;
        pctRT.anchorMax = Vector2.one;
        pctRT.offsetMin = Vector2.zero;
        pctRT.offsetMax = new Vector2(-3f, 0f);

        pctText = pct;
        return fill;
    }

    void Update()
    {
        if (uploadFill != null)
            uploadFill.fillAmount = uploadProgress;

        float collapse = StimulantSystem.Instance != null ? StimulantSystem.Instance.CollapseLevel : 1f;
        if (collapseFill != null)
            collapseFill.fillAmount = collapse;

        if (uploadPct   != null) uploadPct.text   = $"%{Mathf.RoundToInt(uploadProgress * 100)}";
        if (collapsePct != null) collapsePct.text = $"%{Mathf.RoundToInt(collapse * 100)}";
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
