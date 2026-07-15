using UnityEngine;

// Oyuncuyu sahne başında bu noktaya ışınlar (pozisyon + bakış yönü).
// ArenaBuilder'ın ürettiği PlayerStart'ta otomatik var; istediğin yere taşı.
using Bloodrush.Player;

namespace Bloodrush.Flow
{
public class PlayerStartPoint : MonoBehaviour
{
    void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        var pm = player.GetComponent<PlayerMovement>();
        if (pm != null)
            pm.Teleport(transform.position, transform.rotation);
        else
            player.transform.SetPositionAndRotation(transform.position, transform.rotation);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
    }
#endif
}
}
