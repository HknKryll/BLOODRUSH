using System.Collections;
using UnityEngine;
using Bloodrush.Shared;
using Bloodrush.Flow;
using Bloodrush.Enemy;
using Bloodrush.Weapons;
using Bloodrush.UI;
using Bloodrush.FX;

namespace Bloodrush.Player
{
public class PlayerShoot : MonoBehaviour
{
    public enum LauncherMode { Grenade, Flash, Anchor }

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

    [Header("Launcher")]
    [SerializeField] LauncherProjectile grenadePrefab;
    [SerializeField] LauncherProjectile flashPrefab;
    [SerializeField] Transform          launcherBarrel;
    [SerializeField] float              launchSpeed    = 22f;
    [SerializeField] int                maxGrenadeAmmo = 6;
    [SerializeField] int                maxFlashAmmo   = 3;

    [Header("Anchor")]
    [SerializeField] GameObject anchorPrefab;
    [SerializeField] float      anchorThrowSpeed = 30f;

    [Header("Ses")]
    [SerializeField] AudioClip revolverFireClip;
    [SerializeField] [Range(0f,1f)] float revolverFireVolume = 1f;
    [SerializeField] AudioClip emptyClickClip;
    [SerializeField] [Range(0f,1f)] float emptyClickVolume = 0.6f;
    [SerializeField] AudioClip reloadClip;
    [SerializeField] [Range(0f,1f)] float reloadVolume = 0.8f;
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
    int             currentAmmo;
    int             totalAmmo;
    bool            isReloading;
    float           nextFireTime;
    ThrowableAnchor activeAnchor;
    AudioSource     audioSrc;

    void Start()
    {
        currentAmmo = magazineSize;
        totalAmmo   = startingAmmo - magazineSize; // ilk şarjör zaten silahta
        grenadeAmmo = maxGrenadeAmmo;
        flashAmmo   = maxFlashAmmo;
        if (playerCamera == null) playerCamera = Camera.main;
        audioSrc = GetComponent<AudioSource>();
        if (audioSrc == null) audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake  = false;
        audioSrc.spatialBlend = 0f;
    }

    void Update()
    {
        if (Input.GetMouseButton(0) && Time.time >= nextFireTime && !isReloading)
            FireRevolver();

        if (LauncherEnabled && Input.GetButtonDown("Fire2"))
            FireLauncher();

        if (LauncherEnabled && Input.GetKeyDown(KeyCode.Q))
            SwitchMode();

        if (Input.GetKeyDown(KeyCode.R) && !isReloading)
            StartCoroutine(Reload());
    }

    // ───────────────── Revolver ─────────────────

