using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Bloodrush.Flow;
using Bloodrush.Weapons;
using Bloodrush.Shared.Audio;

namespace Bloodrush.Player
{
public class PlayerShoot : MonoBehaviour
{
    public enum LauncherMode { Grenade, Flash }
    public enum Firearm { Revolver, Shotgun, Lmg }

    [Header("Revolver")]
    // 25: dusman1 (35 can) iki atisla olur. 40'ken tek atisliktı ve fazla guclu
    // hissettiriyordu. Zirhli/Buyuk_Dusman (100 can) 3 yerine 4 atis.
    [SerializeField] float revolverDamage  = 25f;
    [SerializeField] float revolverRange   = 120f;
    [SerializeField] float revolverFireRate = 0.28f;
    [Tooltip("Tepme çarpanı — ProceduralWeaponMotion'daki temel recoil değerleriyle çarpılır.")]
    [SerializeField] float revolverRecoilScale = 1f;
    [SerializeField] LayerMask hitMask     = ~0;
    [SerializeField] ParticleSystem muzzleFlash;

    [Header("Revolver Ammo")]
    [SerializeField] int   magazineSize = 10;
    [SerializeField] int   startingAmmo = 60;
    [SerializeField] float reloadTime   = 3f;

    [Header("Revolver Ses")]
    [SerializeField] AudioClip revolverFireClip;
    [SerializeField] [Range(0f,1f)] float revolverFireVolume = 1f;
    [SerializeField] AudioClip emptyClickClip;
    [SerializeField] [Range(0f,1f)] float emptyClickVolume = 0.6f;
    [SerializeField] AudioClip reloadClip;
    [SerializeField] [Range(0f,1f)] float reloadVolume = 0.8f;

    [Header("Shotgun")]
    [SerializeField] float shotgunDamageNear      = 18f;
    [SerializeField] float shotgunDamageFar       = 4f;
    [SerializeField] float shotgunRange           = 14f;
    [SerializeField] float shotgunFireRate        = 0.85f;
    [SerializeField] int   shotgunPellets         = 8;
    [SerializeField] float shotgunSpread          = 4.5f;
    [SerializeField] int   shotgunMagazineSize    = 6;
    [SerializeField] int   shotgunStartingReserve = 24;
    [SerializeField] float shotgunReloadTime      = 2.4f;
    [Tooltip("Tepme çarpanı — shotgun sert teper.")]
    [SerializeField] float shotgunRecoilScale     = 1.8f;
    [SerializeField] ParticleSystem shotgunMuzzleFlash;

    [Header("Shotgun Ses")]
    [SerializeField] AudioClip shotgunFireClip;
    [SerializeField] [Range(0f,1f)] float shotgunFireVolume = 1f;
    [SerializeField] AudioClip shotgunEmptyClickClip;
    [SerializeField] [Range(0f,1f)] float shotgunEmptyClickVolume = 0.6f;
    [SerializeField] AudioClip shotgunReloadClip;
    [SerializeField] [Range(0f,1f)] float shotgunReloadVolume = 0.8f;

    [Header("LMG (Hafif Makineli Tüfek)")]
    [SerializeField] float lmgDamage          = 9f;
    [SerializeField] float lmgRange           = 60f;
    [SerializeField] float lmgFireRate        = 0.09f;
    [SerializeField] float lmgSpread          = 1.5f;
    [SerializeField] int   lmgMagazineSize    = 45;
    [SerializeField] int   lmgStartingReserve = 135;
    [SerializeField] float lmgReloadTime      = 3.2f;
    [Tooltip("Tepme çarpanı — LMG hafif ama seri teper.")]
    [SerializeField] float lmgRecoilScale     = 0.45f;
    [SerializeField] ParticleSystem lmgMuzzleFlash;

    [Header("LMG Ses")]
    [SerializeField] AudioClip lmgFireClip;
    [SerializeField] [Range(0f,1f)] float lmgFireVolume = 0.9f;
    [SerializeField] AudioClip lmgEmptyClickClip;
    [SerializeField] [Range(0f,1f)] float lmgEmptyClickVolume = 0.6f;
    [SerializeField] AudioClip lmgReloadClip;
    [SerializeField] [Range(0f,1f)] float lmgReloadVolume = 0.8f;

