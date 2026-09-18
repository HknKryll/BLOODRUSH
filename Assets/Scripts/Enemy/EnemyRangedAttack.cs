using UnityEngine;
using UnityEngine.AI;
using Bloodrush.Shared.Audio;
using Bloodrush.Shared.Pooling;
using Bloodrush.Player;
using Bloodrush.FX;

namespace Bloodrush.Enemy
{
// Menzilli saldırı: mesafeye göre yaklaş/geri çekil/dur kararı, görüş hattı
// kontrolü, şarjör + yeniden dolum takibi, mermi fırlatma.
public class EnemyRangedAttack
{
    public enum ChaseMove { None, MoveTo, Stop }

    readonly float rangedRange;
    readonly int   magSize;
    readonly float fireRate;
    readonly float reloadTime;
    readonly float projectileSpeed;
    readonly float projectileDamage;
    readonly float spreadAngle;
    readonly EnemyProjectile projectilePrefab;
    readonly Transform muzzle;
    readonly SfxPlayer sfx;
    readonly AudioClip attackClip;
    readonly float attackVolume;

    int   magLeft;
    float nextShotTime;

    public EnemyRangedAttack(float rangedRange, int magSize, float fireRate, float reloadTime,
        float projectileSpeed, float projectileDamage, float spreadAngle,
        EnemyProjectile projectilePrefab, Transform muzzle,
        SfxPlayer sfx, AudioClip attackClip, float attackVolume)
    {
        this.rangedRange = rangedRange;
        this.magSize = magSize;
        this.fireRate = fireRate;
        this.reloadTime = reloadTime;
        this.projectileSpeed = projectileSpeed;
        this.projectileDamage = projectileDamage;
        this.spreadAngle = spreadAngle;
        this.projectilePrefab = projectilePrefab;
        this.muzzle = muzzle;
        this.sfx = sfx;
        this.attackClip = attackClip;
        this.attackVolume = attackVolume;
    }

    // Chase sırasında hedefe göre ne yapılacağına karar verir (caller agent.SetDestination/ResetPath çağırır).
    public ChaseMove GetChaseMove(Transform enemy, Transform player, float dist, out Vector3 destination)
    {
        destination = default;

        if (dist > rangedRange)                       // uzak → yaklaş
        {
            destination = player.position;
            return ChaseMove.MoveTo;
        }
        if (dist < rangedRange * 0.5f)                 // çok yakın → geri çekil
        {
            Vector3 away = enemy.position + (enemy.position - player.position).normalized * 4f;
            if (NavMesh.SamplePosition(away, out NavMeshHit h, 4f, NavMesh.AllAreas))
            {
                destination = h.position;
                return ChaseMove.MoveTo;
            }
            return ChaseMove.None;
        }
        return ChaseMove.Stop;                          // menzilde → dur
    }

    public void StartFiring()
    {
        magLeft      = magSize;
        nextShotTime = 0f;
    }

    // ATEŞ ANI ANİMASYONDAN GELİR (opsiyonel). Animator'da "Fire" parametresi varsa mermi
    // burada DEĞİL, klibin ateş karesindeki Animation Event'ten (FireNow) çıkar — görsel ile
    // mekanik senkron olur. Şarjör ve atış aralığı yine burada işler, yani kadans değişmez.
    //
    // GÜVENLİK AĞI: event gelmezse (klip yok, event eklenmemiş, model rig'siz) fireTimeout
    // sonunda mermi yine de atılır. Zırhlı'nın şu an modeli yok — bu olmadan ateş edemezdi.
    public bool DeferToAnimation { get; set; }
    public float FireTimeout = 0.35f;
    public bool  ShotPending { get; private set; }
    float pendingSince;

    // true dönerse bu karede bir atış BAŞLADI (çağıran taraf Fire trigger'ını atar).
    public bool TickFire(Transform enemy, Transform player)
    {
        if (ShotPending && Time.time - pendingSince >= FireTimeout)
        {
            ShotPending = false;
            FireProjectile(enemy, player);      // event gelmedi → yine de ateşle
        }

        if (Time.time < nextShotTime) return false;
        if (magLeft <= 0) magLeft = magSize;   // reload bitti, şarjör dolu

        magLeft--;
        nextShotTime = Time.time + (magLeft <= 0 ? reloadTime : fireRate);

        if (DeferToAnimation)
        {
            ShotPending  = true;
            pendingSince = Time.time;
            return true;
        }

        FireProjectile(enemy, player);
        return true;
    }

    // Animation Event çağırır (EnemyAnimEvents üzerinden).
    public void FireNow(Transform enemy, Transform player)
    {
        if (!ShotPending) return;   // sadece bekleyen atış; event fazladan gelirse mermi yağmaz
        ShotPending = false;
        FireProjectile(enemy, player);
    }

    void FireProjectile(Transform enemy, Transform player)
    {
        if (projectilePrefab == null || player == null) return;

        Vector3 origin = muzzle ? muzzle.position : enemy.position + Vector3.up * 1.4f;

        // NISAN GOVDE MERKEZINE. Eskiden `player.position + up*0.5` idi; ama oyuncunun
        // pivotu AYAKLARDAN 1.17 m yukarida (CharacterController center.y −0.17,
        // height 2), yani o nokta aslinda ayaktan 1.67 m — 2 m'lik kapsulun ust ucuna
        // yakin ve siyirmaya cok musait. Ayak + 1.0 m tam govde merkezi.
        Vector3 aim = EnemyVision.PlayerFeet(player) + Vector3.up * 1.0f;
        Vector3 dir = (aim - origin).normalized;

        if (spreadAngle > 0f)   // makineli yayılımı
            dir = Quaternion.Euler(Random.Range(-spreadAngle, spreadAngle),
                                   Random.Range(-spreadAngle, spreadAngle), 0f) * dir;

        var proj = PoolManager.Get(projectilePrefab, origin, Quaternion.LookRotation(dir));
        proj.Launch(dir, projectileSpeed, projectileDamage, enemy);
        sfx.Play(attackClip, attackVolume);
        MuzzleFlash.Spawn(origin, muzzle);   // görünür ateş flaşı (namluda çakar)
    }

    // Görüş hattı EnemyVision'a taşındı — yakın dövüş de aynı kontrole ihtiyaç duyuyor,
    // iki kopya tutmak yerine tek kaynak. Davranış aynı: bel/göğüs/baş, üçü de açık olmalı.
    public bool HasLineOfSight(Transform enemy, Transform player)
    {
        if (player == null) return false;
        Vector3 origin = muzzle ? muzzle.position : enemy.position + Vector3.up * 1.4f;
        return EnemyVision.ClearToPlayer(origin, player, enemy, EnemyVision.TorsoHeights);
    }
}
}
