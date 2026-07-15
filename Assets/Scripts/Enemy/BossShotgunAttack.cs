using UnityEngine;
using Bloodrush.Shared;
using Bloodrush.Shared.Audio;
using Bloodrush.FX;
using Bloodrush.Player;

namespace Bloodrush.Enemy
{
// Boss'un pompalı (hitscan koni) saldırısı: nişan süresi, görüş hattı kontrolü,
// koni/menzil kontrolü ve hasar uygulaması.
public class BossShotgunAttack
{
    readonly Transform muzzle;
    readonly float shotgunRange;
    readonly float damageNear;
    readonly float damageFar;
    readonly float coneAngle;
    readonly float aimTime;
    readonly LayerMask obstacleMask;
    readonly ParticleSystem muzzleFlash;
    readonly SfxPlayer sfx;
    readonly AudioClip fireClip;
    readonly float fireVolume;

    float aimTimer;

    public BossShotgunAttack(Transform muzzle, float shotgunRange, float damageNear, float damageFar,
        float coneAngle, float aimTime, LayerMask obstacleMask, ParticleSystem muzzleFlash,
        SfxPlayer sfx, AudioClip fireClip, float fireVolume)
    {
        this.muzzle = muzzle;
        this.shotgunRange = shotgunRange;
        this.damageNear = damageNear;
        this.damageFar = damageFar;
        this.coneAngle = coneAngle;
        this.aimTime = aimTime;
        this.obstacleMask = obstacleMask;
        this.muzzleFlash = muzzleFlash;
        this.sfx = sfx;
        this.fireClip = fireClip;
        this.fireVolume = fireVolume;
    }

    public void StartAim() => aimTimer = 0f;

    // Her frame nişan sırasında çağrılır; süre dolunca true döner.
    public bool TickAim()
    {
        aimTimer += Time.deltaTime;
        return aimTimer >= aimTime;
    }

    public bool HasLineOfSight(Transform boss, Transform player)
    {
        if (player == null) return false;
        Vector3 origin = muzzle ? muzzle.position : boss.position + Vector3.up * 1.5f;
        Vector3 target = player.position + Vector3.up * 0.5f;
        Vector3 dir    = target - origin;
        float   dist   = dir.magnitude;

        // Kendi gövdesini (Cube/Sphere) ve oyuncuyu yok say — sadece gerçek engel (duvar) LoS'u keser
        foreach (var h in Physics.RaycastAll(origin, dir.normalized, dist, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(boss)) continue;                       // kendi gövden
            if (h.collider.GetComponentInParent<PlayerMovement>() != null) continue;  // oyuncu engel değil
            return false;   // araya giren gerçek engel
        }
        return true;
    }

    public void Fire(Transform boss, Transform player, Health playerHealth)
    {
        if (muzzleFlash) { muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); muzzleFlash.Play(); }
        sfx.Play(fireClip, fireVolume);
        CameraShake.Shake(0.08f, 0.1f);

        float dist = Vector3.Distance(boss.position, player.position);
        if (dist > shotgunRange) return;

        Vector3 flat = player.position - boss.position; flat.y = 0f;
        if (Vector3.Angle(boss.forward, flat) > coneAngle) return;  // koni dışı → ıska
        if (!HasLineOfSight(boss, player)) return;                  // duvar arkası → ıska

        float dmg = Mathf.Lerp(damageNear, damageFar, Mathf.Clamp01(dist / shotgunRange));
        playerHealth?.TakeDamage(dmg);
    }
}
}
