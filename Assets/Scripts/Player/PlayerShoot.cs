using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Bloodrush.Flow;
using Bloodrush.Weapons;
using Bloodrush.Shared.Audio;
using Bloodrush.Shared.Pooling;

namespace Bloodrush.Player
{
public class PlayerShoot : MonoBehaviour
{
    public enum LauncherMode { Grenade, Flash }
    public enum Firearm { Revolver, Shotgun, Lmg }

    [Header("Silah Verisi")]
    [Tooltip("Hasar/menzil/ateş hızı/şarjör/ses değerleri artık burada değil — " +
             "Assets/Scripts/Weapons/Data/WeaponData.cs asset'lerinde (bkz. WeaponData_Revolver/Shotgun/LMG).")]
    [SerializeField] WeaponData revolverData;
    [SerializeField] WeaponData shotgunData;
    [SerializeField] WeaponData lmgData;

    [Header("Ortak")]
    [SerializeField] LayerMask hitMask = ~0;
    [SerializeField] ParticleSystem muzzleFlash;
    [SerializeField] ParticleSystem shotgunMuzzleFlash;
    [SerializeField] ParticleSystem lmgMuzzleFlash;

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
        firearms[(int)Firearm.Revolver] = new PlayerFirearm(revolverData, hitMask, playerCamera, muzzleFlash, sfx);
        firearms[(int)Firearm.Shotgun]  = new PlayerFirearm(shotgunData, hitMask, playerCamera, shotgunMuzzleFlash, sfx);
        firearms[(int)Firearm.Lmg]      = new PlayerFirearm(lmgData, hitMask, playerCamera, lmgMuzzleFlash, sfx);

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
    float ActiveRecoilScale => ActiveFirearm.RecoilScale;

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
        var proj = PoolManager.Get(prefab, launcherBarrel.position, playerCamera.transform.rotation);
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
