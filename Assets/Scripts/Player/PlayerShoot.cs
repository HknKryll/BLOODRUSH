using System.Collections;
using UnityEngine;
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
    [SerializeField] float revolverDamage  = 40f;
    [SerializeField] float revolverRange   = 120f;
    [SerializeField] float revolverFireRate = 0.28f;
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
    [SerializeField] ParticleSystem lmgMuzzleFlash;

    [Header("LMG Ses")]
    [SerializeField] AudioClip lmgFireClip;
    [SerializeField] [Range(0f,1f)] float lmgFireVolume = 0.9f;
    [SerializeField] AudioClip lmgEmptyClickClip;
    [SerializeField] [Range(0f,1f)] float lmgEmptyClickVolume = 0.6f;
    [SerializeField] AudioClip lmgReloadClip;
    [SerializeField] [Range(0f,1f)] float lmgReloadVolume = 0.8f;

    [Header("Silah Değiştirme")]
    [SerializeField] KeyCode switchToRevolverKey = KeyCode.Alpha1;
    [SerializeField] KeyCode switchToShotgunKey  = KeyCode.Alpha2;
    [SerializeField] KeyCode switchToLmgKey      = KeyCode.Alpha3;

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
    }

    void Update()
    {
        if (Input.GetKeyDown(switchToRevolverKey)) SwitchFirearm(Firearm.Revolver);
        if (Input.GetKeyDown(switchToShotgunKey))  SwitchFirearm(Firearm.Shotgun);
        if (Input.GetKeyDown(switchToLmgKey))      SwitchFirearm(Firearm.Lmg);

        if (Input.GetMouseButton(0))
            FireActive();

        if (LauncherEnabled && Input.GetButtonDown("Fire2"))
            FireLauncher();

        if (LauncherEnabled && Input.GetKeyDown(KeyCode.Q))
            SwitchMode();

        if (Input.GetKeyDown(KeyCode.R) && !ActiveFirearm.IsReloading)
            StartCoroutine(ActiveFirearm.Reload());
    }

    // ───────────────── Ateşli silah ─────────────────

    void FireActive()
    {
        var fw     = ActiveFirearm;
        var result = fw.TryFire(DamageMultiplier);

        if (result == PlayerFirearm.FireResult.Fired)
        {
            weaponAnim?.TriggerFire();
            weaponMotion?.ApplyRecoil();
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
        if (index == activeIndex) return;
        activeIndex = index;
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
        var proj = Instantiate(prefab, launcherBarrel.position, playerCamera.transform.rotation);
        proj.Launch(playerCamera.transform.forward * launchSpeed);
    }

    void SwitchMode()
    {
        mode = mode == LauncherMode.Grenade ? LauncherMode.Flash : LauncherMode.Grenade;
        sfx.Play(modeSwitchClip, modeSwitchVolume);
    }


    // ───────────────── Public ─────────────────

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
