using UnityEngine;
using Bloodrush.Player;

namespace Bloodrush.Flow
{
// Yerçekimi bölgesi: oyuncu içindeyken düşük yerçekimi (floaty) ve/veya sürekli
// yukarı itiş (anti-grav kolonu) uygular. Trigger collider gerektirir.
// affectsGravity: oda-geneli tek bir düşük-g bölgesi TRUE olur; iç içe geçen
// updraft kolonları FALSE (sadece itiş) olur — böylece kolondan çıkınca oda-geneli
// düşük-g bozulmaz. (Tam eksen-çevirme tam sürüme saklanıyor.)
[RequireComponent(typeof(Collider))]
public class GravityZone : MonoBehaviour
{
    [Tooltip("Bu bölge yerçekimini değiştirsin mi? Oda-geneli düşük-g için TRUE; updraft kolonu için FALSE.")]
    [SerializeField] bool  affectsGravity = true;
    [Tooltip("İçerideyken yerçekimi çarpanı (affectsGravity ise). 0.25 = floaty, 1 = normal.")]
    [SerializeField] float gravityScale   = 0.3f;
    [Tooltip(">0 ise sürekli yukarı itiş hızı (anti-grav kolonu). 0 = kapalı.")]
    [SerializeField] float updraftSpeed   = 0f;

    PlayerMovement inside;

    // GravityRoomBuilder kurulum-zamanında çağırır
    public void Configure(bool affectGravity, float gravScale, float updraft)
    {
        affectsGravity = affectGravity;
        gravityScale   = gravScale;
        updraftSpeed   = updraft;
    }

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider o)
    {
        var pm = o.GetComponentInParent<PlayerMovement>();
        if (pm == null) return;
        inside = pm;
        if (affectsGravity) pm.GravityScale = gravityScale;
    }

    void OnTriggerExit(Collider o)
    {
        var pm = o.GetComponentInParent<PlayerMovement>();
        if (pm == null || pm != inside) return;
        if (affectsGravity) pm.GravityScale = 1f;
        inside = null;
    }

    void Update()
    {
        if (inside != null && updraftSpeed > 0f)
            inside.AddUpdraft(updraftSpeed);
    }

    void OnDisable()
    {
        if (inside != null && affectsGravity) inside.GravityScale = 1f;
        inside = null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = updraftSpeed > 0f ? new Color(0.3f, 0.7f, 1f, 0.25f)
                                         : new Color(0.6f, 0.4f, 1f, 0.2f);
        if (GetComponent<Collider>() is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}
}