    void FireRevolver()
    {
        if (currentAmmo <= 0)
        {
            if (totalAmmo > 0) StartCoroutine(Reload());
            else Play(emptyClickClip, emptyClickVolume);
            return;
        }

        currentAmmo--;
        nextFireTime = Time.time + revolverFireRate;
        Play(revolverFireClip, revolverFireVolume);
        weaponAnim?.TriggerFire();
        weaponMotion?.ApplyRecoil();
        CameraShake.Shake(0.04f, 0.08f);

        if (muzzleFlash != null)
        {
            muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            muzzleFlash.Play();
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        // Görünmez trigger hacimleri (Arena, TutorialHint, LevelExit) mermiyi emmesin
        if (!Physics.Raycast(ray, out RaycastHit hit, revolverRange, hitMask, QueryTriggerInteraction.Ignore)) return;

        LauncherProjectile proj = hit.collider.GetComponent<LauncherProjectile>();
        if (proj != null) { proj.Detonate(); return; }

        var health = hit.collider.GetComponentInParent<Health>();
        if (health != null)
        {
            float dmg = revolverDamage * DamageMultiplier;
            var armor = hit.collider.GetComponentInParent<DirectionalArmor>();
            if (armor != null) dmg *= armor.Multiplier(ray.direction);  // önden zırh emer

            health.TakeDamage(dmg);
            CrosshairHUD.Instance?.ShowHitMarker();
            CameraShake.Shake(0.08f, 0.12f);
            SpawnHitEffect(hit.point, hit.normal);
            SpawnBloodEffect(hit.point, hit.normal);
        }

        if (currentAmmo == 0 && totalAmmo > 0)
            StartCoroutine(Reload());
    }

    IEnumerator Reload()
    {
        if (currentAmmo >= magazineSize || totalAmmo <= 0) yield break;
        isReloading = true;
        Play(reloadClip, reloadVolume);
        yield return new WaitForSeconds(reloadTime);
        int need    = magazineSize - currentAmmo;
        int take    = Mathf.Min(need, totalAmmo);
        currentAmmo += take;
        totalAmmo   -= take;
        isReloading  = false;
    }

    void SpawnHitEffect(Vector3 point, Vector3 normal)
    {
        var go = new GameObject("HitFX");
        go.transform.position = point;
        go.transform.rotation = Quaternion.LookRotation(normal);
        go.AddComponent<HitEffect>();
    }

    void SpawnBloodEffect(Vector3 point, Vector3 normal)
    {
        var go = new GameObject("BloodFX");
        go.transform.position = point;
        go.transform.rotation = Quaternion.LookRotation(normal);
        go.AddComponent<BloodEffect>();
    }

    // ───────────────── Launcher ─────────────────

    void FireLauncher()
    {
        weaponAnim?.TriggerFireLauncher();
        Play(launcherFireClip, launcherFireVolume);
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
            case LauncherMode.Anchor:
                ThrowAnchor();
                break;
        }
    }

    void ThrowAnchor()
    {
        if (anchorPrefab == null) return;
        if (activeAnchor != null) Destroy(activeAnchor.gameObject);
        var go = Instantiate(anchorPrefab, launcherBarrel.position, playerCamera.transform.rotation);
        var rb = go.GetComponent<Rigidbody>();
        if (rb) rb.velocity = playerCamera.transform.forward * anchorThrowSpeed;
        activeAnchor = go.GetComponent<ThrowableAnchor>();
    }

    void SpawnProjectile(LauncherProjectile prefab)
    {
        var proj = Instantiate(prefab, launcherBarrel.position, playerCamera.transform.rotation);
        proj.Launch(playerCamera.transform.forward * launchSpeed);
    }

    void SwitchMode()
    {
        mode = mode switch
        {
            LauncherMode.Grenade => LauncherMode.Flash,
            LauncherMode.Flash   => LauncherMode.Anchor,
            _                    => LauncherMode.Grenade,
        };
        Play(modeSwitchClip, modeSwitchVolume);
    }

    void Play(AudioClip clip, float vol = 1f) { if (clip) audioSrc.PlayOneShot(clip, vol); }

    // ───────────────── Public ─────────────────

    public void AddAmmo(int amount)
    {
        totalAmmo = Mathf.Min(totalAmmo + amount, startingAmmo);
    }

    public void RefillWave()
    {
        bool grenFull  = grenadeAmmo >= maxGrenadeAmmo;
        bool flashFull = flashAmmo   >= maxFlashAmmo;

        if (!grenFull)  grenadeAmmo++;
        if (!flashFull) flashAmmo++;

        if (grenFull && flashFull) AddAmmo(5);
    }

    public float        DamageMultiplier { get; set; } = 1f;
    public bool         LauncherEnabled  { get; set; } = true;
    public LauncherMode CurrentMode  => mode;
    public int  GrenadeAmmo          => grenadeAmmo;
    public int  FlashAmmo            => flashAmmo;
    public int  MaxGrenadeAmmo       => maxGrenadeAmmo;
    public int  MaxFlashAmmo         => maxFlashAmmo;
    public int  CurrentAmmo          => currentAmmo;
    public int  TotalAmmo            => totalAmmo;
    public int  MagazineSize         => magazineSize;
    public bool IsReloading          => isReloading;
}
}
