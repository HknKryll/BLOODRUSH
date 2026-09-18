using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Bloodrush.Settings;
using Bloodrush.Player;

namespace Bloodrush.UI.Menu
{
// SettingsPanel'in sekme icerikleri. Ayri dosyada cunku her sekme kendi basina uzun;
// panelin iskeleti (SettingsPanel.cs) sade kalsin.
public partial class SettingsPanel
{
    // ───────────────── GÖRÜNTÜ ─────────────────

    static Resolution[] cachedResolutions;
    static string[]     cachedResolutionLabels;

    static void EnsureResolutions()
    {
        if (cachedResolutions != null) return;

        // Ayni cozunurlugun farkli tazeleme hizlarini tekillestir — liste sisirmesin.
        var seen = new HashSet<long>();
        var list = new List<Resolution>();
        foreach (var r in Screen.resolutions)
        {
            long key = ((long)r.width << 32) | (uint)r.height;
            if (seen.Add(key)) list.Add(r);
        }
        if (list.Count == 0) list.Add(new Resolution { width = Screen.width, height = Screen.height });

        cachedResolutions      = list.ToArray();
        cachedResolutionLabels = new string[list.Count];
        for (int i = 0; i < list.Count; i++)
            cachedResolutionLabels[i] = $"{list[i].width} × {list[i].height}";
    }

    void BuildDisplayTab()
    {
        ResetRows();
        var s = SettingsStore.Current;
        EnsureResolutions();

        SectionHeader("EKRAN");

        int curRes = 0;
        for (int i = 0; i < cachedResolutions.Length; i++)
            if (cachedResolutions[i].width == (s.resolutionWidth > 0 ? s.resolutionWidth : Screen.width) &&
                cachedResolutions[i].height == (s.resolutionHeight > 0 ? s.resolutionHeight : Screen.height))
            { curRes = i; break; }

        AddStepper("Çözünürlük", cachedResolutionLabels, curRes, i =>
        {
            var prevW = s.resolutionWidth; var prevH = s.resolutionHeight;
            s.resolutionWidth  = cachedResolutions[i].width;
            s.resolutionHeight = cachedResolutions[i].height;
            ApplyDisplayWithRevert(prevW, prevH, s.screenMode);
        });

        AddStepper("Ekran Modu",
            new[] { "TAM EKRAN", "KENARLIKSIZ", "PENCERE" },
            ScreenModeToIndex((FullScreenMode)s.screenMode), i =>
            {
                int prevMode = s.screenMode;
                s.screenMode = (int)IndexToScreenMode(i);
                ApplyDisplayWithRevert(s.resolutionWidth, s.resolutionHeight, prevMode);
            });

        int monitorCount = Mathf.Max(1, Display.displays.Length);
        var monitorLabels = new string[monitorCount];
        for (int i = 0; i < monitorCount; i++) monitorLabels[i] = $"MONİTÖR {i + 1}";
        AddStepper("Monitör", monitorLabels, Mathf.Clamp(s.monitorIndex, 0, monitorCount - 1),
            i => { s.monitorIndex = i; SettingsApplier.ApplyDisplay(); });

        SectionHeader("PERFORMANS");

        AddToggle("Dikey Senkron", s.vsync, v =>
        {
            s.vsync = v;
            SettingsApplier.ApplyDisplay();
        });

        var fpsOptions = new[] { "SINIRSIZ", "30", "60", "120", "144", "240" };
        var fpsValues  = new[] { 0, 30, 60, 120, 144, 240 };
        int fpsIndex = System.Array.IndexOf(fpsValues, s.fpsLimit);
        AddStepper("FPS Sınırı", fpsOptions, fpsIndex < 0 ? 0 : fpsIndex, i =>
        {
            s.fpsLimit = fpsValues[i];
            SettingsApplier.ApplyDisplay();
        });

        var qualityNames = QualitySettings.names;
        AddStepper("Kalite", qualityNames, Mathf.Clamp(s.qualityLevel, 0, qualityNames.Length - 1),
            i => { s.qualityLevel = i; SettingsApplier.ApplyDisplay(); });

        SectionHeader("GÖRÜŞ");

        AddSlider("Görüş Açısı (FOV)", 60f, 110f, s.fov, true,
            v => $"{v:0}°", v => { s.fov = v; SettingsApplier.ApplyDisplay(); });

        MarkPending("Parlaklık");
        AddSlider("Parlaklık", 0.5f, 1.5f, s.brightness, false,
            v => $"{v * 100f:0}%", v => s.brightness = v);
    }

    static int ScreenModeToIndex(FullScreenMode m) => m switch
    {
        FullScreenMode.ExclusiveFullScreen => 0,
        FullScreenMode.FullScreenWindow    => 1,
        _                                  => 2,
    };

    static FullScreenMode IndexToScreenMode(int i) => i switch
    {
        0 => FullScreenMode.ExclusiveFullScreen,
        1 => FullScreenMode.FullScreenWindow,
        _ => FullScreenMode.Windowed,
    };

