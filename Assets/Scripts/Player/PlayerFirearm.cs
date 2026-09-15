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

    readonly WeaponData data;
    readonly LayerMask  hitMask;

    readonly Camera         playerCamera;
    readonly ParticleSystem muzzleFlash;
    readonly SfxPlayer      sfx;

    int   currentAmmo;
    int   totalAmmo;
    bool  isReloading;
    float nextFireTime;

    public PlayerFirearm(WeaponData data, LayerMask hitMask, Camera playerCamera,
        ParticleSystem muzzleFlash, SfxPlayer sfx)
    {
        this.data = data;
        this.displayName = data.displayName;
        this.hitMask = hitMask;
        this.playerCamera = playerCamera;
        this.muzzleFlash = muzzleFlash;
        this.sfx = sfx;

        currentAmmo = data.magazineSize;
        totalAmmo   = Mathf.Max(0, data.startingReserve - data.magazineSize);
    }

    public int   CurrentAmmo  => currentAmmo;
    public int   TotalAmmo    => totalAmmo;
    public int   MagazineSize => data.magazineSize;
    public bool  IsReloading  => isReloading;
    public float RecoilScale  => data.recoilScale;

    public void AddAmmo(int amount) => totalAmmo = Mathf.Min(totalAmmo + amount, data.startingReserve);

    public FireResult TryFire(float damageMultiplier)
    {
        if (isReloading)          return FireResult.Reloading;
        if (Time.time < nextFireTime) return FireResult.OnCooldown;

        if (currentAmmo <= 0)
        {
            sfx.Play(data.emptyClickClip, data.emptyClickVolume);
            return FireResult.Empty;
        }

        currentAmmo--;
        nextFireTime = Time.time + data.fireRate;
        sfx.Play(data.fireClip, data.fireVolume);

        if (muzzleFlash != null)
        {
            muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            muzzleFlash.Play();
        }
        CameraShake.Shake(0.04f, 0.08f);

        for (int i = 0; i < data.pelletCount; i++)
            FirePellet(damageMultiplier);

        return FireResult.Fired;
    }

    void FirePellet(float damageMultiplier)
    {
        Vector3 dir = playerCamera.transform.forward;
        if (data.spreadAngle > 0f)
            dir = Quaternion.Euler(Random.Range(-data.spreadAngle, data.spreadAngle),
                                   Random.Range(-data.spreadAngle, data.spreadAngle), 0f) * dir;

        Ray ray = new Ray(playerCamera.transform.position, dir);
        // Görünmez trigger hacimleri mermiyi emmesin (bkz. PlayerShoot.FireRevolver notu)
        if (!Physics.Raycast(ray, out RaycastHit hit, data.range, hitMask, QueryTriggerInteraction.Ignore)) return;

        LauncherProjectile proj = hit.collider.GetComponent<LauncherProjectile>();
        if (proj != null) { proj.Detonate(); return; }

        var health = hit.collider.GetComponentInParent<Health>();
        if (health == null) return;

        float dmg = Mathf.Lerp(data.damageNear, data.damageFar, Mathf.Clamp01(hit.distance / data.range)) * damageMultiplier;

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
        if (currentAmmo >= data.magazineSize || totalAmmo <= 0) yield break;
        isReloading = true;
        sfx.Play(data.reloadClip, data.reloadVolume);
        yield return new WaitForSeconds(data.reloadTime);
        int need    = data.magazineSize - currentAmmo;
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
