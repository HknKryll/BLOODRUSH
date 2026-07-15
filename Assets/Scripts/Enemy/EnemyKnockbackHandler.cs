using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Bloodrush.Enemy
{
// Yumruk geri itmesi: zeminde kısa, duvar-farkında parabolik bir kayma
// (duvardan geçmez, NavMesh üzerinde iner).
public class EnemyKnockbackHandler
{
    public IEnumerator Run(Transform enemy, NavMeshAgent agent, Vector3 dir, float force, Action onLanded)
    {
        if (agent.enabled) agent.enabled = false;

        // Yatay fırlatma mesafesi — eğlenceli, force 7 → ~2.5m
        float distance = force * 0.35f;

        // Duvar kontrolü — yolda engel varsa mesafeyi kırp (kendi yarıçapının ötesinden başla)
        Vector3 origin = enemy.position + Vector3.up * 0.6f + dir * 0.5f;
        if (Physics.Raycast(origin, dir, out RaycastHit wall, distance, ~0, QueryTriggerInteraction.Ignore))
            distance = Mathf.Max(0f, wall.distance);

        Vector3 start = enemy.position;
        Vector3 land  = start + dir * distance;

        // İniş noktasını NavMesh'e kelepçele (duvar arkası yürünmez alana taşmasın)
        if (NavMesh.SamplePosition(land, out NavMeshHit nav, 1.5f, NavMesh.AllAreas))
            land = nav.position;

        // Yay: yatay start→land + dikey parabol (0 → tepe → 0)
        float height = Mathf.Clamp(force * 0.12f, 0.4f, 1.5f);
        const float dur = 0.35f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            Vector3 pos = Vector3.Lerp(start, land, p);
            pos.y += height * 4f * p * (1f - p);   // parabolik yükseklik
            enemy.position = pos;
            yield return null;
        }

        // Zemine/NavMesh'e otur ve agent'ı geri aç
        if (NavMesh.SamplePosition(enemy.position, out NavMeshHit end, 1.5f, NavMesh.AllAreas))
            enemy.position = end.position;
        agent.enabled = true;
        onLanded?.Invoke();
    }
}
}
