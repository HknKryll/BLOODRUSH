using UnityEngine;

// Menzilli düşmanların attığı kaçılabilir mermi. İleri uçar, oyuncuya değince
// hasar verir, duvara değince/süresi dolunca yok olur.
public class EnemyProjectile : MonoBehaviour
{
    float speed;
    float damage;
    float life = 5f;

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
