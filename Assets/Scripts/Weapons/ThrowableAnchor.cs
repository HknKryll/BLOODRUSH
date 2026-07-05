using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ThrowableAnchor : MonoBehaviour
{
    [SerializeField] float freezeDelay = 0.4f;  // fırlatıldıktan sonra havada donana kadar geçen süre
    [SerializeField] float lifetime    = 4f;     // havada kalma süresi

    Rigidbody rb;

    void Awake() => rb = GetComponent<Rigidbody>();

    void Start()
    {
        Invoke(nameof(Freeze), freezeDelay);
        Invoke(nameof(Expire), freezeDelay + lifetime);
    }

    void Freeze()
    {
        rb.velocity    = Vector3.zero;
        rb.isKinematic = true;
        GetComponent<Collider>().enabled = true;
    }

    void Expire() => Destroy(gameObject);
}