    // Cozunurluk/ekran modu geri donusu zor: uygula, sonra 10 sn geri sayan onay goster.
    // Oyuncu hicbir sey yapmazsa ESKI DEGERE DONULUR — ekran bozuk acilip oyuncuyu
    // kilitlemesin diye.
    void ApplyDisplayWithRevert(int prevW, int prevH, int prevMode)
    {
        var s = SettingsStore.Current;
        int newW = s.resolutionWidth, newH = s.resolutionHeight, newMode = s.screenMode;

        SettingsApplier.ApplyDisplay();
        SettingsStore.Save();

        ConfirmDialog.AskWithRevert(
            "EKRAN AYARI",
            "Bu ayarı korumak istiyor musun?\nOtomatik geri alınacak: {0} sn",
            "KORU",
            onKeep: () =>
            {
                s.resolutionWidth = newW; s.resolutionHeight = newH; s.screenMode = newMode;
                SettingsStore.Save();
            },
            onRevert: () =>
            {
                s.resolutionWidth = prevW; s.resolutionHeight = prevH; s.screenMode = prevMode;
                SettingsApplier.ApplyDisplay();
                SettingsStore.Save();
                ShowTab(SettingsSection.Display);
            },
            seconds: 10f);
    }

    // ───────────────── SES ─────────────────

    void BuildAudioTab()
    {
        ResetRows();
        var s = SettingsStore.Current;

        SectionHeader("SEVİYELER");

        // Kaydiricilar dogrusal 0-1; SettingsApplier bunu dB'ye cevirir (20*log10).
        // Dogrusal degeri dogrudan dB vermek kaydiricinin ilk %20'sinde sesi bitirirdi.
        AddSlider("Ana Ses", 0f, 1f, s.volMaster, false, Percent,
            v => { s.volMaster = v; SettingsApplier.ApplyAudio(); });

        AddSlider("Müzik", 0f, 1f, s.volMusic, false, Percent,
            v => { s.volMusic = v; SettingsApplier.ApplyAudio(); });

        AddSlider("Efektler", 0f, 1f, s.volSfx, false, Percent,
            v => { s.volSfx = v; SettingsApplier.ApplyAudio(); });

        AddSlider("Arayüz Sesleri", 0f, 1f, s.volUi, false, Percent,
            v => { s.volUi = v; SettingsApplier.ApplyAudio(); });

        if (Shared.Audio.AudioRouting.Mixer == null)
            SectionHeader("UYARI  ·  GameMixer BULUNAMADI — SEVİYELER TEK GRUPTA");
    }

    static string Percent(float v) => $"{v * 100f:0}%";

    // ───────────────── KONTROLLER ─────────────────

    void BuildControlsTab()
    {
        ResetRows();
        var s = SettingsStore.Current;

        SectionHeader("FARE");

        AddSlider("Hassasiyet", 0.1f, 10f, s.sensitivity, false,
            v => $"{v:0.0}", v => { s.sensitivity = v; SettingsApplier.ApplyControls(); });

        AddToggle("Y Eksenini Ters Çevir", s.invertY,
            v => { s.invertY = v; SettingsApplier.ApplyControls(); });

        // KALDIRILDI: "Nişan Alırken Hassasiyet" ve "Gamepad Titreşimi" — projede ne ADS
        // (nişan alma) durumu ne de titreşim kullanan bir kod var. İşlevsiz seçenek hiç
        // olmayandan kötü. JSON alanları duruyor, özellik yazılınca tek satırla geri gelir.

        SectionHeader("TUŞ ATAMALARI");

        for (int i = 0; i < KeyBindings.Count; i++)
        {
            int index = i;
            var action = (KeyBindings.Action)index;
            bool rebindable = KeyBindings.IsRebindable(action);

            var c = NextRow(KeyBindings.DisplayNames[index]);

            // Atama bekleniyorsa o satir "BEKLENIYOR..." gosterir — ayri bir pencere
            // acmiyoruz: diyalogun kendi Esc yakalamasi panelinkiyle carpisiyordu.
            KeyCode cur = KeyBindings.Get(action);
            string caption = rebindIndex == index ? "BEKLENİYOR…"
                           : cur == KeyCode.None  ? "—  (BOŞ)"
                           : PrettyKey(cur);

            var btn = MenuUI.Button(c, theme, caption,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 36f),
                rebindable ? (System.Action)(() => BeginRebind(index)) : null,
                TMPro.TextAlignmentOptions.Center,
                rebindIndex == index ? theme.accent : theme.accentText);

            // Duraklat kilitli: Escape her zaman menüden çıkış yolu olarak kalmalı.
            if (!rebindable) btn.SetInteractable(false);
            contentSelectables.Add(btn.GetComponent<Selectable>());
        }

        if (rebindIndex >= 0)
            SectionHeader("YENİ TUŞA BAS  ·  FARE TUŞLARI DA OLUR  ·  VAZGEÇMEK İÇİN ESC");
        else if (!string.IsNullOrEmpty(conflictNote))
            SectionHeader(conflictNote);
    }

