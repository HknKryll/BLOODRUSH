using UnityEngine;
using UnityEngine.SceneManagement;
using Bloodrush.Shared.Audio;
using Bloodrush.FX;
using Bloodrush.UI;
using Bloodrush.Player;

namespace Bloodrush.Settings
{
// Ayarlari motora ve oyuna uygular. Veri katmani (SettingsStore) ile arayuz arasinda
// duran tek yer — arayuz sadece degeri degistirip burayi cagirir.
//
// Ayarlarin bir kismi "uygulanir", bir kismi simdilik sadece SAKLANIR (projede henuz
// karsiligi yok): parlaklik, kan efektleri, hareket bulanikligi, alt yazi, dil,
// nisan alirken hassasiyet, Y ekseni ters, titresim. Bunlarin degeri
// SettingsStore.Current uzerinden okunabilir; ozellik yazildiginda tek satirla baglanir.
public static class SettingsApplier
{
    // Mixer'da acilan parametre adlari (Editor'da Expose edilir).
    public const string ParamMaster = "MasterVol";
    public const string ParamMusic  = "MusicVol";
    public const string ParamSfx    = "SfxVol";
    public const string ParamUi     = "UiVol";

    // Oyun acilir acilmaz: ayarlari yukle, uygula, bolum takibini baslat.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        SettingsStore.Load();
        ApplyAll();

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // "Devam Et" icin son bolumu kaydeder. Hicbir oynanis script'ine dokunmadan,
    // sadece sahne yuklenme olayini dinleyerek.
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsChapterScene(scene.name)) SettingsStore.RecordChapter(scene.name);

        // Sahnedeki yeni nesnelere (nisangah, kamera) ayarlari tekrar uygula.
        ApplyGameplay();
    }

    static bool IsChapterScene(string name)
        => !string.IsNullOrEmpty(name) && name.Length >= 3 &&
           name.StartsWith("CH") && char.IsDigit(name[2]);

    // ───────────────── Uygulama ─────────────────

    public static void ApplyAll()
    {
        ApplyDisplay();
        ApplyAudio();
        ApplyControls();
        ApplyGameplay();
    }

    public static void ApplyDisplay()
    {
        var s = SettingsStore.Current;

        QualitySettings.SetQualityLevel(
            Mathf.Clamp(s.qualityLevel, 0, QualitySettings.names.Length - 1), true);

        QualitySettings.vSyncCount    = s.vsync ? 1 : 0;
        // VSync acikken targetFrameRate yok sayilir; kapaliyken 0 = sinirsiz.
        Application.targetFrameRate   = s.fpsLimit <= 0 ? -1 : s.fpsLimit;

        int w = s.resolutionWidth  > 0 ? s.resolutionWidth  : Screen.width;
        int h = s.resolutionHeight > 0 ? s.resolutionHeight : Screen.height;
        var mode = (FullScreenMode)s.screenMode;

        if (w != Screen.width || h != Screen.height || mode != Screen.fullScreenMode)
            Screen.SetResolution(w, h, mode);

        SpeedEffect.FovOverride = s.fov;
    }

    public static void ApplyAudio()
    {
        var s = SettingsStore.Current;
        var mixer = AudioRouting.Mixer;
        if (mixer == null)
        {
            // Mixer henuz kurulmamis — sesler yine calar, sadece grup kontrolu yok.
            AudioListener.volume = s.volMaster;
            return;
        }

        AudioListener.volume = 1f;   // kontrol mixer'da, listener'i tavanda birak
        mixer.SetFloat(ParamMaster, ToDecibels(s.volMaster));
        mixer.SetFloat(ParamMusic,  ToDecibels(s.volMusic));
        mixer.SetFloat(ParamSfx,    ToDecibels(s.volSfx));
        mixer.SetFloat(ParamUi,     ToDecibels(s.volUi));
    }

    // DOGRUSAL -> dB. Kulak logaritmik duyar: 0..1 degeri dogrudan dB olarak vermek
    // kaydiricinin ilk %20'sinde sesi bitirir, gerisi duyulmaz. -80 dB pratikte sessiz.
    public static float ToDecibels(float linear01)
    {
        if (linear01 <= 0.0001f) return -80f;
        return Mathf.Log10(Mathf.Clamp01(linear01)) * 20f;
    }

    public static float FromDecibels(float db)
        => db <= -79.9f ? 0f : Mathf.Clamp01(Mathf.Pow(10f, db / 20f));

    public static void ApplyControls()
    {
        var s = SettingsStore.Current;

        // PlayerMovement PlayerPrefs'i SADECE Awake'te okuyor — yani menuden degistirince
        // o anki oyuncuya hic ulasmiyordu. Anahtari beslemeye devam ediyoruz (sonraki
        // sahne icin) AMA yasayan oyuncuya da dogrudan yaziyoruz.
        PlayerPrefs.SetFloat("Sensitivity", s.sensitivity);

        var pm = Object.FindFirstObjectByType<PlayerMovement>();
        if (pm != null)
        {
            pm.sensitivity = s.sensitivity;
            pm.InvertY     = s.invertY;
        }
    }

    public static void ApplyGameplay()
    {
        var s = SettingsStore.Current;

        CameraShake.ShakeScale = Mathf.Clamp01(s.cameraShake);

        if (CrosshairHUD.Instance != null)
            CrosshairHUD.Instance.ApplyStyle(s.crosshairType, s.crosshairColor);
    }
}
}
