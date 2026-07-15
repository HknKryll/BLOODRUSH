using UnityEngine;
using Bloodrush.Shared;
using Bloodrush.Enemy;

namespace Bloodrush.Weapons
{
[RequireComponent(typeof(Rigidbody))]
public class LauncherProjectile : MonoBehaviour
{
    [Header("Ortak")]
    [SerializeField] float fuseTime = 3f;
    [SerializeField] float blastRadius = 5f;
    [SerializeField] LayerMask damageMask = ~0;
    [SerializeField] GameObject explosionVFX;

    [Header("Grenade")]
    [SerializeField] bool isFlash = false;
    [SerializeField] float blastDamage = 75f;

    [Header("Flash")]
    [SerializeField] float flashDuration = 2.5f;

    Rigidbody rb;
    bool detonated;

    void Awake() => rb = GetComponent<Rigidbody>();

    void Start() => Invoke(nameof(Detonate), fuseTime);

    public void Launch(Vector3 velocity)
    {
        rb.velocity = velocity;
    }

    // Revolver ile vurulunca veya zamanlayıcı dolunca çağrılır
    public void Detonate()
    {
        if (detonated) return;
        detonated = true;
        CancelInvoke();

        if (explosionVFX)
            Instantiate(explosionVFX, transform.position, Quaternion.identity);

        Collider[] cols = Physics.OverlapSphere(transform.position, blastRadius, damageMask);

        foreach (Collider col in cols)
        {
            if (isFlash)
                ApplyFlash(col);
            else
                ApplyBlast(col);
        }

        Destroy(gameObject);
    }

    void ApplyBlast(Collider col)
    {
        Health h = col.GetComponent<Health>();
        if (h == null) return;

        // Merkeze yakınlık oranında hasar azalır
        float dist = Vector3.Distance(transform.position, col.transform.position);
        float dmg  = Mathf.Lerp(blastDamage, blastDamage * 0.2f, dist / blastRadius);
        h.TakeDamage(dmg);
    }

    void ApplyFlash(Collider col)
    {
        col.GetComponent<EnemyAI>()?.Stun(flashDuration);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = isFlash ? new Color(1f, 1f, 0f, 0.3f) : new Color(1f, 0.3f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, blastRadius);
    }
#endif
}
}
