using UnityEngine;

// Checkpoint: oyuncu girince respawn noktası olarak kaydedilir.
// Kullanım: boş GO + BoxCollider (isTrigger) + bu script. GO'nun yönü (Y)
// respawn'da oyuncunun bakacağı yön olur.
[RequireComponent(typeof(BoxCollider))]
public class Checkpoint : MonoBehaviour
{
    bool used;

    void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (used) return;
        if (other.GetComponentInParent<PlayerMovement>() == null)
        {
            Debug.Log($"[Checkpoint] '{other.name}' girdi ama Player değil.", this);
            return;
        }
        used = true;
        Debug.Log("[Checkpoint] Oyuncu girdi — checkpoint kaydediliyor.", this);
        GameFlow.SetCheckpoint(transform);
    }
}
