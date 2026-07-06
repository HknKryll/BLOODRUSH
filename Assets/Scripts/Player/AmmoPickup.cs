using UnityEngine;

public class AmmoPickup : MonoBehaviour
{
    [SerializeField] int       minAmmo    = 3;
    [SerializeField] int       maxAmmo    = 7;
    [SerializeField] AudioClip pickupClip;

    bool collected;

    public void Collect(PlayerShoot shoot)
    {
        if (collected || shoot == null) return;
        collected = true;
        shoot.AddAmmo(Random.Range(minAmmo, maxAmmo + 1));
        if (pickupClip != null) AudioSource.PlayClipAtPoint(pickupClip, transform.position);
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 1.5f);
    }
}
