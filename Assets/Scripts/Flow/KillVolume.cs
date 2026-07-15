using UnityEngine;

// Düşme hacmi: oyuncu girince son checkpoint'te canlandırılır (sahne baştan
// yüklenmez). Parkurun/uçurumun altına geniş bir trigger olarak konur.
// Kullanım: boş GO + BoxCollider (isTrigger) + bu script.
[RequireComponent(typeof(BoxCollider))]
public class KillVolume : MonoBehaviour
{
    void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerMovement>() == null)
        {
            Debug.Log($"[KillVolume] '{other.name}' girdi ama Player değil.", this);
            return;
        }
        Debug.Log("[KillVolume] Oyuncu düştü — respawn çağrılıyor.", this);
        GameFlow.RespawnAtCheckpoint();
    }
}
