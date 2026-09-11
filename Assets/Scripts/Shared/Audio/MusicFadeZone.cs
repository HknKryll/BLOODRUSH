using UnityEngine;
using Bloodrush.Player;

namespace Bloodrush.Shared.Audio
{
// Oyuncu bu hacme girince bagli RoomMusic'i sondurur, cikinca geri acar.
//
// Resepsiyonda kullanimi: asansorun onune/icine koy — oyuncu asagi inmek uzere kabine
// girdiginde muzik soner, vazgecip cikarsa geri gelir.
//
// Dogrudan referansla calisir (UnityEvent degil): zonu koy, muzik objesini surukle, bitti.
// Baska odalarda da aynen kullanilir.
//
// NOT: Projede PlayerTriggerZone var ama sadece onEntered tasiyor, onExited yok. Ona alan
// eklemek CH4 sistemine dokunmak olurdu; bu yuzden ayri ve bagimsiz yazildi.
[RequireComponent(typeof(BoxCollider))]
public class MusicFadeZone : MonoBehaviour
{
    [Tooltip("Sondurulecek/acilacak muzik. Sahnedeki RoomMusic objesini surukle.")]
    [SerializeField] RoomMusic music;

    [Tooltip("KAPALI: ters calisir — hacme girince muzik ACILIR, cikinca soner. " +
             "(Muzigin sadece belirli bir odada calmasini istedigin durum icin.)")]
    [SerializeField] bool fadeOutOnEnter = true;

    void Reset()
    {
        var box = GetComponent<BoxCollider>();
        if (box != null)
        {
            box.isTrigger = true;
            box.size      = new Vector3(4f, 3f, 4f);
            box.center    = new Vector3(0f, 1.5f, 0f);
        }
    }

    void Awake()
    {
        // Elle eklenen collider'da isTrigger unutulursa oyuncu duvara toslar — zorla.
        var box = GetComponent<BoxCollider>();
        if (box != null) box.isTrigger = true;

        if (music == null)
            Debug.LogWarning("[MusicFadeZone] Music alani bos — hicbir sey olmayacak.", this);
    }

    void OnTriggerEnter(Collider other)
    {
        if (music == null || !IsPlayer(other)) return;
        if (fadeOutOnEnter) music.FadeOut(); else music.FadeIn();
    }

    void OnTriggerExit(Collider other)
    {
        if (music == null || !IsPlayer(other)) return;
        if (fadeOutOnEnter) music.FadeIn(); else music.FadeOut();
    }

    // Proje geneli oyuncu tespiti deseni (33 yerde ayni sekilde kullaniliyor).
    static bool IsPlayer(Collider c) => c.GetComponentInParent<PlayerMovement>() != null;

    void OnDrawGizmosSelected()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.color  = new Color(0.4f, 0.7f, 1f, 0.3f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
    }
}
}
