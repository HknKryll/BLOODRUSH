using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using Bloodrush.Arena;
using Bloodrush.Player;
using Bloodrush.Flow;

namespace Bloodrush.UI
{
public class PauseMenu : MonoBehaviour
{
    // ─── Palet (MainMenuManager ile aynı) ────────────────────────────
    static readonly Color BgColor     = new Color(0.051f, 0.051f, 0.051f, 1f);
    static readonly Color NeonRed     = new Color(1f, 0.102f, 0.102f, 1f);
    static readonly Color NeonRedGlow = new Color(1f, 0.102f, 0.102f, 0.45f);
    static readonly Color NeonRedFill = new Color(1f, 0.102f, 0.102f, 0.28f);
    static readonly Color BtnBg       = new Color(0.04f, 0.04f, 0.04f, 0.93f);
    static readonly Color ScanLine    = new Color(0f, 0f, 0f, 0.13f);

    GameObject      pauseRoot;
    GameObject      pausePanel;
    GameObject      settingsPanel;
    Slider          volumeSlider;
    TextMeshProUGUI volumeLabel;
    Slider          sensSlider;
    TextMeshProUGUI sensLabel;

    bool isPaused;
    bool settingsOpen;

    GameObject         controlsPanel;
    bool               controlsOpen;
    int                listeningIndex = -1;   // rebind için tuş bekliyor
    TextMeshProUGUI[]  keyButtons;

    PlayerMovement movement;

    // ─── Başlangıç ───────────────────────────────────────────────────

    void Start()
    {
        movement = FindFirstObjectByType<PlayerMovement>();
        AudioListener.volume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        EnsureEventSystem();
        BuildUI();
    }

    // Kampanya sahnelerinde (CH1-CH3) sahneye EventSystem konmamış — onsuz hiçbir
    // UI butonu/slider'ı tıklamaya cevap vermez (menü görünür ama ölüdür). Yoksa
    // burada oluştur; PauseMenu her kampanya sahnesindeki Player'da olduğu için
    // bu, tüm sahneleri tek seferde garantiye alır. Proje eski Input Manager'ı
    // kullanıyor (Input.GetKeyDown/GetAxisRaw) → StandaloneInputModule doğru modül.
    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    void Update()
    {
        // Rebind dinleme modu — bir sonraki tuşu yakala (ESC iptal)
        if (listeningIndex >= 0) { CaptureRebindKey(); return; }

        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (WaveManager.IsGameOver) return;
        if (controlsOpen) { CloseControls(); return; }
        if (settingsOpen) { CloseSettings(); return; }
        TogglePause();
    }

    // ─── Pause ───────────────────────────────────────────────────────

    void TogglePause() { if (isPaused) Resume(); else Pause(); }

    void Pause()
    {
        isPaused             = true;
        Time.timeScale       = 0f;
        AudioListener.pause  = true;
        Cursor.lockState     = CursorLockMode.None;
        Cursor.visible       = true;
        pauseRoot.SetActive(true);
        pausePanel.SetActive(true);
        TutorialHintUI.SetPaused(true);
    }

    void Resume()
    {
        isPaused             = false;
        CloseSettings();
        Time.timeScale       = 1f;
        AudioListener.pause  = false;
        Cursor.lockState     = CursorLockMode.Locked;
        Cursor.visible       = false;
        pausePanel.SetActive(false);
        pauseRoot.SetActive(false);
        TutorialHintUI.SetPaused(false);
    }

    void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    // ─── Ayarlar ─────────────────────────────────────────────────────

    void OpenSettings()  { settingsOpen = true;  settingsPanel.SetActive(true);  }
    void CloseSettings()
    {
        settingsOpen = false;
        controlsOpen = false;
        listeningIndex = -1;
        if (controlsPanel) controlsPanel.SetActive(false);
        settingsPanel.SetActive(false);
    }

    // ─── Kontroller (tuş atama) ──────────────────────────────────────

