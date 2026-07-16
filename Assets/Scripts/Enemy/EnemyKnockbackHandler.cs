using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Bloodrush.Enemy
{
// Yumruk geri itmesi: gerçek Rigidbody fiziğiyle fırlatılır — PhysX doğal
// şekilde yere/duvara çarpıp yerleşmesini sağlar (ani "ışınlanma" yok).
public class EnemyKnockbackHandler
{
    public IEnumerator Run(Transform enemy, NavMeshAgent agent, Rigidbody rb, Vector3 dir, float force, Action onLanded)
    {
        if (agent.enabled) agent.enabled = false;
        rb.isKinematic = false;
        rb.useGravity  = true;

        float horizontalSpeed = force * 1.6f;   // eski parabolle benzer ~2.5m menzil
        float upSpeed         = Mathf.Clamp(force * 0.5f, 2f, 6f);
        rb.velocity = dir * horizontalSpeed + Vector3.up * upSpeed;

        yield return new WaitForSeconds(0.05f);   // önce yerden ayrılsın

        float t = 0f;
        while (t < 3f)
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

        // PhysX collider'lar sayesinde artık duvardan geçmiyor; sadece NavMesh
        // dışına düştüyse (ör. rampa kenarı) en yakın noktaya kelepçele.
        if (!agent.isOnNavMesh && NavMesh.SamplePosition(enemy.position, out NavMeshHit end, 2f, NavMesh.AllAreas))
            enemy.position = end.position;

        agent.enabled = true;
        onLanded?.Invoke();
    }
}
}
