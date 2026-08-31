using System.Collections;
using UnityEngine;
using Bloodrush.Shared;
using Bloodrush.Shared.Audio;
using Bloodrush.Enemy;
using Bloodrush.Weapons;
using Bloodrush.UI;
using Bloodrush.FX;

// Tek bir ateşli silahın (Revolver/Tabanca, Shotgun, LMG) hitscan mantığını
// taşıyan yeniden kullanılabilir sınıf — MonoBehaviour DEĞİL, EnemyRangedAttack
// ile aynı composition deseni (bkz. Assets/Scripts/Enemy/EnemyRangedAttack.cs).
// PlayerShoot her silah için bir örnek tutar, aktif olanı Update'te besler.
//
// Not: Shotgun/LMG için henüz ayrı silah görseli/animasyonu yok — bu sınıf
// tamamen mantık katmanı, viewmodel gelene kadar mevcut silah mesh'i/animasyonu
// (WeaponAnimator "Fire" trigger'ı) paylaşılabilir.
namespace Bloodrush.Player
{
public class PlayerFirearm
{
    public enum FireResult { Fired, Empty, OnCooldown, Reloading }

    public readonly string displayName;

    readonly float damageNear;
    readonly float damageFar;
    readonly float range;
    readonly float fireRate;
    readonly int   pelletCount;
    readonly float spreadAngle;
    readonly int   magazineSize;
    readonly float reloadTime;
    readonly int   reserveCap;
    readonly LayerMask hitMask;

    readonly Camera         playerCamera;
    readonly ParticleSystem muzzleFlash;
    readonly SfxPlayer      sfx;
    readonly AudioClip      fireClip;
    readonly float          fireVolume;
    readonly AudioClip      emptyClickClip;
    readonly float          emptyClickVolume;
    readonly AudioClip      reloadClip;
    readonly float          reloadVolume;

    int   currentAmmo;
    int   totalAmmo;
    bool  isReloading;
    float nextFireTime;

    public PlayerFirearm(string displayName, float damageNear, float damageFar, float range, float fireRate,
        int pelletCount, float spreadAngle, int magazineSize, int startingReserve, float reloadTime,
        LayerMask hitMask, Camera playerCamera, ParticleSystem muzzleFlash,
        SfxPlayer sfx, AudioClip fireClip, float fireVolume,
        AudioClip emptyClickClip, float emptyClickVolume, AudioClip reloadClip, float reloadVolume)
    {
        this.displayName = displayName;
        this.damageNear = damageNear;
        this.damageFar = damageFar;
        this.range = range;
        this.fireRate = fireRate;
        this.pelletCount = Mathf.Max(1, pelletCount);
        this.spreadAngle = spreadAngle;
        this.magazineSize = magazineSize;
        this.reloadTime = reloadTime;
        this.reserveCap = startingReserve;
        this.hitMask = hitMask;
        this.playerCamera = playerCamera;
        this.muzzleFlash = muzzleFlash;
        this.sfx = sfx;
        this.fireClip = fireClip;
        this.fireVolume = fireVolume;
        this.emptyClickClip = emptyClickClip;
        this.emptyClickVolume = emptyClickVolume;
        this.reloadClip = reloadClip;
        this.reloadVolume = reloadVolume;

        currentAmmo = magazineSize;
        totalAmmo   = Mathf.Max(0, startingReserve - magazineSize);
    }

    public int  CurrentAmmo  => currentAmmo;
    public int  TotalAmmo    => totalAmmo;
    public int  MagazineSize => magazineSize;
    public bool IsReloading  => isReloading;

    public void AddAmmo(int amount) => totalAmmo = Mathf.Min(totalAmmo + amount, reserveCap);

    public FireResult TryFire(float damageMultiplier)
    {
        if (isReloading)          return FireResult.Reloading;
        if (Time.time < nextFireTime) return FireResult.OnCooldown;

        if (currentAmmo <= 0)
        {
            sfx.Play(emptyClickClip, emptyClickVolume);
            return FireResult.Empty;
        }

        currentAmmo--;
        nextFireTime = Time.time + fireRate;
        sfx.Play(fireClip, fireVolume);

        if (muzzleFlash != null)
        {
            muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            muzzleFlash.Play();
        }
        CameraShake.Shake(0.04f, 0.08f);

        for (int i = 0; i < pelletCount; i++)
            FirePellet(damageMultiplier);

        return FireResult.Fired;
    }

    void FirePellet(float damageMultiplier)
    {
        Vector3 dir = playerCamera.transform.forward;
        if (spreadAngle > 0f)
            dir = Quaternion.Euler(Random.Range(-spreadAngle, spreadAngle),
                                   Random.Range(-spreadAngle, spreadAngle), 0f) * dir;

        Ray ray = new Ray(playerCamera.transform.position, dir);
        // Görünmez trigger hacimleri mermiyi emmesin (bkz. PlayerShoot.FireRevolver notu)
        if (!Physics.Raycast(ray, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore)) return;

        LauncherProjectile proj = hit.collider.GetComponent<LauncherProjectile>();
        if (proj != null) { proj.Detonate(); return; }

        var health = hit.collider.GetComponentInParent<Health>();
        if (health == null) return;

        float dmg = Mathf.Lerp(damageNear, damageFar, Mathf.Clamp01(hit.distance / range)) * damageMultiplier;

        // Yönlü zırh (önden az hasar / arkadan tam) kaldırıldı — zırhlı düşman
        // artık her yönden tam hasar alır. DirectionalArmor component'i ArmoredHazard
        // prefab'ında atıl duruyor; istenirse elle silinebilir.

        health.TakeDamage(dmg);
        CrosshairHUD.Instance?.ShowHitMarker();
        CameraShake.Shake(0.08f, 0.12f);
        SpawnHitEffect(hit.point, hit.normal);
        SpawnBloodEffect(hit.point, hit.normal);
    }

    public IEnumerator Reload()
    {
        if (currentAmmo >= magazineSize || totalAmmo <= 0) yield break;
        isReloading = true;
        sfx.Play(reloadClip, reloadVolume);
        yield return new WaitForSeconds(reloadTime);
        int need    = magazineSize - currentAmmo;
        int take    = Mathf.Min(need, totalAmmo);
        currentAmmo += take;
        totalAmmo   -= take;
        isReloading  = false;
    }

    static void SpawnHitEffect(Vector3 point, Vector3 normal)
    {
        var go = new GameObject("HitFX");
        go.transform.position = point;
        go.transform.rotation = Quaternion.LookRotation(normal);
        go.AddComponent<HitEffect>();
    }

    static void SpawnBloodEffect(Vector3 point, Vector3 normal)
    {
        var go = new GameObject("BloodFX");
        go.transform.position = point;
        go.transform.rotation = Quaternion.LookRotation(normal);
        go.AddComponent<BloodEffect>();
    }
}
}
