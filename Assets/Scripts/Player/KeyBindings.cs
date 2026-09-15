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
// GIRDI BIRLESTIRME (2026-09-15, Refactor plani Faz 2a+2b TAMAMLANDI): depolama
// katmani (cache/EnsureCache/WriteBack/Set/ResetDefaults) BILEREK KeyCode tabanli
// birakildi — GameSettings.keyBindings (int[]) semasi hic degismedi, mevcut
// kayitli ayar dosyalari bozulmadan calismaya devam ediyor. Bunun yerine AYNI
// cache uzerinden okuyan, yeni Input System (Keyboard/Mouse.current) kullanan
// bir sorgu katmani eklendi: Down/Held/Up (KeyBindings.Action tabanli tuketiciler
// icin — PlayerMovement/PlayerParry/PlayerShoot/GrapplingHook/StimulantSystem/
// PuzzleConsole), DownKey/HeldKey/UpKey (rebind sistemine hic bagli olmayan
// ham KeyCode alanlari icin — NpcDialogue/BookSession/InteractableBook/
// SeatInteractable/PresentationSequence/IntroSalonController/Flashlight/
// ProximityInteractable/EnemyAI debug), ve TryGetAnyKeyDown (SettingsPanel.Tabs.cs'teki
// CaptureRebind() icin — "hangi tusa basildi" genel tespiti). Proje genelinde
// Assets/Scripts altinda artik UnityEngine.Input'a gercek bir kod referansi
// kalmadi (EnemyDebugOverlay.cs haric — o, activeInputHandler ayari degisse
// bile calismaya devam etsin diye BILEREK #if ile hem eskiyi hem yeniyi
// destekliyor, dokunulmadi).
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
            case KeyCode.KeypadEnter:  return Key.NumpadEnter;
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
            case KeyCode.Insert:       return Key.Insert;
            case KeyCode.Delete:       return Key.Delete;
            case KeyCode.Home:         return Key.Home;
            case KeyCode.End:          return Key.End;
            case KeyCode.PageUp:       return Key.PageUp;
            case KeyCode.PageDown:     return Key.PageDown;
            case KeyCode.Pause:        return Key.Pause;
            case KeyCode.Print:        return Key.PrintScreen;
            case KeyCode.ScrollLock:   return Key.ScrollLock;
            case KeyCode.LeftWindows:  return Key.LeftMeta;
            case KeyCode.RightWindows: return Key.RightMeta;
            case KeyCode.KeypadPeriod: return Key.NumpadPeriod;
            case KeyCode.KeypadDivide: return Key.NumpadDivide;
            case KeyCode.KeypadMultiply: return Key.NumpadMultiply;
            case KeyCode.KeypadMinus: return Key.NumpadMinus;
            case KeyCode.KeypadPlus:  return Key.NumpadPlus;
            case KeyCode.KeypadEquals: return Key.NumpadEquals;
            default:                   return null;
        }
    }

    // Yukaridakinin tersi — CaptureRebind() "hangi tusa basildi" tespiti icin.
    // Kapsam yukaridaki ile ayni (gercekci rebind hedefleri); haritalanmamis
    // egzotik bir tusa basilirsa rebind sessizce yok sayilir (crash yok, veri
    // bozulmaz — oyuncu baska bir tusa basar).
    static KeyCode? FromInputSystemKey(Key key)
    {
        if (key >= Key.A && key <= Key.Z)
            return (KeyCode)((int)KeyCode.A + (key - Key.A));
        if (key >= Key.Digit0 && key <= Key.Digit9)
            return (KeyCode)((int)KeyCode.Alpha0 + (key - Key.Digit0));
        if (key >= Key.F1 && key <= Key.F12)
            return (KeyCode)((int)KeyCode.F1 + (key - Key.F1));
        if (key >= Key.Numpad0 && key <= Key.Numpad9)
            return (KeyCode)((int)KeyCode.Keypad0 + (key - Key.Numpad0));

        switch (key)
        {
            case Key.Space:        return KeyCode.Space;
            case Key.Escape:       return KeyCode.Escape;
            case Key.Enter:        return KeyCode.Return;
            case Key.NumpadEnter:  return KeyCode.KeypadEnter;
            case Key.Tab:          return KeyCode.Tab;
            case Key.Backspace:    return KeyCode.Backspace;
            case Key.LeftCtrl:     return KeyCode.LeftControl;
            case Key.RightCtrl:    return KeyCode.RightControl;
            case Key.LeftShift:    return KeyCode.LeftShift;
            case Key.RightShift:   return KeyCode.RightShift;
            case Key.LeftAlt:      return KeyCode.LeftAlt;
            case Key.RightAlt:     return KeyCode.RightAlt;
            case Key.UpArrow:      return KeyCode.UpArrow;
            case Key.DownArrow:    return KeyCode.DownArrow;
            case Key.LeftArrow:    return KeyCode.LeftArrow;
            case Key.RightArrow:   return KeyCode.RightArrow;
            case Key.CapsLock:     return KeyCode.CapsLock;
            case Key.LeftBracket:  return KeyCode.LeftBracket;
            case Key.RightBracket: return KeyCode.RightBracket;
            case Key.Semicolon:    return KeyCode.Semicolon;
            case Key.Quote:        return KeyCode.Quote;
            case Key.Comma:        return KeyCode.Comma;
            case Key.Period:       return KeyCode.Period;
            case Key.Slash:        return KeyCode.Slash;
            case Key.Backslash:    return KeyCode.Backslash;
            case Key.Minus:        return KeyCode.Minus;
            case Key.Equals:       return KeyCode.Equals;
            case Key.Backquote:    return KeyCode.BackQuote;
            case Key.Insert:       return KeyCode.Insert;
            case Key.Delete:       return KeyCode.Delete;
            case Key.Home:         return KeyCode.Home;
            case Key.End:          return KeyCode.End;
            case Key.PageUp:       return KeyCode.PageUp;
            case Key.PageDown:     return KeyCode.PageDown;
            case Key.Pause:        return KeyCode.Pause;
            case Key.PrintScreen:  return KeyCode.Print;
            case Key.ScrollLock:   return KeyCode.ScrollLock;
            case Key.LeftMeta:     return KeyCode.LeftWindows;
            case Key.RightMeta:    return KeyCode.RightWindows;
            case Key.NumpadPeriod:   return KeyCode.KeypadPeriod;
            case Key.NumpadDivide:   return KeyCode.KeypadDivide;
            case Key.NumpadMultiply: return KeyCode.KeypadMultiply;
            case Key.NumpadMinus:    return KeyCode.KeypadMinus;
            case Key.NumpadPlus:     return KeyCode.KeypadPlus;
            case Key.NumpadEquals:   return KeyCode.KeypadEquals;
            default:                 return null;
        }
    }

    // Bu karede yeni basilan HERHANGI bir tus/fare dugmesi var mi? Rebind
    // yakalama (SettingsPanel.Tabs.cs) icin — eskiden tum KeyCode enum'unu
    // Input.GetKeyDown ile tarardi, simdi Keyboard/Mouse.current uzerinden
    // ayni kapsami (Mouse0-4 dahil, Escape haric — cagiran taraf ayrica
    // ele aliyor) tarar.
    public static bool TryGetAnyKeyDown(out KeyCode result)
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            foreach (var control in kb.allKeys)
            {
                if (!control.wasPressedThisFrame) continue;
                if (control.keyCode == Key.Escape) continue;   // cagiran taraf ayri ele aliyor
                var mapped = FromInputSystemKey(control.keyCode);
                if (!mapped.HasValue) continue;
                result = mapped.Value;
                return true;
            }
        }

        var m = Mouse.current;
        if (m != null)
        {
            if (m.leftButton.wasPressedThisFrame)    { result = KeyCode.Mouse0; return true; }
            if (m.rightButton.wasPressedThisFrame)   { result = KeyCode.Mouse1; return true; }
            if (m.middleButton.wasPressedThisFrame)  { result = KeyCode.Mouse2; return true; }
            if (m.backButton.wasPressedThisFrame)    { result = KeyCode.Mouse3; return true; }
            if (m.forwardButton.wasPressedThisFrame) { result = KeyCode.Mouse4; return true; }
        }

        result = KeyCode.None;
        return false;
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

    // Genel amacli KeyCode sorgusu (Faz 2b) — KeyBindings rebind sistemine hic
    // bagli olmayan, dogrudan [SerializeField] KeyCode alani tutan tuketiciler icin
    // (NpcDialogue.talkKey, InteractableBook.interactKey, SeatInteractable'daki
    // menu navigasyonu vb.). Ayni ResolveControl esleme tablosunu kullanir.
    public static bool DownKey(KeyCode kc) { var c = ResolveControl(kc); return c != null && c.wasPressedThisFrame; }
    public static bool HeldKey(KeyCode kc) { var c = ResolveControl(kc); return c != null && c.isPressed; }
    public static bool UpKey(KeyCode kc)   { var c = ResolveControl(kc); return c != null && c.wasReleasedThisFrame; }

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
