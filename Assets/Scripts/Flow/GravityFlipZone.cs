using UnityEngine;
using Bloodrush.Player;

namespace Bloodrush.Flow
{
// Yerçekimi flip bölgesi: oyuncu içindeyken flip modu aktif — zıplayınca yerçekimi
// ters döner (zemin↔tavan), tavan oyuncunun zemini olur. Trigger collider gerektirir.
// Ayrıca içerideyken GRAPPLING HOOK devre dışı — yoksa oyuncu flip tırmanışını atlar.
//
// SAĞLAMLIK: Işınlama (ölüm-respawn, düşme-reset) ile bölgeden çıkınca Unity
// OnTriggerExit'i TETİKLEMEZ — bu yüzden flip modu (ve hook kilidi) açık kalıp başka
// odada da sorun çıkarıyordu. Çözüm: OnTriggerStay ile her fizik turunda "hâlâ içeride
// mi" işaretle; bir tur hiç Stay gelmezse (ışınlanıp çıktı) serbest bırak.
[RequireComponent(typeof(Collider))]
public class GravityFlipZone : MonoBehaviour
{
    PlayerMovement inside;
    GrapplingHook  hook;
    bool seen;   // bu fizik turunda oyuncu içeride görüldü mü

    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = true;
    }

    void OnTriggerEnter(Collider o)
    {
        var pm = o.GetComponentInParent<PlayerMovement>();
        if (pm == null) return;
        inside = pm;
        seen   = true;
        pm.SetFlipMode(true);

        hook = pm.GetComponent<GrapplingHook>();
        if (hook != null) hook.HookEnabled = false;   // kulede kanca yok
    }

    void OnTriggerStay(Collider o)
    {
        var pm = o.GetComponentInParent<PlayerMovement>();
        if (pm != null && pm == inside) seen = true;
    }

    void OnTriggerExit(Collider o)
    {
        var pm = o.GetComponentInParent<PlayerMovement>();
        if (pm == null || pm != inside) return;
        Release();
    }

    // Işınlanıp çıkışta OnTriggerExit gelmez — Stay yokluğuyla yakala.
    void FixedUpdate()
    {
        if (inside != null && !seen) Release();
        seen = false;
    }

    void OnDisable()
    {
        if (inside != null) Release();
    }

    // Flip modunu kapat + kancayı geri aç
    void Release()
    {
        if (inside != null) inside.SetFlipMode(false);
        if (hook   != null) hook.HookEnabled = true;
        inside = null;
        hook   = null;
    }
}
}