    void OpenControls()
    {
        controlsOpen = true;
        settingsPanel.SetActive(false);
        controlsPanel.SetActive(true);
        RefreshAllKeyButtons();
    }

    void CloseControls()
    {
        controlsOpen = false;
        listeningIndex = -1;
        controlsPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    void BeginRebind(int action)
    {
        listeningIndex = action;
        if (keyButtons != null && action < keyButtons.Length && keyButtons[action])
            keyButtons[action].text = "...";
    }

    void CaptureRebindKey()
    {
        foreach (KeyCode kc in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (kc == KeyCode.None || !Input.GetKeyDown(kc)) continue;
            if (kc != KeyCode.Escape)   // ESC = iptal
                KeyBindings.Set((KeyBindings.Action)listeningIndex, kc);
            RefreshKeyButton(listeningIndex);
            listeningIndex = -1;
            return;
        }
    }

    void RefreshKeyButton(int i)
    {
        if (keyButtons != null && i >= 0 && i < keyButtons.Length && keyButtons[i])
            keyButtons[i].text = KeyBindings.Get((KeyBindings.Action)i).ToString();
    }

    void RefreshAllKeyButtons()
    {
        for (int i = 0; i < KeyBindings.Count; i++) RefreshKeyButton(i);
    }

    void ResetBindings()
    {
        KeyBindings.ResetDefaults();
        RefreshAllKeyButtons();
    }

    void OnVolumeChanged(float v)
    {
        AudioListener.volume = v;
        PlayerPrefs.SetFloat("MasterVolume", v);
        if (volumeLabel) volumeLabel.text = $"SES   {v:0.0}";
    }

    void OnSensChanged(float v)
    {
        PlayerPrefs.SetFloat("Sensitivity", v);
        if (movement) movement.sensitivity = v;
        if (sensLabel) sensLabel.text = $"HASSASİYET   {v:0.0}";
    }

    // ─── UI Builder ──────────────────────────────────────────────────

    void BuildUI()
    {
        var canvasGO = new GameObject("PauseCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution  = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight   = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Tüm pause UI'ı saran root — yalnızca duraklayınca aktif
        pauseRoot = Stretch(canvasGO.transform, "PauseRoot");
        pauseRoot.AddComponent<Image>().color = Color.clear; // raycaster için
        var bgGO = Stretch(pauseRoot.transform, "BG");
        bgGO.AddComponent<Image>().color = BgColor;
        BuildScanLines(bgGO.transform);

        // ── Pause Paneli ──
        pausePanel = MakeCenterBox(pauseRoot.transform, "PausePanel", new Vector2(400, 420));
        AddTopLine(pausePanel.transform);
        AddTitle(pausePanel.transform, "DURAKLATILDI", new Vector2(0, 155));
        AddButton(pausePanel.transform, "DEVAM ET",        new Vector2(0,  50),  Resume);
        AddButton(pausePanel.transform, "AYARLAR",         new Vector2(0, -20), OpenSettings);
        AddButton(pausePanel.transform, "ANA MENÜYE DÖN",  new Vector2(0, -90), GoToMainMenu);
        // ── Ayarlar Paneli ──
        settingsPanel = MakeCenterBox(pauseRoot.transform, "SettingsPanel", new Vector2(420, 400));
        AddTopLine(settingsPanel.transform);
        AddSettingsTitle(settingsPanel.transform, "AYARLAR", new Vector2(0, 152));

        float savedVol  = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float savedSens = PlayerPrefs.GetFloat("Sensitivity", 2f);

        volumeLabel = AddSettingsLabel(settingsPanel.transform, $"SES   {savedVol:0.0}", new Vector2(0, 85));
        volumeSlider = AddSlider(settingsPanel.transform, new Vector2(0, 58), 0f, 1f, savedVol, OnVolumeChanged);

        sensLabel = AddSettingsLabel(settingsPanel.transform, $"HASSASİYET   {savedSens:0.0}", new Vector2(0, 10));
        sensSlider = AddSlider(settingsPanel.transform, new Vector2(0, -18), 0.5f, 10f, savedSens, OnSensChanged);

        AddButton(settingsPanel.transform, "KONTROLLER", new Vector2(0, -72), OpenControls);
        AddButton(settingsPanel.transform, "GERİ",       new Vector2(0, -135), CloseSettings);
        settingsPanel.SetActive(false);

        BuildControlsPanel();
        pauseRoot.SetActive(false);
    }

    // Kontroller paneli: her aksiyon için bir satır (etiket + mevcut tuş butonu),
    // altta "Varsayılana Dön". Tuş butonuna basınca rebind dinleme moduna girer.
    void BuildControlsPanel()
    {
        controlsPanel = MakeCenterBox(pauseRoot.transform, "ControlsPanel", new Vector2(520, 600));
        AddTopLine(controlsPanel.transform);
        AddSettingsTitle(controlsPanel.transform, "KONTROLLER", new Vector2(0, 270));

        int n = KeyBindings.Count;
        keyButtons = new TextMeshProUGUI[n];
        float startY = 220f, rowH = 32f;
        for (int i = 0; i < n; i++)
        {
            float y = startY - i * rowH;
            AddControlLabel(controlsPanel.transform, KeyBindings.DisplayNames[i], new Vector2(-140, y));
            int action = i;
            keyButtons[i] = AddKeyButton(controlsPanel.transform,
                KeyBindings.Get((KeyBindings.Action)i).ToString(),
                new Vector2(150, y), () => BeginRebind(action));
        }
        float by = startY - n * rowH - 14f;
        AddButton(controlsPanel.transform, "VARSAYILANA DÖN", new Vector2(0, by),        ResetBindings);
        AddButton(controlsPanel.transform, "GERİ",            new Vector2(0, by - 58f),  CloseControls);
        controlsPanel.SetActive(false);
    }

    void AddControlLabel(Transform parent, string text, Vector2 pos)
    {
        var go = new GameObject("CtrlLbl");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(230, 30);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = 16f; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.MidlineLeft; tmp.color = Color.white;
    }

    TextMeshProUGUI AddKeyButton(Transform parent, string label, Vector2 pos,
                                 UnityEngine.Events.UnityAction action)
    {
        float w = 150f, h = 30f;
        var border = new GameObject("KeyBtn");
        border.transform.SetParent(parent, false);
        var brt = border.AddComponent<RectTransform>();
        brt.anchoredPosition = pos; brt.sizeDelta = new Vector2(w, h);
        border.AddComponent<Image>().color = NeonRed;

        var bgGO  = Stretch(border.transform, "BG", new Vector2(2, 2), new Vector2(-2, -2));
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = BtnBg;

        var textGO = Stretch(border.transform, "Text");
        var tmp    = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 16f; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;

        var btn = border.AddComponent<Button>();
        btn.transition    = Selectable.Transition.ColorTint;
        btn.targetGraphic = bgImg;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.5f, 0.12f, 0.12f, 1f);
        colors.pressedColor     = new Color(0.7f, 0.15f, 0.15f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(action);
        return tmp;
    }

    // ─── Yardımcılar ─────────────────────────────────────────────────

    void BuildScanLines(Transform parent)
    {
        for (int i = 0; i < 160; i++)
        {
            var go = new GameObject("SL");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot     = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(0, i * 8);
            rt.offsetMax = new Vector2(0, i * 8 + 2);
            go.AddComponent<Image>().color = ScanLine;
        }
    }

    GameObject MakeCenterBox(Transform parent, string name, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = size;
        go.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.04f, 0.97f);
        return go;
    }

    void AddTopLine(Transform parent)
    {
        var go = new GameObject("TopLine");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(0, 3);
        go.AddComponent<Image>().color = NeonRed;
    }

    void AddTitle(Transform parent, string text, Vector2 pos)
    {
        // Glow
        var glow = new GameObject("TitleGlow");
        glow.transform.SetParent(parent, false);
        var grt = glow.AddComponent<RectTransform>();
        grt.anchoredPosition = pos;
        grt.sizeDelta        = new Vector2(380, 70);
        var gtmp = glow.AddComponent<TextMeshProUGUI>();
        gtmp.text = text; gtmp.fontSize = 42f; gtmp.fontStyle = FontStyles.Bold;
        gtmp.alignment = TextAlignmentOptions.Center; gtmp.color = NeonRedGlow;
        gtmp.characterSpacing = 5f; gtmp.enableWordWrapping = false;

        // Ana
        var main = new GameObject("Title");
        main.transform.SetParent(parent, false);
        var mrt = main.AddComponent<RectTransform>();
        mrt.anchoredPosition = pos;
        mrt.sizeDelta        = new Vector2(380, 70);
        var mtmp = main.AddComponent<TextMeshProUGUI>();
        mtmp.text = text; mtmp.fontSize = 38f; mtmp.fontStyle = FontStyles.Bold;
        mtmp.alignment = TextAlignmentOptions.Center; mtmp.color = NeonRed;
        mtmp.characterSpacing = 5f; mtmp.enableWordWrapping = false;
    }

    void AddSettingsTitle(Transform parent, string text, Vector2 pos)
    {
        var go = new GameObject("SettTitle");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(380, 50);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = 34f; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = NeonRed;
        tmp.characterSpacing = 5f;
    }

    TextMeshProUGUI AddSettingsLabel(Transform parent, string text, Vector2 pos)
    {
        var go = new GameObject("Lbl");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(380, 32);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = 16f; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
        tmp.characterSpacing = 3f;
        return tmp;
    }

    void AddButton(Transform parent, string label, Vector2 pos,
                   UnityEngine.Events.UnityAction action)
    {
        float w = 300f, h = 50f;

        var border = new GameObject("Btn_" + label);
        border.transform.SetParent(parent, false);
        var brt = border.AddComponent<RectTransform>();
        brt.anchoredPosition = pos;
        brt.sizeDelta        = new Vector2(w, h);
        border.AddComponent<Image>().color = NeonRed;

        var bgGO  = Stretch(border.transform, "BG", new Vector2(2, 2), new Vector2(-2, -2));
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = BtnBg;

        var fillGO  = Stretch(border.transform, "Fill", new Vector2(2, 2), new Vector2(-2, -2));
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = new Color(NeonRedFill.r, NeonRedFill.g, NeonRedFill.b, 0f);

        var textGO = Stretch(border.transform, "Text");
        var tmp    = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 20f; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
        tmp.characterSpacing = 4f;

        var btn = border.AddComponent<Button>();
        btn.transition    = Selectable.Transition.None;
        btn.targetGraphic = bgImg;
        btn.onClick.AddListener(action);

        var hover = border.AddComponent<ButtonHoverEffect>();
        hover.fillImage = fillImg;
        hover.fillColor = NeonRedFill;
    }

    Slider AddSlider(Transform parent, Vector2 pos, float min, float max, float value,
                     UnityEngine.Events.UnityAction<float> onChange)
    {
        var go = new GameObject("Slider");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(340, 22);

        var slider      = go.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value    = value;

        Stretch(go.transform, "BG").AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f);

        var fillArea = Stretch(go.transform, "FillArea", new Vector2(4, 0), new Vector2(-4, 0));
        var fill     = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillRT = fill.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero;
        fill.AddComponent<Image>().color = NeonRed;
        slider.fillRect = fillRT;

        var handleArea = Stretch(go.transform, "HandleArea", new Vector2(8, 0), new Vector2(-8, 0));
        var handle     = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        var handleRT   = handle.AddComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(14, 26);
        var handleImg  = handle.AddComponent<Image>();
        handleImg.color    = Color.white;
        slider.handleRect  = handleRT;
        slider.targetGraphic = handleImg;

        slider.onValueChanged.AddListener(onChange);
        return slider;
    }

    static GameObject Stretch(Transform parent, string name,
                               Vector2 minOff = default, Vector2 maxOff = default)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = minOff;      rt.offsetMax = maxOff;
        return go;
    }
}
}
