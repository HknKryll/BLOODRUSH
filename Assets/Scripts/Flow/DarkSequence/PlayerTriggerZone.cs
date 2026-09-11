using UnityEngine;
using UnityEngine.Events;
using Bloodrush.Player;

namespace Bloodrush.Flow
{
// Oyuncu icine girince UnityEvent tetikleyen en sade tetikleyici. Projede boyle genel
// amacli bir parca yoktu; her sistem kendi OnTriggerEnter'ini yaziyordu. Karanlik
// Sekans'ta "koridorun sonuna varinca guc geri gelsin" gibi anlar icin gerekli.
//
// Kullanim: bos objeye ekle, Box Collider otomatik gelir ve Is Trigger acilir,
// boyutunu ayarla, On Entered listesine ne olacagini bagla.
[RequireComponent(typeof(BoxCollider))]
public class PlayerTriggerZone : MonoBehaviour
{
    [Tooltip("Bir kez mi calissin, her giriste mi?")]
    [SerializeField] bool onlyOnce = true;

    [Tooltip("Oyuncu girince tetiklenir.")]
    public UnityEvent onEntered;

    bool fired;

    void Reset()
    {
        var box = GetComponent<BoxCollider>();
        if (box != null)
        {
            box.isTrigger = true;
            box.size      = new Vector3(4f, 3f, 2f);
            box.center    = new Vector3(0f, 1.5f, 0f);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (onlyOnce && fired) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;

        fired = true;
        Debug.Log($"[PlayerTriggerZone] Tetiklendi: {name}", this);
        onEntered?.Invoke();
    }

    void OnDrawGizmosSelected()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.color  = new Color(0.3f, 1f, 0.5f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
    }
}
}
