using UnityEngine;
using Bloodrush.Shared;
using Bloodrush.Shared.Audio;
using Bloodrush.FX;
using Bloodrush.Player;

namespace Bloodrush.Enemy
{
// Boss'un kabza (yakın dövüş, parry'lenebilir) saldırısı: telegraph süresi,
// menzil kontrolü, hasar/knockback uygulaması ve cooldown takibi.
public class BossMeleeAttack
{
    readonly float meleeRange;
    readonly float meleeDamage;
    readonly float meleeKnockback;
    readonly float meleeTelegraph;
    readonly float meleeCooldown;
    readonly SfxPlayer sfx;
    readonly AudioClip meleeClip;
    readonly float meleeVolume;

    float telegraphTimer;

    public float ReadyTime { get; private set; }

    public BossMeleeAttack(float meleeRange, float meleeDamage, float meleeKnockback, float meleeTelegraph,
        float meleeCooldown, SfxPlayer sfx, AudioClip meleeClip, float meleeVolume)
    {
        this.meleeRange = meleeRange;
        this.meleeDamage = meleeDamage;
        this.meleeKnockback = meleeKnockback;
        this.meleeTelegraph = meleeTelegraph;
        this.meleeCooldown = meleeCooldown;
        this.sfx = sfx;
        this.meleeClip = meleeClip;
        this.meleeVolume = meleeVolume;
    }

    public void StartTelegraph() => telegraphTimer = 0f;

    // Her frame telegraph sırasında çağrılır; süre dolunca true döner.
    public bool TickTelegraph()
    {
        telegraphTimer += Time.deltaTime;
        return telegraphTimer >= meleeTelegraph;
    }

    // Telegraph süresi dolduğunda çağrılır; hâlâ menzildeyse hasar+knockback uygular
    // ve her durumda cooldown'u başlatır.
    public void ResolveTelegraph(Transform boss, Transform player, Health playerHealth, PlayerMovement playerMovement)
    {
        if (Vector3.Distance(boss.position, player.position) <= meleeRange * 1.4f)
        {
            playerHealth?.TakeDamage(meleeDamage);
            if (playerMovement != null)
            {
                Vector3 away = player.position - boss.position; away.y = 0f;
                playerMovement.Launch(away.normalized * meleeKnockback + Vector3.up * 3f);
            }
            sfx.Play(meleeClip, meleeVolume);
            CameraShake.Shake(0.3f, 0.2f);
        }
        ReadyTime = Time.time + meleeCooldown;
    }
}
}
