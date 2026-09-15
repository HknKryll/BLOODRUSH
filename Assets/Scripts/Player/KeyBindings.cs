using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Bloodrush.Player
{
// Merkezi tuş atama sistemi. Tüm KeyCode-tabanlı aksiyon tuşları buradan okunur;
// scriptler artık kendi sabit tuşlarını değil bunu kullanır. Atamalar PlayerPrefs'te
// saklanır (kalıcı). Pause menüsündeki Kontroller paneli Set/ResetDefaults çağırır.
// Hareket (WASD) ve fare aksiyonları burada DEĞİL — onlar Unity Input axes/mouse.
//
// GIRDI BIRLESTIRME (2026-09-15, Refactor plani Faz 2a): depolama katmani
// (cache/EnsureCache/WriteBack/Set/ResetDefaults) BILEREK KeyCode tabanli
// birakildi — SettingsPanel.Tabs.cs'teki CaptureRebind() hala legacy Input
// ile tum KeyCode degerlerini tarayip Set() cagiriyor, ona dokunmadik.
// Bunun yerine AYNI cache uzerinden okuyan, yeni Input System (Keyboard/Mouse.current)
// kullanan bir sorgu katmani (Down/Held/Up) EKLENDI. PlayerMovement/PlayerParry/
// PlayerShoot/GrapplingHook bu yeni katmani kullaniyor; NpcDialogue/InteractionInput/
// BookSession henuz eski KeyCode-donen ozelliklere (Grapple, Jump vb.) bagli —
// onlar Faz 2b'de tasinacak. Iki katman ayni cache'i okudugu icin senkron kalirlar.
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
    // (Faz 2a: artik Input.GetKey degil, asagidaki yeni Held() kullanir.)
    public static float MoveX => (Held(Action.MoveRight) ? 1f : 0f) - (Held(Action.MoveLeft) ? 1f : 0f);
    public static float MoveZ => (Held(Action.MoveForward) ? 1f : 0f) - (Held(Action.MoveBack) ? 1f : 0f);

    public static KeyCode Weapon1      => Get(Action.Weapon1);
    public static KeyCode Weapon2      => Get(Action.Weapon2);
    public static KeyCode Weapon3      => Get(Action.Weapon3);
    public static KeyCode Interact     => Get(Action.Interact);
    public static KeyCode Jump         => Get(Action.Jump);

    // ══════════════ YENI INPUT SYSTEM SORGU KATMANI (Faz 2a) ══════════════
    // Asagidakiler UnityEngine.Input'a HIC dokunmuyor — Keyboard.current /
    // Mouse.current uzerinden okuyor. Hangi KeyCode'un atali oldugunu yine
    // yukaridaki (degismemis) cache'ten aliyor, sadece OKUMA yontemi yeni.

    // KeyCode -> Input System Key eslemesi. Rebind UI'da zaten sadece
    // klavye + Mouse0-4 kabul ediliyor (CaptureRebind, joystick/garip
    // araliklari eliyor) — bu yuzden tam KeyCode enum'unu degil, gercekci
    // rebind hedeflerini kapsiyor.
    static Key? ToInputSystemKey(KeyCode kc)
    {
        if (kc >= KeyCode.A && kc <= KeyCode.Z)
            return (Key)((int)Key.A + (kc - KeyCode.A));
        if (kc >= KeyCode.Alpha0 && kc <= KeyCode.Alpha9)
            return (Key)((int)Key.Digit0 + (kc - KeyCode.Alpha0));
        if (kc >= KeyCode.F1 && kc <= KeyCode.F12)
            return (Key)((int)Key.F1 + (kc - KeyCode.F1));

        switch (kc)
        {
            case KeyCode.Space:        return Key.Space;
            case KeyCode.Escape:       return Key.Escape;
            case KeyCode.Return:       return Key.Enter;
            case KeyCode.Tab:          return Key.Tab;
            case KeyCode.Backspace:    return Key.Backspace;
            case KeyCode.LeftControl:  return Key.LeftCtrl;
            case KeyCode.RightControl: return Key.RightCtrl;
            case KeyCode.LeftShift:    return Key.LeftShift;
            case KeyCode.RightShift:   return Key.RightShift;
            case KeyCode.LeftAlt:      return Key.LeftAlt;
            case KeyCode.RightAlt:     return Key.RightAlt;
            case KeyCode.UpArrow:      return Key.UpArrow;
            case KeyCode.DownArrow:    return Key.DownArrow;
            case KeyCode.LeftArrow:    return Key.LeftArrow;
            case KeyCode.RightArrow:   return Key.RightArrow;
            case KeyCode.CapsLock:     return Key.CapsLock;
            case KeyCode.LeftBracket:  return Key.LeftBracket;
            case KeyCode.RightBracket: return Key.RightBracket;
            case KeyCode.Semicolon:    return Key.Semicolon;
            case KeyCode.Quote:        return Key.Quote;
            case KeyCode.Comma:        return Key.Comma;
            case KeyCode.Period:       return Key.Period;
            case KeyCode.Slash:        return Key.Slash;
            case KeyCode.Backslash:    return Key.Backslash;
            case KeyCode.Minus:        return Key.Minus;
            case KeyCode.Equals:       return Key.Equals;
            case KeyCode.BackQuote:    return Key.Backquote;
            default:                   return null;
        }
    }

    static ButtonControl ResolveControl(KeyCode kc)
    {
        if (kc >= KeyCode.Mouse0 && kc <= KeyCode.Mouse6)
        {
            var m = Mouse.current;
            if (m == null) return null;
            switch (kc)
            {
                case KeyCode.Mouse0: return m.leftButton;
                case KeyCode.Mouse1: return m.rightButton;
                case KeyCode.Mouse2: return m.middleButton;
                case KeyCode.Mouse3: return m.backButton;
                case KeyCode.Mouse4: return m.forwardButton;
                default:             return null;   // Mouse5/6 icin Input System karsiligi yok
            }
        }

        var kb = Keyboard.current;
        if (kb == null) return null;
        var key = ToInputSystemKey(kc);
        return key.HasValue ? kb[key.Value] : null;
    }

    // Aksiyonun bu karede basildi / basili / birakildi mi? (Input.GetKeyDown/GetKey/GetKeyUp yerine)
    public static bool Down(Action a) { var c = ResolveControl(Get(a)); return c != null && c.wasPressedThisFrame; }
    public static bool Held(Action a) { var c = ResolveControl(Get(a)); return c != null && c.isPressed; }
    public static bool Up(Action a)   { var c = ResolveControl(Get(a)); return c != null && c.wasReleasedThisFrame; }

    // Fare deltasi — eskiden Input.GetAxisRaw("Mouse X"/"Mouse Y") kullanilirdi.
    // ProjectSettings/InputManager.asset'teki "Mouse X"/"Mouse Y" eksenlerinin
    // sensitivity degeri 0.1 idi (gravity=0, tip=Mouse Movement); Input System'in
    // ham piksel deltasi bu carpanla ayni davranisi verir. Cagiran taraf (PlayerMovement)
    // kendi 'sensitivity' alanini ustune carpmaya devam ediyor, formul sekli aynen korunuyor.
    const float LegacyMouseAxisSensitivity = 0.1f;
    public static Vector2 MouseDelta
    {
        get
        {
            var m = Mouse.current;
            return m != null ? m.delta.ReadValue() * LegacyMouseAxisSensitivity : Vector2.zero;
        }
    }
}
}
