using UnityEngine;
using UnityEngine.SceneManagement;
using Bloodrush.Flow;
using Bloodrush.UI;

namespace Bloodrush.Player
{
// Oyuncunun ELİNDE NE VAR sorusunun tek merci. Asansör kazasında oyuncu istisnasız her
// şeyini kaybediyor (silah, launcher, kanca, yumruk, stimulant); ilerideki bir noktada
// (karanlık odada bulunan zayıf silah, kasabada tam teçhizat) geri veriliyor.
//
// GameProgress ile AYNI şekil: static sınıf, referans gerektirmez, sahneler arası yaşar.
// GameProgress "hangi silahı KAZANDIN" der (kalıcı ilerleme), PlayerLoadout "şu an
// hangisini KULLANABİLİYORSUN" der (anlık kısıt). İkisi çarpışırsa daha kısıtlayıcı olan
// kazanır — Apply() asla GameProgress'in ötesinde silah VERMEZ.
//
// Kullanım:
//   PlayerLoadout.DisarmAll();                          // kaza anı
//   PlayerLoadout.Grant(PlayerLoadout.Gear.Revolver);   // karanlık odada silah bulundu
//   PlayerLoadout.RestoreAll();                         // kasaba — her şey geri
public static class PlayerLoadout
{
    [System.Flags]
    public enum Gear
    {
        None      = 0,
        Revolver  = 1,
        Shotgun   = 2,
        Lmg       = 4,
        Launcher  = 8,
        Hook      = 16,
        Melee     = 32,   // yumruk / parry
        Stimulant = 64,
        All       = ~0,
    }

    public static Gear Allowed { get; private set; } = Gear.All;

    // Launcher bölüm ayarıdır (GameFlow Ch2'de kapatıyor). Silahsızlaştırırken üstüne
    // yazdığımız için, geri verirken bölümün kendi kararını ezmeyelim diye saklanır.
    static bool launcherBeforeDisarm = true;

    // "Kazadan sonraki sahne" durumunun tur içinde bir kez uygulanmasını sağlar.
    static bool postCrashApplied;

    // Yaralılık — ekipmandan ayrı bir BEDEN durumu, ama aynı "tur boyu kalıcı, her sahnede
    // Apply ile yeniden uygulanır" mantığını paylaştığı için burada duruyor.
    public static bool  Injured   { get; private set; }
    public static float JumpScale { get; private set; } = 1f;

    public static bool Has(Gear g)   => (Allowed & g) != 0;
    public static bool AnyFirearm    => (Allowed & (Gear.Revolver | Gear.Shotgun | Gear.Lmg)) != 0;
    public static bool ShowWeaponHUD => AnyFirearm;

    // ───────────────── Public API ─────────────────

    public static void DisarmAll()
    {
        var go = FindPlayer();
        var ps = go != null ? go.GetComponentInChildren<PlayerShoot>(true) : null;
        if (ps != null) launcherBeforeDisarm = ps.LauncherEnabled;

        Allowed = Gear.None;
        Debug.Log("[PlayerLoadout] Tüm ekipman alındı (silah / launcher / kanca / yumruk / stimulant).");
        Apply(go);
    }

    // Yaralı hâli: zıplama gücü kısılır (0 = hiç zıplayamaz, 1 = normal). İyileştiğinde
    // SetInjured(false) çağır.
    public static void SetInjured(bool value, float jumpScale = 0.55f)
    {
        Injured   = value;
        JumpScale = value ? Mathf.Clamp01(jumpScale) : 1f;

        // Iyilesince kaymayi geri ac (Apply sadece KISAR, asla vermez).
        if (!value)
        {
            var go = FindPlayer();
            var pm = go != null ? go.GetComponentInChildren<PlayerMovement>(true) : null;
            if (pm != null) pm.SlideEnabled = true;
        }
        Debug.Log($"[PlayerLoadout] Yaralı: {value} (zıplama ×{JumpScale:0.00})");
        Apply();
    }

