using UnityEngine;

namespace Bloodrush.Player
{
// Merkezi tuş atama sistemi. Tüm KeyCode-tabanlı aksiyon tuşları buradan okunur;
// scriptler artık kendi sabit tuşlarını değil bunu kullanır. Atamalar PlayerPrefs'te
// saklanır (kalıcı). Pause menüsündeki Kontroller paneli Set/ResetDefaults çağırır.
// Hareket (WASD) ve fare aksiyonları burada DEĞİL — onlar Unity Input axes/mouse.
public static class KeyBindings
{
    public enum Action
    {
        Grapple, ParryPunch, Slide, Stimulant, Reload,
        LauncherMode, Weapon1, Weapon2, Weapon3, Interact, Jump
    }

    static readonly KeyCode[] Defaults =
    {
        KeyCode.E,           // Grapple
        KeyCode.F,           // ParryPunch
        KeyCode.LeftControl, // Slide
        KeyCode.C,           // Stimulant
        KeyCode.R,           // Reload
        KeyCode.Q,           // LauncherMode
        KeyCode.Alpha1,      // Weapon1
        KeyCode.Alpha2,      // Weapon2
        KeyCode.Alpha3,      // Weapon3
        KeyCode.G,           // Interact
        KeyCode.Space,       // Jump
    };

    // Kontroller panelinde gösterilecek adlar (Action sırasıyla aynı)
    public static readonly string[] DisplayNames =
    {
        "Kanca", "Parry / Yumruk", "Kayma", "Stimulant", "Şarjör Değiştir",
        "Launcher Modu", "Silah 1", "Silah 2", "Silah 3", "Etkileşim", "Zıplama",
    };

    public static int Count => Defaults.Length;

    static KeyCode[] cache;

    static string PrefKey(Action a) => "bind_" + a;

    static void EnsureCache()
    {
        if (cache != null) return;
        cache = new KeyCode[Defaults.Length];
        for (int i = 0; i < Defaults.Length; i++)
            cache[i] = (KeyCode)PlayerPrefs.GetInt(PrefKey((Action)i), (int)Defaults[i]);
    }

    public static KeyCode Get(Action a) { EnsureCache(); return cache[(int)a]; }
    public static KeyCode Default(Action a) => Defaults[(int)a];

    public static void Set(Action a, KeyCode k)
    {
        EnsureCache();
        cache[(int)a] = k;
        PlayerPrefs.SetInt(PrefKey(a), (int)k);
        PlayerPrefs.Save();
    }

    public static void ResetDefaults()
    {
        for (int i = 0; i < Defaults.Length; i++)
            PlayerPrefs.DeleteKey(PrefKey((Action)i));
        PlayerPrefs.Save();
        cache = null;   // sonraki Get varsayılanları yeniden yükler
    }

    // ── Kolaylık erişimleri ──
    public static KeyCode Grapple      => Get(Action.Grapple);
    public static KeyCode ParryPunch   => Get(Action.ParryPunch);
    public static KeyCode Slide        => Get(Action.Slide);
    public static KeyCode Stimulant    => Get(Action.Stimulant);
    public static KeyCode Reload       => Get(Action.Reload);
    public static KeyCode LauncherMode => Get(Action.LauncherMode);
    public static KeyCode Weapon1      => Get(Action.Weapon1);
    public static KeyCode Weapon2      => Get(Action.Weapon2);
    public static KeyCode Weapon3      => Get(Action.Weapon3);
    public static KeyCode Interact     => Get(Action.Interact);
    public static KeyCode Jump         => Get(Action.Jump);
}
}