    [Header("Launcher")]
    [SerializeField] LauncherProjectile grenadePrefab;
    [SerializeField] LauncherProjectile flashPrefab;
    [SerializeField] Transform          launcherBarrel;
    [SerializeField] float              launchSpeed    = 22f;
    [SerializeField] int                maxGrenadeAmmo = 6;
    [SerializeField] int                maxFlashAmmo   = 3;

    [Header("Launcher Ses")]
    [SerializeField] AudioClip launcherFireClip;
    [SerializeField] [Range(0f,1f)] float launcherFireVolume = 1f;
    [SerializeField] AudioClip modeSwitchClip;
    [SerializeField] [Range(0f,1f)] float modeSwitchVolume = 0.6f;

    [Header("Silah Modelleri")]
    [Tooltip("Sıra: 0=Revolver, 1=Shotgun, 2=Lmg. Aktif olan görünür, diğerleri gizlenir.")]
    [SerializeField] GameObject[] weaponModels;
    [Tooltip("Silahı indirme süresi — model bu sürenin sonunda (en altta, görünmezken) değişir.")]
    [SerializeField] float switchHolsterTime = 0.16f;

    [Header("References")]
    [SerializeField] Camera                 playerCamera;
    [SerializeField] WeaponAnimator         weaponAnim;
    [SerializeField] ProceduralWeaponMotion weaponMotion;

    LauncherMode    mode = LauncherMode.Grenade;
    int             grenadeAmmo;
    int             flashAmmo;
    SfxPlayer       sfx;

    PlayerFirearm[] firearms;
    int             activeIndex;
    bool            switching;
    bool            weaponsHidden;

    // Silah kilidi — bölüm ilerlemesine göre GameFlow kısar. Varsayılan hepsi AÇIK
    // (GameFlow'suz sahneler eskisi gibi tüm silahlar). Revolver (0) hep açık kalır.
    readonly bool[] unlocked = { true, true, true };

    void Start()
    {
        grenadeAmmo = maxGrenadeAmmo;
        flashAmmo   = maxFlashAmmo;
        if (playerCamera == null) playerCamera = Camera.main;
        sfx = SfxPlayer.CreateOrGet(gameObject, spatialBlend: 0f);

        firearms = new PlayerFirearm[3];
        firearms[(int)Firearm.Revolver] = new PlayerFirearm("REVOLVER",
            revolverDamage, revolverDamage, revolverRange, revolverFireRate,
            1, 0f, magazineSize, startingAmmo, reloadTime, hitMask, playerCamera, muzzleFlash,
            sfx, revolverFireClip, revolverFireVolume, emptyClickClip, emptyClickVolume, reloadClip, reloadVolume);

        firearms[(int)Firearm.Shotgun] = new PlayerFirearm("SHOTGUN",
            shotgunDamageNear, shotgunDamageFar, shotgunRange, shotgunFireRate,
            shotgunPellets, shotgunSpread, shotgunMagazineSize, shotgunStartingReserve, shotgunReloadTime,
            hitMask, playerCamera, shotgunMuzzleFlash,
            sfx, shotgunFireClip, shotgunFireVolume, shotgunEmptyClickClip, shotgunEmptyClickVolume,
            shotgunReloadClip, shotgunReloadVolume);

        firearms[(int)Firearm.Lmg] = new PlayerFirearm("LMG",
            lmgDamage, lmgDamage, lmgRange, lmgFireRate,
            1, lmgSpread, lmgMagazineSize, lmgStartingReserve, lmgReloadTime,
            hitMask, playerCamera, lmgMuzzleFlash,
            sfx, lmgFireClip, lmgFireVolume, lmgEmptyClickClip, lmgEmptyClickVolume,
            lmgReloadClip, lmgReloadVolume);

        // weaponMotion atanmamışsa otomatik bul (PlayerShoot Player kökünde,
        // ProceduralWeaponMotion bir child'da) — referans kopması recoil'i öldürmesin.
        if (weaponMotion == null)
            weaponMotion = GetComponentInChildren<ProceduralWeaponMotion>(true);

        UpdateWeaponModel();                        // açılışta sadece aktif silah görünür
        Motion?.PlayDraw(ViaBottom(activeIndex));   // silah kendi yönünden çekilerek belirir
    }

    // Silahın giriş/çıkış yönü: revolver ekranın ALTINDAN iner/çekilir (kılıf hissi),
    // omuz silahları (shotgun/LMG) ÜSTTEN iner/çekilir (sırttan alma hissi).
    static bool ViaBottom(int index) => index == (int)Firearm.Revolver;

