using System;
using System.IO;
using UnityEngine;

namespace Bloodrush.Settings
{
// Ayarlarin JSON dosyasina yazilmasi/okunmasi. PlayerPrefs DEGIL: tek dosya, elle
// okunabilir, yedeklenebilir, tek hamlede silinebilir.
//
// BOZUK DOSYA ASLA OYUNU DURDURMAZ: okuma/ayristirma her asamada try/catch icinde;
// herhangi bir sorunda varsayilanlara donulur ve Console'a bilgi yazilir.
public static class SettingsStore
{
    const string FileName = "bloodrush_settings.json";

    static GameSettings current;

    public static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

    public static GameSettings Current
    {
        get
        {
            if (current == null) Load();
            return current;
        }
    }

    public static event Action Changed;

    public static void Load()
    {
        current = ReadFromDisk() ?? new GameSettings();
    }

    static GameSettings ReadFromDisk()
    {
        try
        {
            if (!File.Exists(Path))
            {
                Debug.Log($"[SettingsStore] Ayar dosyasi yok, varsayilanlarla basliyor: {Path}");
                return null;
            }

            string json = File.ReadAllText(Path);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[SettingsStore] Ayar dosyasi bos — varsayilanlara donuldu.");
                return null;
            }

            var loaded = JsonUtility.FromJson<GameSettings>(json);
            if (loaded == null)
            {
                Debug.LogWarning("[SettingsStore] Ayar dosyasi cozulemedi — varsayilanlara donuldu.");
                return null;
            }

            Sanitize(loaded);
            Debug.Log($"[SettingsStore] Ayarlar yuklendi: {Path}");
            return loaded;
        }
        catch (Exception e)
        {
            // Bozuk JSON, izin sorunu, disk hatasi — hepsi ayni yere cikar: varsayilanlar.
            Debug.LogWarning($"[SettingsStore] Ayarlar okunamadi ({e.GetType().Name}: {e.Message}) — " +
                             "varsayilanlarla devam ediliyor.");
            return null;
        }
    }

    // Elle duzenlenmis ya da eski surumden gelmis dosyalarda sacma degerleri toparlar.
    static void Sanitize(GameSettings s)
    {
        s.volMaster = Mathf.Clamp01(s.volMaster);
        s.volMusic  = Mathf.Clamp01(s.volMusic);
        s.volSfx    = Mathf.Clamp01(s.volSfx);
        s.volUi     = Mathf.Clamp01(s.volUi);

        s.fov          = Mathf.Clamp(s.fov, 60f, 110f);
        s.brightness   = Mathf.Clamp(s.brightness, 0.5f, 1.5f);
        s.cameraShake  = Mathf.Clamp01(s.cameraShake);
        s.sensitivity  = Mathf.Clamp(s.sensitivity, 0.1f, 10f);
        s.adsSensitivity = Mathf.Clamp(s.adsSensitivity, 0.1f, 2f);

        s.qualityLevel = Mathf.Clamp(s.qualityLevel, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
        s.fpsLimit     = Mathf.Max(0, s.fpsLimit);
        s.monitorIndex = Mathf.Max(0, s.monitorIndex);
        s.crosshairType = Mathf.Clamp(s.crosshairType, 0, 3);

        if (s.keyBindings == null) s.keyBindings = new int[0];
    }

    public static void Save()
    {
        try
        {
            string json = JsonUtility.ToJson(Current, prettyPrint: true);
            File.WriteAllText(Path, json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SettingsStore] Ayarlar kaydedilemedi: {e.Message}");
        }
    }

    // Bir sekmeyi varsayilana dondur + uygula + kaydet.
    public static void ResetSection(SettingsSection section)
    {
        Current.ResetSection(section);
        SettingsApplier.ApplyAll();
        Save();
        Changed?.Invoke();
        Debug.Log($"[SettingsStore] '{section}' varsayilana dondu.");
    }

    // Bir ayar degistiginde cagrilir: aninda uygula + diske yaz (uygula butonu yok).
    public static void NotifyChanged(bool save = true)
    {
        Changed?.Invoke();
        if (save) Save();
    }

    // ───────────────── Devam Et ─────────────────

    public static bool HasContinue => !string.IsNullOrEmpty(Current.lastChapter);

    public static void RecordChapter(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        if (Current.lastChapter == sceneName) return;
        Current.lastChapter = sceneName;
        Save();
        Debug.Log($"[SettingsStore] Son bolum kaydedildi: {sceneName}");
    }
}
}
