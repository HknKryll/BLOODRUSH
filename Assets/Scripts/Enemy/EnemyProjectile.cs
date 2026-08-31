using UnityEngine;

// Menzilli düşmanların attığı kaçılabilir mermi. İleri uçar, oyuncuya değince
// hasar verir, duvara değince/süresi dolunca yok olur.
using Bloodrush.Shared;
using Bloodrush.Player;

namespace Bloodrush.Enemy
{
public class EnemyProjectile : MonoBehaviour
{
    float speed;
    float damage;
    float life = 5f;

    static Material trailMat;

    void Awake()
    {
        // Asset'siz parlak iz (tracer) — gelen ateş net görünür. Prefab'a zaten
        // TrailRenderer eklenmemişse ekle.
        if (GetComponent<TrailRenderer>() == null)
        {
            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.time            = 0.12f;
            trail.startWidth      = 0.10f;
            trail.endWidth        = 0f;
            trail.numCapVertices  = 2;
            trail.sharedMaterial  = GetTrailMat();
        }
    }

    static Material GetTrailMat()
    {
        if (trailMat == null)
        {
            trailMat = new Material(Shader.Find("HDRP/Unlit"));
            trailMat.SetColor("_UnlitColor", new Color(1f, 0.5f, 0.2f) * 1.8f);   // hafif HDR turuncu (Bloom patlatmasın)
        }
        return trailMat;
    }

    public void Launch(Vector3 direction, float projectileSpeed, float projectileDamage)
    {
        transform.forward = direction;
        speed  = projectileSpeed;
        damage = projectileDamage;
    }

    void Update()
    {
        float step = speed * Time.deltaTime;

        // Yolda oyuncu/duvar var mı?
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, step + 0.2f,
                ~0, QueryTriggerInteraction.Ignore))
        {
            var playerHealth = hit.collider.GetComponentInParent<Health>();
            var isPlayer     = hit.collider.GetComponentInParent<PlayerMovement>() != null;
            if (isPlayer && playerHealth != null)
                playerHealth.TakeDamage(damage);

            // Oyuncu ya da duvar — her hâlükârda yok ol (başka düşmana zarar vermez)
            if (isPlayer || hit.collider.GetComponentInParent<EnemyAI>() == null)
            {
                Destroy(gameObject);
                return;
            }
        }

        transform.position += transform.forward * step;

        life -= Time.deltaTime;
        if (life <= 0f) Destroy(gameObject);
    }
}
}
