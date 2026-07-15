using UnityEngine;
using Bloodrush.Shared;
using Bloodrush.Shared.Audio;
using Bloodrush.Player;

namespace Bloodrush.Enemy
{
// Yakın dövüş saldırısı: menzil kontrolü, telegraph (önceden haber verme)
// süresi ve gösterge, hasar + oyuncu geri itme uygulaması.
public class EnemyMeleeAttack
{
    readonly float attackRange;
    readonly float attackDamage;
    readonly float attackCooldown;
    readonly float telegraphDuration;
    readonly float playerKnockback;
    readonly GameObject attackIndicator;
    readonly SfxPlayer sfx;
    readonly AudioClip attackClip;
    readonly float attackVolume;

    float lastAttackTime;
    float telegraphStartTime;
    float activeTelegraphDuration;

    public EnemyMeleeAttack(float attackRange, float attackDamage, float attackCooldown, float telegraphDuration,
        float playerKnockback, GameObject attackIndicator, SfxPlayer sfx, AudioClip attackClip, float attackVolume)
    {
        this.attackRange = attackRange;
        this.attackDamage = attackDamage;
        this.attackCooldown = attackCooldown;
        this.telegraphDuration = telegraphDuration;
        this.playerKnockback = playerKnockback;
        this.attackIndicator = attackIndicator;
        this.sfx = sfx;
        this.attackClip = attackClip;
        this.attackVolume = attackVolume;
    }

    public bool InRange(float dist)    => dist <= attackRange;
    public bool OutOfRange(float dist) => dist > attackRange * 1.3f;

    // Chase -> Attack geçişinde çağrılır.
    public void EnterAttack() => lastAttackTime = Time.time;

    public bool ReadyToTelegraph() => Time.time >= lastAttackTime + attackCooldown;

    public void StartTelegraph()
    {
        telegraphStartTime      = Time.time;
        activeTelegraphDuration = telegraphDuration;
        if (attackIndicator)
        {
            attackIndicator.SetActive(true);
            foreach (var ps in attackIndicator.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
                activeTelegraphDuration = ps.main.duration; // particle'ın kendi süresiyle eşleş
            }
        }
    }

    public bool TickTelegraph() => Time.time >= telegraphStartTime + activeTelegraphDuration;

    public void ResolveTelegraph(Transform enemy, Transform player, PlayerMovement playerMovement)
    {
        lastAttackTime = Time.time;
        player.GetComponent<Health>()?.TakeDamage(attackDamage);

        // Büyük düşman: vurunca oyuncuyu geri it
        if (playerKnockback > 0f && playerMovement != null)
        {
            Vector3 away = player.position - enemy.position; away.y = 0f;
            playerMovement.Launch(away.normalized * playerKnockback + Vector3.up * 2f);
        }

        sfx.Play(attackClip, attackVolume);
    }

    public void HideIndicator()
    {
        if (!attackIndicator) return;
        foreach (var ps in attackIndicator.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        attackIndicator.SetActive(false);
    }
}
}
