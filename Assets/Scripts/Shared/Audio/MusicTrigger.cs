using UnityEngine;
using Bloodrush.Player;

namespace Bloodrush.Shared.Audio
{
// Oyuncu icine girince MusicDirector'a "su parcaya gec" diyen tetik hacmi.
//
// Neden Arena'ya baglanmadi: Arena.cs'te hic UnityEvent yok, yani disariya haber
// veremiyor. Oraya event eklemek 3 arenanin paylastigi bir dovus script'ine dokunmak
// olurdu; bunun yerine muzik tamamen kendi objelerinde duruyor. Sonraki bolumlerde bu
// objeyi kopyalayip Track Id'yi degistirmek yeterli.
//
// Yakinlik tespiti proje geneli desen: GetComponentInParent<PlayerMovement>() (tag degil).
[RequireComponent(typeof(BoxCollider))]
public class MusicTrigger : MonoBehaviour
{
    [Tooltip("MusicDirector'daki parca adi, orn. 'tutorial' / 'boss'.")]
    [SerializeField] string trackId = "tutorial";

    [Tooltip("Acikken fade YOK — parca aninda ve tam sesle girer. Sessizlikten sonra " +
             "muzigin patlamasi icin bu kullanilir.")]
    [SerializeField] bool instant;

    [Tooltip("Instant kapaliyken gecis suresi (sn). Calan baska bir parca varsa " +
             "crossfade olur ve olcu sinirina hizalanir.")]
    [SerializeField] float crossfadeDuration = 1f;

    [Tooltip("Kapaliysa oyuncu her girisinde tekrar tetiklenir. Boss odasi icin KAPALI " +
             "olmali: olup checkpoint'e dondukten sonra geri girince yeniden gecmeli.")]
    [SerializeField] bool onlyOnce = true;

    bool fired;

    void Reset()
    {
        var box = GetComponent<BoxCollider>();
        if (box != null)
        {
            box.isTrigger = true;
            box.size      = new Vector3(6f, 4f, 2f);
            box.center    = new Vector3(0f, 2f, 0f);
        }
    }

    void Start()
    {
        var box = GetComponent<BoxCollider>();
        if (box != null) box.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (onlyOnce && fired) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;

        if (MusicDirector.Instance == null)
        {
            Debug.LogWarning($"[MusicTrigger] '{name}' tetiklendi ama sahnede MusicDirector yok.", this);
            return;
        }

        fired = true;
        if (instant) MusicDirector.Instance.PlayInstant(trackId);
        else         MusicDirector.Instance.Play(trackId, crossfadeDuration);

        Debug.Log($"[MusicTrigger] '{name}' → '{trackId}' " +
                  (instant ? "(ani giris)" : $"(crossfade {crossfadeDuration:0.0} sn)"), this);
    }

    void OnDrawGizmosSelected()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.color  = new Color(0.5f, 0.8f, 1f, 0.3f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
}