    // KeyCode adlarını okunur hale getirir (Alpha1 → 1, Mouse0 → SOL TIK ...).
    static string PrettyKey(KeyCode k) => k switch
    {
        KeyCode.Mouse0 => "SOL TIK",
        KeyCode.Mouse1 => "SAĞ TIK",
        KeyCode.Mouse2 => "ORTA TIK",
        KeyCode.Mouse3 => "FARE 4",
        KeyCode.Mouse4 => "FARE 5",
        KeyCode.LeftControl  => "SOL CTRL",
        KeyCode.RightControl => "SAĞ CTRL",
        KeyCode.LeftShift    => "SOL SHIFT",
        KeyCode.RightShift   => "SAĞ SHIFT",
        KeyCode.LeftAlt      => "SOL ALT",
        KeyCode.Space        => "BOŞLUK",
        _ => k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9
             ? ((int)(k - KeyCode.Alpha0)).ToString()
             : k.ToString().ToUpperInvariant(),
    };

    int rebindIndex = -1;
    string conflictNote;
    public bool IsRebinding => rebindIndex >= 0;

    void BeginRebind(int index)
    {
        rebindIndex  = index;
        conflictNote = null;
        ShowTab(SettingsSection.Controls);   // satiri "BEKLENIYOR..." haline getir
    }

    // Rebind dinleyicisi (Faz 2b, 2026-09-15): artik Keyboard/Mouse.current
    // (yeni Input System) uzerinden — KeyBindings.TryGetAnyKeyDown() tum
    // klavye/fare basislarini tarayip Set() icin uygun bir KeyCode donuyor.
    // KeyBindings'in kendi deposu (Set/cache) hala KeyCode tabanli, degismedi.
    void CaptureRebind()
    {
        if (rebindIndex < 0) return;

        // ESC atamayi IPTAL eder ve tuketir — ayni karede paneli (ve pause'u) kapatmasin.
        if (KeyBindings.DownKey(KeyCode.Escape) && Flow.InteractionInput.TryConsumeEscape())
        {
            rebindIndex = -1;
            ShowTab(SettingsSection.Controls);
            return;
        }

        if (!KeyBindings.TryGetAnyKeyDown(out KeyCode k)) return;

        var action  = (KeyBindings.Action)rebindIndex;
        var cleared = KeyBindings.Set(action, k);

        // Cakisma: ayni tusu kullanan eski aksiyon bosaltildi, kullaniciya soyle.
        conflictNote = cleared.HasValue
            ? $"“{KeyBindings.DisplayNames[(int)cleared.Value]}” ÇAKIŞTI — BOŞALTILDI, YENİDEN ATA"
            : null;

        rebindIndex = -1;
        ShowTab(SettingsSection.Controls);
    }

    // ───────────────── OYUN ─────────────────

    void BuildGameplayTab()
    {
        ResetRows();
        var s = SettingsStore.Current;

        SectionHeader("NİŞANGÂH");

        AddStepper("Tip", new[] { "ÇİZGİ", "ÇİZGİ + NOKTA", "NOKTA", "KAPALI" },
            s.crosshairType, i => { s.crosshairType = i; SettingsApplier.ApplyGameplay(); });

        var colorNames = new[] { "BEYAZ", "ALTIN", "CAMGÖBEĞİ", "YEŞİL", "KIRMIZI" };
        var colors = new[] { Color.white, theme.accent, new Color(0.4f, 0.9f, 1f),
                             new Color(0.45f, 1f, 0.55f), new Color(1f, 0.35f, 0.3f) };
        int colorIndex = 0;
        for (int i = 0; i < colors.Length; i++)
            if (ApproximatelyEqual(colors[i], s.crosshairColor)) { colorIndex = i; break; }

        AddStepper("Renk", colorNames, colorIndex, i =>
        {
            s.crosshairColor = colors[i];
            SettingsApplier.ApplyGameplay();
        });

        SectionHeader("KAMERA");

        // Kullanicinin ozellikle istedigi ayar: kanca + hizli hareket bazi oyunculara
        // mide bulantisi yapiyor, bu yuzden SIFIRA kadar inebilmeli.
        AddSlider("Kamera Sarsıntısı", 0f, 1f, s.cameraShake, false,
            v => v <= 0.001f ? "KAPALI" : $"{v * 100f:0}%",
            v => { s.cameraShake = v; SettingsApplier.ApplyGameplay(); });

        MarkPending("Hareket Bulanıklığı");
        AddToggle("Hareket Bulanıklığı", s.motionBlur, v => s.motionBlur = v);

        SectionHeader("DİĞER");

        MarkPending("Kan Efektleri");
        AddToggle("Kan Efektleri", s.bloodEffects, v => s.bloodEffects = v);

        MarkPending("Altyazı");
        AddToggle("Altyazı", s.subtitles, v => s.subtitles = v);

        MarkPending("Dil");
        AddStepper("Dil", new[] { "TÜRKÇE", "ENGLISH" }, s.language, i => s.language = i);
    }

    static bool ApproximatelyEqual(Color a, Color b)
        => Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;
}
}
