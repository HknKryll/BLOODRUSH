using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Sıçrayıcı (STORY_DESIGN.md Bölüm 8, Tip 2) traversal'ı: chase sırasında orta
// menzilde oyuncuya doğru güçlü bir sıçrayış yapar — köşeye sıkıştırma hissi
// verir, oyuncuyu kanca/pozisyon değiştirmeye zorlar. Gerçek fizikle (PhysX)
// fırlatılır; iniş mantığı EnemyKnockbackHandler ile aynı desendedir.
//
// Görsel not: bu davranış tamamen fizik tabanlı çalışır, özel bir "zıplama"
// animasyonuna bağımlı değil — model/animasyon gelene kadar mevcut mesh'lerle
// (örn. HighSpeedEnemy) placeholder olarak kullanılabilir.
namespace Bloodrush.Enemy
{
public class EnemyLeapBehavior
{
    readonly float leapRangeMin;
    readonly float leapRangeMax;
    readonly float leapCooldown;
    readonly float leapSpeed;
    readonly float leapArcHeight;

    float nextLeapTime;

    public EnemyLeapBehavior(float leapRangeMin, float leapRangeMax, float leapCooldown, float leapSpeed, float leapArcHeight)
    {
        this.leapRangeMin  = leapRangeMin;
        this.leapRangeMax  = leapRangeMax;
        this.leapCooldown  = leapCooldown;
        this.leapSpeed     = leapSpeed;
        this.leapArcHeight = leapArcHeight;
    }

    public bool ReadyToLeap(float dist) =>
        Time.time >= nextLeapTime && dist >= leapRangeMin && dist <= leapRangeMax;

    public IEnumerator Run(Transform enemy, NavMeshAgent agent, Rigidbody rb, Vector3 targetPos, Action onLanded)
    {
        nextLeapTime = Time.time + leapCooldown;

        if (agent.enabled) agent.enabled = false;
        rb.isKinematic = false;
        rb.useGravity  = true;

        Vector3 dir = targetPos - enemy.position; dir.y = 0f;
        rb.velocity = dir.normalized * leapSpeed + Vector3.up * leapArcHeight;

        yield return new WaitForSeconds(0.05f);   // önce yerden ayrılsın

        float t = 0f;
        while (t < 2.5f)
        {
            t += Time.deltaTime;
            bool grounded = Physics.Raycast(enemy.position + Vector3.up * 0.1f, Vector3.down, 0.3f,
                                             ~0, QueryTriggerInteraction.Ignore);
            if (grounded && rb.velocity.magnitude < 0.3f) break;
            yield return null;
        }

        rb.velocity    = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity  = false;

        if (!agent.isOnNavMesh && NavMesh.SamplePosition(enemy.position, out NavMeshHit end, 2f, NavMesh.AllAreas))
            enemy.position = end.position;

        agent.enabled = true;
        onLanded?.Invoke();
    }
}
}
