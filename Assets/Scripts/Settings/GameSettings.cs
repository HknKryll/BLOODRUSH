using System;
using UnityEngine;

namespace Bloodrush.Settings
{
// Butun oyun ayarlarinin duz veri karsiligi. JsonUtility ile serilestirilir, o yuzden
// SADECE alanlar (property degil), Dictionary yok, hepsi public.
//
// Varsayilanlar burada tanimli: dosya yoksa ya da bozuksa yeni bir GameSettings()
// olusturulur ve oyun sorunsuz acilir.
[Serializable]
public class GameSettings
{
    // Dosya formati degisirse eski kayitlari tanimak icin.
    public int version = 1;

    // ───────────────── Goruntu ─────────────────
    [Tooltip("0 = acilista ekranin mevcut cozunurlugu kullanilir.")]
    public int  resolutionWidth  = 0;
    public int  resolutionHeight = 0;
    public int  refreshRate      = 0;
    public int  screenMode       = (int)FullScreenMode.FullScreenWindow;
    public int  monitorIndex     = 0;
    public bool vsync            = true;
    [Tooltip("0 = sinirsiz.")]
    public int  fpsLimit         = 0;
    [Tooltip("0=High Fidelity, 1=Balanced, 2=Performant.")]
    public int   qualityLevel = 1;
    public float fov          = 70f;
    public float brightness   = 1f;   // saklanir, henuz bagli degil

    // ───────────────── Ses (0..1 dogrusal) ─────────────────
    // dB'ye cevrim SettingsApplier'da: 20*log10(v). Dogrusal degeri dogrudan dB
    // vermek kaydiriciyi ilk %20'de "bitmis" gibi hissettirirdi.
    public float volMaster = 1f;
    public float volMusic  = 0.7f;
    public float volSfx    = 1f;
    public float volUi     = 0.8f;

    // ───────────────── Kontroller ─────────────────
    public float sensitivity    = 2f;
    public float adsSensitivity = 1f;     // saklanir
    public bool  invertY        = false;  // saklanir
    public bool  vibration      = true;   // saklanir
    [Tooltip("KeyBindings.Action sirasiyla KeyCode degerleri. Bos ise varsayilanlar.")]
    public int[] keyBindings = new int[0];

    // ───────────────── Oyun ─────────────────
    [Tooltip("0=Cizgi, 1=Cizgi+Nokta, 2=Sadece nokta, 3=Kapali.")]
    public int   crosshairType  = 1;
    public Color crosshairColor = Color.white;
    public float cameraShake    = 1f;
    public bool  bloodEffects   = true;   // saklanir
    public bool  motionBlur     = true;   // saklanir
    public bool  subtitles      = true;   // saklanir
    [Tooltip("0=Turkce, 1=English.")]
    public int   language       = 0;      // saklanir

    // ───────────────── Ilerleme (Devam Et) ─────────────────
    [Tooltip("Ulasilan son bolum sahnesi. Bos ise Devam Et pasif.")]
    public string lastChapter = "";

    // Bir sekmenin alanlarini varsayilana dondurur. Diger sekmelere dokunmaz.
    public void ResetSection(SettingsSection section)
    {
        var d = new GameSettings();
        switch (section)
        {
            case SettingsSection.Display:
                resolutionWidth = d.resolutionWidth; resolutionHeight = d.resolutionHeight;
                refreshRate = d.refreshRate; screenMode = d.screenMode;
                monitorIndex = d.monitorIndex; vsync = d.vsync; fpsLimit = d.fpsLimit;
                qualityLevel = d.qualityLevel; fov = d.fov; brightness = d.brightness;
                break;
            case SettingsSection.Audio:
                volMaster = d.volMaster; volMusic = d.volMusic;
                volSfx = d.volSfx; volUi = d.volUi;
                break;
            case SettingsSection.Controls:
                sensitivity = d.sensitivity; adsSensitivity = d.adsSensitivity;
                invertY = d.invertY; vibration = d.vibration;
                keyBindings = new int[0];   // bos = KeyBindings varsayilanlarina doner
                break;
            case SettingsSection.Gameplay:
                crosshairType = d.crosshairType; crosshairColor = d.crosshairColor;
                cameraShake = d.cameraShake; bloodEffects = d.bloodEffects;
                motionBlur = d.motionBlur; subtitles = d.subtitles; language = d.language;
                break;
        }
    }
}

public enum SettingsSection { Display, Audio, Controls, Gameplay }
}
