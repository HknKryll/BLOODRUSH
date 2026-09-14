using UnityEngine;
using UnityEngine.AI;
using Bloodrush.Enemy;

namespace Bloodrush.Flow
{
// Doğan düşmanlara OverloadRoom tarafından eklenir: düşman şarj bölgesinin içinde
// kalmak istemez, dışarı doğru yürür. Bulmacanın gerilimi buradan geliyor — düşman
// kendi ayağıyla bölgeye girip ölmez, oyuncu onu KANCAYLA içeri çekip orada
// öldürmek zorunda.
//
// EnemyAI'ın İÇİNE HİÇ KARIŞMIYOR (kullanıcı kısıtı: ilgisiz/paylaşılan scriptlere
// dokunma). Sadece dışarıdan NavMeshAgent hedefi veriyor. İki incelik:
//
//  1) LateUpdate'te çalışıyor. EnemyAI.DoChase hedefi Update'te veriyor; aynı karede
//     sonra yazan kazanır. Update'te olsaydı ikisi sırayla birbirini ezip düşmanı
//     titretirdi.
//  2) Kancayla çekilirken (EnemyAI.IsBeingPulled) tamamen susuyor, ve bırakıldıktan
//     sonra da kısa bir süre (graceAfterPull) susmaya devam ediyor — oyuncuya adil bir
//     öldürme penceresi bırakmak için. Bu olmasaydı düşman bırakıldığı an bölgeden
//     fırlar, bulmaca imkânsıza yakın olurdu.
[RequireComponent(typeof(NavMeshAgent))]
public class ZoneAvoidance : MonoBehaviour
{
    [Tooltip("Bölgeden ne kadar uzağa kaçmaya çalışsın (m).")]
    [SerializeField] float fleeDistance = 9f;
    [Tooltip("Kaçış hedefi kaç saniyede bir yeniden hesaplansın.")]
    [SerializeField] float repathInterval = 0.5f;
    [Tooltip("Kancadan bırakıldıktan sonra kaç saniye kaçmasın (öldürme penceresi).")]
    [SerializeField] float graceAfterPull = 1.4f;

    ChargeZone   zone;
    NavMeshAgent agent;
    EnemyAI      ai;

    float   nextRepath;
    float   graceUntil;
    bool    wasPulled;
    Vector3 fleeTarget;
    bool    hasTarget;

    public void Configure(ChargeZone chargeZone) => zone = chargeZone;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        ai    = GetComponent<EnemyAI>();
    }

    void LateUpdate()
    {
        if (zone == null || agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        // Şarj dolduysa bulmaca bitti — düşmanlar normal davransın.
        if (zone.IsFull) return;

        bool pulled = ai != null && ai.IsBeingPulled;
        if (pulled)
        {
            wasPulled  = true;
            hasTarget  = false;
            graceUntil = Time.time + graceAfterPull;
            return;                       // oyuncu çekiyor, araya girme
        }
        if (wasPulled)
        {
            wasPulled = false;            // bırakıldı — grace zaten kuruldu
        }
        if (Time.time < graceUntil) return;

        if (!zone.Contains(transform.position + Vector3.up * 0.9f))
        {
            hasTarget = false;            // dışarıdayız, EnemyAI serbest
            return;
        }

        if (!hasTarget || Time.time >= nextRepath)
        {
            nextRepath = Time.time + repathInterval;
            hasTarget  = TryPickFleeTarget(out fleeTarget);
        }

        // Her karede SetDestination çağırmak yol hesabını her karede tetiklerdi.
        // Sadece EnemyAI hedefi ezdiyse geri al.
        if (hasTarget && (agent.destination - fleeTarget).sqrMagnitude > 0.25f)
            agent.SetDestination(fleeTarget);
    }

    bool TryPickFleeTarget(out Vector3 result)
    {
        result = transform.position;

        Vector3 away = transform.position - zone.Center;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)                       // tam merkezdeyiz
            away = new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f);
        away.Normalize();

        // Önce doğrudan dışarı; olmazsa yanlara doğru birkaç açı dene (köşeye sıkışma).
        for (int i = 0; i < 5; i++)
        {
            Vector3 dir    = Quaternion.Euler(0f, i * 55f * (i % 2 == 0 ? 1f : -1f), 0f) * away;
            Vector3 target = transform.position + dir * fleeDistance;

            if (NavMesh.SamplePosition(target, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas) &&
                !zone.Contains(hit.position + Vector3.up * 0.9f))
            {
                result = hit.position;
                return true;
            }
        }
        return false;
    }
}
}
