using UnityEngine;

namespace Bloodrush.Player
{
// Merkezi tuş atama sistemi. Tüm KeyCode-tabanlı aksiyon tuşları buradan okunur;
// scriptler artık kendi sabit tuşlarını değil bunu kullanır. Atamalar PlayerPrefs'te
// saklanır (kalıcı). Pause menüsündeki Kontroller paneli Set/ResetDefaults çağırır.
// Hareket (WASD) ve fare aksiyonları burada DEĞİL — onlar Unity Input axes/mouse.
public static class KeyBindings
{
    // YENI: MoveForward..Fire eklendi. Bunlar eskiden Unity eksenlerinden
    // (Input.GetAxisRaw "Horizontal"/"Vertical") ve fare tusundan okunuyordu, yani
    // yeniden atanamiyordu. Artik hepsi tek yerden geliyor.
    // DIKKAT: yeni degerler listenin SONUNA eklendi — kayitli JSON dizileri index
    // tabanli oldugu icin mevcut atamalar bozulmasin diye.
    public enum Action
    {
        Grapple, ParryPunch, Slide, Stimulant, Reload,
        LauncherMode, Weapon1, Weapon2, Weapon3, Interact, Jump,
        MoveForward, MoveBack, MoveLeft, MoveRight, Fire, Pause
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
        KeyCode.W,           // MoveForward
        KeyCode.S,           // MoveBack
        KeyCode.A,           // MoveLeft
        KeyCode.D,           // MoveRight
        KeyCode.Mouse0,      // Fire
        KeyCode.Escape,      // Pause
    };

    // Kontroller panelinde gösterilecek adlar (Action sırasıyla aynı)
    public static readonly string[] DisplayNames =
    {
        "Kanca", "Parry / Yumruk", "Kayma", "Stimulant", "Şarjör Değiştir",
        "Launcher Modu", "Silah 1", "Silah 2", "Silah 3", "Etkileşim", "Zıplama",
        "İleri", "Geri", "Sol", "Sağ", "Ateş", "Duraklat",
    };

    // Duraklat tusu yeniden ATANAMAZ: oyuncu Escape'i baska bir seye atayip menuden
    // cikamaz hale gelmesin (kacis yolu her zaman acik kalmali).
    public static bool IsRebindable(Action a) => a != Action.Pause;

    public static int Count => Defaults.Length;

    static KeyCode[] cache;

    // DEPOLAMA ARTIK JSON: atamalar SettingsStore'daki ayar dosyasında, diğer tüm
    // ayarlarla aynı yerde tutuluyor (tek dosya = tek yedek, tek "sıfırla").
    // Dışarıya bakan API (Get/Set/Default/ResetDefaults) HİÇ DEĞİŞMEDİ — bunu okuyan
    // oynanış scriptlerinin haberi bile olmuyor.
    static void EnsureCache()
    {
        if (cache != null) return;

        cache = new KeyCode[Defaults.Length];
        var saved = Settings.SettingsStore.Current.keyBindings;

        for (int i = 0; i < Defaults.Length; i++)
            cache[i] = (saved != null && i < saved.Length && saved[i] != 0)
                     ? (KeyCode)saved[i]
                     : Defaults[i];
    }

    static void WriteBack()
    {
        var s = Settings.SettingsStore.Current;
        if (s.keyBindings == null || s.keyBindings.Length != Defaults.Length)
            s.keyBindings = new int[Defaults.Length];

        for (int i = 0; i < Defaults.Length; i++)
            s.keyBindings[i] = (int)cache[i];

        Settings.SettingsStore.Save();
    }

    public static KeyCode Get(Action a) { EnsureCache(); return cache[(int)a]; }
    public static KeyCode Default(Action a) => Defaults[(int)a];

    // Ayni tusu kullanan BASKA bir aksiyon varsa onu bosaltir ve hangisi oldugunu doner
    // (UI kullaniciya "cakisti, bosaltildi" diyebilsin diye). Cakisma sessizce
    // birakilsaydi iki aksiyon ayni tusla tetiklenirdi.
    public static Action? Set(Action a, KeyCode k)
    {
        EnsureCache();
        if (!IsRebindable(a)) return null;

        Action? cleared = null;
        for (int i = 0; i < cache.Length; i++)
        {
            if (i == (int)a || cache[i] != k) continue;
            if (!IsRebindable((Action)i)) continue;   // Duraklat'i asla bosaltma
            cache[i] = KeyCode.None;
            cleared  = (Action)i;
            break;
        }

        cache[(int)a] = k;
        WriteBack();
        return cleared;
    }

    public static void ResetDefaults()
    {
        cache = (KeyCode[])Defaults.Clone();
        WriteBack();
    }

    // Ayarlar dosyası dışarıdan değişirse (varsayılana dönme vb.) önbelleği tazele.
    public static void Invalidate() => cache = null;

    // ── Kolaylık erişimleri ──
    public static KeyCode Grapple      => Get(Action.Grapple);
    public static KeyCode ParryPunch   => Get(Action.ParryPunch);
    public static KeyCode Slide        => Get(Action.Slide);
    public static KeyCode Stimulant    => Get(Action.Stimulant);
    public static KeyCode Reload       => Get(Action.Reload);
    public static KeyCode LauncherMode => Get(Action.LauncherMode);
    public static KeyCode MoveForward  => Get(Action.MoveForward);
    public static KeyCode MoveBack     => Get(Action.MoveBack);
    public static KeyCode MoveLeft     => Get(Action.MoveLeft);
    public static KeyCode MoveRight    => Get(Action.MoveRight);
    public static KeyCode Fire         => Get(Action.Fire);
    public static KeyCode Pause        => Get(Action.Pause);

    // Hareket eksenleri — eskiden Input.GetAxisRaw("Horizontal"/"Vertical") ile
    // okunuyordu, o yuzden yeniden atanamiyordu. Ayni -1..1 araligini doner.
    public static float MoveX => (Input.GetKey(MoveRight) ? 1f : 0f) - (Input.GetKey(MoveLeft) ? 1f : 0f);
    public static float MoveZ => (Input.GetKey(MoveForward) ? 1f : 0f) - (Input.GetKey(MoveBack) ? 1f : 0f);

    public static KeyCode Weapon1      => Get(Action.Weapon1);
    public static KeyCode Weapon2      => Get(Action.Weapon2);
    public static KeyCode Weapon3      => Get(Action.Weapon3);
    public static KeyCode Interact     => Get(Action.Interact);
    public static KeyCode Jump         => Get(Action.Jump);
}
}
