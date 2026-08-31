using UnityEngine;
using UnityEngine.AI;
using Bloodrush.Enemy;
using Bloodrush.Player;

namespace Bloodrush.Flow
{
// Basit oda düşman spawner'ı: oyuncu tetik alanına girince atanmış düşmanı spawn
// noktalarında bir kez doğurur (NavMesh'e yapıştırır + hemen avlamaya başlatır).
// WaveDirector gibi karmaşık değil — yerçekimi odası gibi tek-seferlik odalar için.
[RequireComponent(typeof(Collider))]
public class RoomEnemySpawner : MonoBehaviour
{
    [Tooltip("Doğurulacak düşman prefabı (KULLANICI atar).")]
    [SerializeField] GameObject  enemyPrefab;
    [SerializeField] int         count = 4;
    [SerializeField] Transform[] spawnPoints;

    bool spawned;

    public void Configure(Transform[] points) { spawnPoints = points; }

    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = true;
    }

    void OnTriggerEnter(Collider o)
    {
        if (spawned || o.GetComponentInParent<PlayerMovement>() == null) return;
        spawned = true;

        if (enemyPrefab == null || spawnPoints == null || spawnPoints.Length == 0) return;
        for (int i = 0; i < count; i++)
        {
            var pt = spawnPoints[i % spawnPoints.Length];
            var go = Instantiate(enemyPrefab, pt.position, pt.rotation);

            if (go.TryGetComponent(out NavMeshAgent agent) &&
                NavMesh.SamplePosition(pt.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                agent.Warp(hit.position);

            if (go.TryGetComponent(out EnemyAI ai))
                ai.AlertNow();
        }
    }
}
}
