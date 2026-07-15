using UnityEngine;
using UnityEngine.AI;

namespace Bloodrush.Enemy
{
// Devriye noktaları arası dolaşma + oyuncu görüş kontrolü (raycast tabanlı,
// aralıklı örneklenir). EnemyAI'ın Patrol durumunda kullanılır.
public class EnemyPatrolBehavior
{
    const float sightInterval = 0.15f;

    readonly float patrolWaitTime;
    readonly float sightRange;
    readonly LayerMask obstacleMask;

    Transform[] patrolPoints;
    int   patrolIndex;
    float waitUntil = -1f;

    float sightTimer;
    bool  lastSightResult;

    public EnemyPatrolBehavior(Transform[] patrolPoints, float patrolWaitTime, float sightRange, LayerMask obstacleMask)
    {
        this.patrolPoints = patrolPoints;
        this.patrolWaitTime = patrolWaitTime;
        this.sightRange = sightRange;
        this.obstacleMask = obstacleMask;
    }

    public Transform FirstPoint => patrolPoints.Length > 0 ? patrolPoints[0] : null;

    public void SetPoints(Transform[] points)
    {
        patrolPoints = points;
        patrolIndex  = 0;
    }

    public bool CanSeePlayer(Transform enemy, Transform player, float dist)
    {
        sightTimer += Time.deltaTime;
        if (sightTimer < sightInterval) return lastSightResult;
        sightTimer = 0f;

        if (dist > sightRange) { lastSightResult = false; return false; }

        Vector3 origin = enemy.position + Vector3.up;
        Vector3 dir    = (player.position - origin).normalized;

        lastSightResult = !Physics.Raycast(origin, dir, dist, obstacleMask);
        return lastSightResult;
    }

    // Devriye durumunu günceller. Oyuncu görülürse true döner (çağıran Chase'e geçer).
    public bool Tick(NavMeshAgent agent, Transform enemy, Transform player, float distToPlayer)
    {
        if (CanSeePlayer(enemy, player, distToPlayer)) return true;

        if (patrolPoints.Length == 0) return false;
        if (!agent.isOnNavMesh) return false;
        if (patrolPoints[patrolIndex] == null) return false;

        // Hedefe ulaştı mı?
        if (!agent.pathPending && agent.remainingDistance < 0.4f)
        {
            if (waitUntil < 0f)
                waitUntil = Time.time + patrolWaitTime;

            if (Time.time < waitUntil) return false;

            waitUntil   = -1f;
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            if (patrolPoints[patrolIndex] != null)
                agent.SetDestination(patrolPoints[patrolIndex].position);
        }
        return false;
    }
}
}