    void Update()
    {
        if (!CanShoot) return;   // silahsız (bkz. PlayerLoadout) — hiçbir silah girdisi işlenmez

        if (KeyBindings.Down(KeyBindings.Action.Weapon1)) SwitchFirearm(Firearm.Revolver);
        if (KeyBindings.Down(KeyBindings.Action.Weapon2)) SwitchFirearm(Firearm.Shotgun);
        if (KeyBindings.Down(KeyBindings.Action.Weapon3)) SwitchFirearm(Firearm.Lmg);

        if (KeyBindings.Held(KeyBindings.Action.Fire) && !switching)
            FireActive();

        // Eskiden Input.GetButtonDown("Fire2") — Input Manager'da "Fire2" varsayilani
        // Sol Alt VEYA Mouse1 (sag tik) idi (bkz. ProjectSettings/InputManager.asset),
        // KeyBindings'e hic bagli degildi (rebind edilemez). Ayni ikili davranis korundu.
        bool fire2 = (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) ||
                     (Keyboard.current != null && Keyboard.current.leftAltKey.wasPressedThisFrame);
        if (LauncherEnabled && fire2)
            FireLauncher();

        if (LauncherEnabled && KeyBindings.Down(KeyBindings.Action.LauncherMode))
            SwitchMode();

        if (KeyBindings.Down(KeyBindings.Action.Reload) && !ActiveFirearm.IsReloading)
            StartCoroutine(ActiveFirearm.Reload());
    }

    // ───────────────── Ateşli silah ─────────────────

    // Inspector referansı kopmuşsa sahnedeki tek motora (singleton) düş —
    // recoil, PlayerShoot'un nerede olduğundan bağımsız her zaman çalışsın.
    ProceduralWeaponMotion Motion =>
        weaponMotion != null ? weaponMotion : ProceduralWeaponMotion.Instance;

    // Aktif silahın tepme çarpanı
    float ActiveRecoilScale => activeIndex == (int)Firearm.Shotgun ? shotgunRecoilScale
                             : activeIndex == (int)Firearm.Lmg     ? lmgRecoilScale
                             :                                       revolverRecoilScale;

    void FireActive()
    {
        var fw     = ActiveFirearm;
        var result = fw.TryFire(DamageMultiplier);

        if (result == PlayerFirearm.FireResult.Fired)
        {
            weaponAnim?.TriggerFire();
            Motion?.ApplyRecoil(ActiveRecoilScale);
            if (fw.CurrentAmmo == 0 && fw.TotalAmmo > 0)
                StartCoroutine(fw.Reload());
        }
        else if (result == PlayerFirearm.FireResult.Empty && fw.TotalAmmo > 0)
        {
            StartCoroutine(fw.Reload());
        }
    }

    void SwitchFirearm(Firearm next)
    {
        int index = (int)next;
        if (index == activeIndex || switching) return;
        if (index >= 0 && index < unlocked.Length && !unlocked[index]) return;   // kilitli silah — yok say
        StartCoroutine(SwitchRoutine(index));
    }

    // Silah kilidi (GameFlow / WeaponPickup / PlayerLoadout çağırır). Revolver ARTIK
    // kilitlenebilir — asansör kazasında oyuncu istisnasız TÜM silahlarını kaybediyor.
    // "Revolver varsayılan olarak açık" kuralı hâlâ geçerli, ama artık unlocked[] alan
    // başlatıcısında duruyor (GameFlow'suz sahneler eskisi gibi tüm silahlarla başlar);
    // neyin kapanacağına tek merci PlayerLoadout.
    public void SetUnlocked(Firearm w, bool value)
    {
        int i = (int)w;
        if (i >= 0 && i < unlocked.Length) unlocked[i] = value;
    }

    public bool IsUnlocked(Firearm w)
    {
        int i = (int)w;
        return i < 0 || i >= unlocked.Length || unlocked[i];
    }

    // Holster (eldeki kendi yönünden çıkar) → uçta model değiştir → yenisi kendi yönünden gelir
    IEnumerator SwitchRoutine(int index)
    {
        switching = true;
        Motion?.Holster(ViaBottom(activeIndex));       // eldeki silah kendi yönünden iner
        yield return new WaitForSeconds(switchHolsterTime);
        activeIndex = index;                           // istatistik/mermi yeni silaha geçer
        UpdateWeaponModel();                           // model ekran dışındayken değişir
        Motion?.PlayDraw(ViaBottom(index));            // yeni silah kendi yönünden çekilir
        sfx?.Play(modeSwitchClip, modeSwitchVolume);   // çekiş sesi (opsiyonel klip)
        switching = false;
    }