    // "Bu sahne kazadan SONRA geçiyor" diyen sahneler (GameFlow.startDisarmed) çağırır:
    // silahsız + yaralı. Tur boyunca SADECE BİR KEZ etki eder — sahneyi doğrudan
    // Play'lediğinde de doğru durumla başlarsın, ama burada bulduğun silahı ölüp sahne
    // yeniden yüklenince KAYBETMEZSİN (statik bayrak sahne yüklemesini aşar).
    public static void EnterPostCrashState(float jumpScale)
    {
        if (postCrashApplied) return;
        postCrashApplied = true;
        DisarmAll();
        SetInjured(true, jumpScale);
    }

    public static void Grant(Gear g)
    {
        Allowed |= g;
        Debug.Log($"[PlayerLoadout] Ekipman verildi: {g} — güncel: {Allowed}");
        var go = FindPlayer();
        Reopen(go, g);
        Apply(go);
    }

    public static void Revoke(Gear g)
    {
        Allowed &= ~g;
        Debug.Log($"[PlayerLoadout] Ekipman alındı: {g} — güncel: {Allowed}");
        Apply();
    }

    public static void RestoreAll()
    {
        Allowed = Gear.All;
        Debug.Log("[PlayerLoadout] Tüm ekipman geri verildi.");
        var go = FindPlayer();
        Reopen(go, Gear.All);
        Apply(go);
    }

    // Yeni oyun (GameFlow.resetProgressOnStart → GameProgress.ResetRun) çağırır.
    public static void ResetRun()
    {
        Allowed              = Gear.All;
        launcherBeforeDisarm = true;
        postCrashApplied     = false;
        Injured              = false;
        JumpScale            = 1f;
    }

    // ───────────────── Uygulama ─────────────────

    // SADECE KISAR, asla vermez → istediğin kadar tekrar çağrılabilir (idempotent) ve
    // GameFlow'un allWeaponsThisScene / GameProgress kararlarını geri sarmaz.
    public static void Apply(GameObject player = null)
    {
        var go = player != null ? player : FindPlayer();
        if (go == null) return;

        var ps = go.GetComponentInChildren<PlayerShoot>(true);
        if (ps != null)
        {
            bool wasHidden = ps.WeaponsHidden;

            ps.CanShoot      = AnyFirearm || Has(Gear.Launcher);
            ps.WeaponsHidden = !AnyFirearm;

            if (!Has(Gear.Revolver)) ps.SetUnlocked(PlayerShoot.Firearm.Revolver, false);
            if (!Has(Gear.Shotgun))  ps.SetUnlocked(PlayerShoot.Firearm.Shotgun,  false);
            if (!Has(Gear.Lmg))      ps.SetUnlocked(PlayerShoot.Firearm.Lmg,      false);
            if (!Has(Gear.Launcher)) ps.LauncherEnabled = false;

            // Elde kilitli bir silah kaldıysa izinli olana geç — SwitchFirearm kilitli
            // silahtan ÇIKARMIYOR, sadece girişi engelliyor (bkz. PlayerShoot.SwitchFirearm).
            if (AnyFirearm && (wasHidden || !ps.IsUnlocked(ps.CurrentFirearm)))
                ps.ForceEquip(FirstAllowed(ps));
        }

        // Yaralı hâli — zıplama gücü. Varsayılan 1f olduğu için normal oyunda etkisiz.
        var pm = go.GetComponentInChildren<PlayerMovement>(true);
        if (pm != null)
        {
            pm.JumpMultiplier = JumpScale;
            // Yaraliyken kayma da kapali: kazadan cikmis biri kayarak ilerleyemez.
            // Zipla kisiti (JumpScale) ile ayni yerden yonetiliyor, boylece sahne
            // gecisini ve checkpoint respawn'i birlikte asiyorlar.
            if (Injured) pm.SlideEnabled = false;
        }

        // Kanca kendi CancelActive()'iyle güvenle geri sarılır (bkz. GrapplingHook:76-80).
        var hook = go.GetComponentInChildren<GrapplingHook>(true);
        if (hook != null && !Has(Gear.Hook)) hook.HookEnabled = false;

        var parry = go.GetComponentInChildren<PlayerParry>(true);
        if (parry != null && !Has(Gear.Melee)) parry.enabled = false;

        var stim = go.GetComponentInChildren<StimulantSystem>(true);
        if (stim != null && !Has(Gear.Stimulant))
        {
            stim.CancelBuffs();   // buff ortasında kapatılırsa çarpanlar sonsuza dek takılı kalırdı
            stim.enabled = false;
        }

        // Silah yokken nişangâh anlamsız (etkileşim promptları DialogueUI'dan geliyor,
        // nişangâha bağlı değil).
        if (CrosshairHUD.Instance != null &&
            CrosshairHUD.Instance.TryGetComponent(out Canvas crosshairCanvas))
            crosshairCanvas.enabled = AnyFirearm;
    }

