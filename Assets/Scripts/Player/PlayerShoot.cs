using System.Collections;
using UnityEngine;

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
    [SerializeField] float reloadTime   = 1.2f;

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

    void Start()
    {
        currentAmmo = magazineSize;
        totalAmmo   = startingAmmo - magazineSize; // ilk şarjör zaten silahta
        grenadeAmmo = maxGrenadeAmmo;
        flashAmmo   = maxFlashAmmo;
        if (playerCamera == null) playerCamera = Camera.main;
    }

    void Update()
    {
        if (Input.GetMouseButton(0) && Time.time >= nextFireTime && !isReloading)
            FireRevolver();

        if (Input.GetButtonDown("Fire2"))
            FireLauncher();

        if (Input.GetKeyDown(KeyCode.Q))
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
            return;
        }

        currentAmmo--;
        nextFireTime = Time.time + revolverFireRate;
        weaponAnim?.TriggerFire();
        weaponMotion?.ApplyRecoil();
        CameraShake.Shake(0.04f, 0.08f);

        if (muzzleFlash != null)
        {
            muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            muzzleFlash.Play();
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, revolverRange, hitMask)) return;

        LauncherProjectile proj = hit.collider.GetComponent<LauncherProjectile>();
        if (proj != null) { proj.Detonate(); return; }

        var health = hit.collider.GetComponentInParent<Health>();
        if (health != null)
        {
            health.TakeDamage(revolverDamage);
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
    }

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