    // Aktif silah modelini göster, diğerlerini gizle. weaponsHidden ise HİÇBİRİ görünmez
    // (eller boş) — SwitchFirearm kilitli bir silahtan ÇIKARMADIĞI için gizlemenin
    // activeIndex'ten bağımsız olması şart.
    void UpdateWeaponModel()
    {
        if (weaponModels == null) return;
        for (int i = 0; i < weaponModels.Length; i++)
            if (weaponModels[i]) weaponModels[i].SetActive(!weaponsHidden && i == activeIndex);
    }

    // ───────────────── Launcher ─────────────────

    void FireLauncher()
    {
        weaponAnim?.TriggerFireLauncher();
        sfx.Play(launcherFireClip, launcherFireVolume);
        switch (mode)
        {
            case LauncherMode.Grenade when grenadeAmmo > 0:
                grenadeAmmo--;
                SpawnProjectile(grenadePrefab);
                break;
            case LauncherMode.Flash when flashAmmo > 0:
                flashAmmo--;
                SpawnProjectile(flashPrefab);
                break;
        }
    }

    void SpawnProjectile(LauncherProjectile prefab)
    {
        // Prefab/namlu atanmamışsa sessizce çık — eksik referans oyunu patlatmasın.
        if (prefab == null || launcherBarrel == null) return;
        var proj = Instantiate(prefab, launcherBarrel.position, playerCamera.transform.rotation);
        proj.Launch(playerCamera.transform.forward * launchSpeed);
    }

    void SwitchMode()
    {
        mode = mode == LauncherMode.Grenade ? LauncherMode.Flash : LauncherMode.Grenade;
        sfx.Play(modeSwitchClip, modeSwitchVolume);
    }


    // ───────────────── Public ─────────────────

    // Silahsız mod (PlayerLoadout). enabled=false YERİNE property olmasının sebebi:
    // GameFlow.RespawnRoutine checkpoint respawn'ında ps.enabled'ı kapatıp GERİ AÇIYOR —
    // enabled'a bağlanan bir silahsızlık her ölümde sessizce bozulurdu.
    public bool CanShoot { get; set; } = true;

    // true iken elde hiçbir silah modeli görünmez (eller boş).
    public bool WeaponsHidden
    {
        get => weaponsHidden;
        set { weaponsHidden = value; UpdateWeaponModel(); }
    }

    // PlayerLoadout silahı geri verirken kullanır: SwitchFirearm'ın holster gecikmesini ve
    // `switching` kilidini atlayıp silahı doğrudan ele verir (çekiş animasyonuyla birlikte).
    public void ForceEquip(Firearm w)
    {
        if (firearms == null) return;              // Start() öncesi (sceneLoaded) çağrılabilir
        int i = (int)w;
        if (i < 0 || i >= firearms.Length) return;

        activeIndex = i;
        switching   = false;
        UpdateWeaponModel();
        Motion?.PlayDraw(ViaBottom(i));
    }

    public void AddAmmo(int amount) => ActiveFirearm.AddAmmo(amount);

    public void RefillWave()
    {
        bool grenFull  = grenadeAmmo >= maxGrenadeAmmo;
        bool flashFull = flashAmmo   >= maxFlashAmmo;

        if (!grenFull)  grenadeAmmo++;
        if (!flashFull) flashAmmo++;

        if (grenFull && flashFull) AddAmmo(5);
    }

    PlayerFirearm ActiveFirearm => firearms[activeIndex];

    public float        DamageMultiplier { get; set; } = 1f;
    public bool         LauncherEnabled  { get; set; } = true;
    public LauncherMode CurrentMode      => mode;
    public Firearm      CurrentFirearm   => (Firearm)activeIndex;
    public string       CurrentFirearmName => ActiveFirearm.displayName;
    public int  GrenadeAmmo          => grenadeAmmo;
    public int  FlashAmmo            => flashAmmo;
    public int  MaxGrenadeAmmo       => maxGrenadeAmmo;
    public int  MaxFlashAmmo         => maxFlashAmmo;
    public int  CurrentAmmo          => ActiveFirearm.CurrentAmmo;
    public int  TotalAmmo            => ActiveFirearm.TotalAmmo;
    public int  MagazineSize         => ActiveFirearm.MagazineSize;
    public bool IsReloading          => ActiveFirearm.IsReloading;
}
}