    // Verilen ekipmanı fiilen geri açar — ateşli silahlarda GameProgress'i tavan kabul eder.
    static void Reopen(GameObject go, Gear g)
    {
        if (go == null) return;

        var ps = go.GetComponentInChildren<PlayerShoot>(true);
        if (ps != null)
        {
            if ((g & Gear.Revolver) != 0) ps.SetUnlocked(PlayerShoot.Firearm.Revolver, true);
            if ((g & Gear.Shotgun)  != 0) ps.SetUnlocked(PlayerShoot.Firearm.Shotgun, GameProgress.ShotgunUnlocked);
            if ((g & Gear.Lmg)      != 0) ps.SetUnlocked(PlayerShoot.Firearm.Lmg,     GameProgress.LmgUnlocked);
            if ((g & Gear.Launcher) != 0) ps.LauncherEnabled = launcherBeforeDisarm;
        }

        var hook = go.GetComponentInChildren<GrapplingHook>(true);
        if (hook != null && (g & Gear.Hook) != 0) hook.HookEnabled = true;

        var parry = go.GetComponentInChildren<PlayerParry>(true);
        if (parry != null && (g & Gear.Melee) != 0) parry.enabled = true;

        var stim = go.GetComponentInChildren<StimulantSystem>(true);
        if (stim != null && (g & Gear.Stimulant) != 0) stim.enabled = true;
    }

    static PlayerShoot.Firearm FirstAllowed(PlayerShoot ps)
    {
        if (Has(Gear.Revolver) && ps.IsUnlocked(PlayerShoot.Firearm.Revolver)) return PlayerShoot.Firearm.Revolver;
        if (Has(Gear.Shotgun)  && ps.IsUnlocked(PlayerShoot.Firearm.Shotgun))  return PlayerShoot.Firearm.Shotgun;
        return PlayerShoot.Firearm.Lmg;
    }

    static GameObject FindPlayer() => GameObject.FindGameObjectWithTag("Player");

    // ───────────────── Sahne geçişi ─────────────────

    // Kısıt sahne yüklemesini AŞMALI: karanlık oda ileride ayrı bir sahneye taşınabilir ve
    // GameFlow.Start() her sahnede silahları GameProgress'ten yeniden açıyor. sceneLoaded
    // her Start()'tan ÖNCE çalışır; Apply() bu aşamada güvenli (ForceEquip Start öncesi
    // kendini kapatıyor). Kısıt yokken hiçbir şey yapmaz — normal oyunu etkilemez.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Boot()
    {
        Allowed              = Gear.All;   // domain reload kapalıyken static'ler taşınmasın
        launcherBeforeDisarm = true;
        postCrashApplied     = false;
        Injured              = false;
        JumpScale            = 1f;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Allowed != Gear.All) Apply();
    }
}
}
