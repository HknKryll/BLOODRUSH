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
    readonly float maxVerticalReach;
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

    public EnemyMeleeAttack(float attackRange, float maxVerticalReach, float attackDamage, float attackCooldown,
        float telegraphDuration, float playerKnockback, GameObject attackIndicator, SfxPlayer sfx,
        AudioClip attackClip, float attackVolume)
    {
        this.attackRange = attackRange;
        this.maxVerticalReach = maxVerticalReach;
        this.attackDamage = attackDamage;
        this.attackCooldown = attackCooldown;
        this.telegraphDuration = telegraphDuration;
        this.playerKnockback = playerKnockback;
        this.attackIndicator = attackIndicator;
        this.sfx = sfx;
        this.attackClip = attackClip;
        this.attackVolume = attackVolume;
    }

    // MENZİL ARTIK HAM 3B MESAFE DEĞİL: yatay mesafe + ayrı bir dikey sınır.
    //
    // Eskiden `dist <= attackRange` idi ve `dist` Vector3.Distance'tan geliyordu — yani
    // "4 m yukarıda" ile "4 m ileride" aynı sayılıyordu. CH3 arenasında kattan kata
    // mesafe TAM 4.00 m (zemin y 0.00, platform üstü y 4.00) ve Büyük_Düşman'ın menzili
    // 5; sonuç olarak platformun üstünde duran düşman hiç kıpırdamadan aşağıdaki oyuncuyu
    // "menzilde" sayıyor, EnemyAI.DoAttack'teki agent.ResetPath() ile oraya çakılıyor ve
    // zeminin içinden vuruyordu. Küçük düşman (menzil 3.01) ise oyuncu zıplayınca
    // (zıplama tepesi 2.82 m → mesafe 1.18 m) aynı şeyi yapıyordu.
    // DİKEY FARK AYAK–AYAK ÖLÇÜLÜR, PİVOT–PİVOT DEĞİL. Oyuncunun pivotu ayaklarından
    // 1.17 m yukarıda (CharacterController center.y −0.17, height 2), düşmanınki tam
    // ayağında (NavMeshAgent baseOffset 0). Pivotları doğrudan çıkarınca DÜMDÜZ ZEMİNDE
    // bile 1.17 m'lik sahte bir kot farkı çıkıyor ve eşiği aşıp bütün yakın dövüşü
    // kilitliyordu (düşmanlar sadece itip duruyordu). Bkz. EnemyVision.PlayerFeet.
    public bool InRange(Transform enemy, Transform player)
    {
        if (enemy == null || player == null) return false;
        Vector3 d = EnemyVision.PlayerFeet(player) - enemy.position;
        if (Mathf.Abs(d.y) > maxVerticalReach) return false;   // kat farkı — yumruk yetişmez
        d.y = 0f;
        return d.magnitude <= attackRange;
    }

    public bool OutOfRange(Transform enemy, Transform player)
    {
        if (enemy == null || player == null) return true;
        Vector3 d = EnemyVision.PlayerFeet(player) - enemy.position;
        if (Mathf.Abs(d.y) > maxVerticalReach * 1.3f) return true;
        d.y = 0f;
        return d.magnitude > attackRange * 1.3f;
    }

    // Chase -> Attack geçişinde çağrılır. NOT: eskiden burada lastAttackTime = Time.time
    // yapılıyordu — bu, menzile girince tam attackCooldown kadar BEKLEME + her yeniden
    // girişte sıfırlama demekti (koş→dur→koş salınımı, geç vuruş). Kaldırıldı: ilk vuruş
    // menzile girer girmez tetiklenir; cooldown yalnız ResolveTelegraph'tan (gerçek
    // vuruştan) sayılır, böylece saldırı akıcı ve hızlı olur.
    public void EnterAttack() { }

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

        // SON KONTROL: telegraph boyunca oyuncu siper arkasına geçmiş olabilir. Eskiden
        // burada hiçbir görüş hattı kontrolü YOKTU — düşman duvarın/zeminin ötesinden
        // hasar veriyordu. Engel varsa vuruş boşa gider (istenen davranış: siper almak
        // saldırıdan kurtarır), cooldown yine de işler.
        if (!EnemyVision.ClearForMelee(enemy, player))
        {
            sfx.Play(attackClip, attackVolume);   // savurma sesi duyulsun, hasar yok
            return;
        }

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
