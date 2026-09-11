using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Bloodrush.Shared.Audio;

namespace Bloodrush.Flow
{
// ADIM 11 — Çıkışı kapatan devrilmiş raf: "[E] İt" ile kenara devrilir/kayar ve yol açılır.
// Tek adımlık bir engel — bulmaca değil, tempoyu bir an düşüren fiziksel bir eylem.
public class PushObstacle : ProximityInteractable
{
    [Header("Engel")]
    [SerializeField] string promptLabel = "[E] Rafı İt";
    [Tooltip("İtilince gidilecek YEREL konum farkı (m).")]
    [SerializeField] Vector3 pushOffset = new Vector3(1.4f, 0f, 0f);
    [Tooltip("İtilince eklenecek YEREL dönüş (derece) — devrilme hissi.")]
    [SerializeField] Vector3 pushEuler  = new Vector3(0f, 0f, -18f);
    [SerializeField] float   pushTime   = 0.8f;

    [Tooltip("İtilince kapatılacak collider (yolu açan). Boşsa kendi collider'ı kapatılır.")]
    [SerializeField] Collider blockingCollider;

    [Header("Ses")]
    [SerializeField] AudioClip pushClip;
    [SerializeField] [Range(0f,2f)] float volume = 1f;

    [Header("İtilince")]
    public UnityEvent onPushed;

    bool pushed;

    protected override string PromptText => pushed ? null : promptLabel;

    protected override void OnInteract()
    {
        if (pushed) return;
        pushed = true;
        StartCoroutine(Push());
    }

    IEnumerator Push()
    {
        SfxPlayer.PlayAtPoint(pushClip, transform.position, volume);

        Vector3    fromPos = transform.localPosition;
        Quaternion fromRot = transform.localRotation;
        Vector3    toPos   = fromPos + pushOffset;
        Quaternion toRot   = fromRot * Quaternion.Euler(pushEuler);

        float t = 0f;
        while (t < pushTime)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / pushTime));
            transform.localPosition = Vector3.Lerp(fromPos, toPos, p);
            transform.localRotation = Quaternion.Slerp(fromRot, toRot, p);
            yield return null;
        }
        transform.localPosition = toPos;
        transform.localRotation = toRot;

        var col = blockingCollider != null ? blockingCollider : GetComponent<Collider>();
        if (col != null) col.enabled = false;

        onPushed?.Invoke();
    }

    // Itmenin NEREYE gidecegini Play'e girmeden gosterir. Push Offset local uzayda
    // uygulandigi icin objenin/parent'in rotasyonuna gore yon degisiyor; turuncu kure
    // hedefi, cizgi de yolu gosterir — yanlis eksen secince aninda gorulur.
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Vector3 target = transform.parent != null
                       ? transform.parent.TransformPoint(transform.localPosition + pushOffset)
                       : transform.position + pushOffset;

        Gizmos.color = new Color(1f, 0.6f, 0.15f, 0.95f);
        Gizmos.DrawLine(transform.position, target);
        Gizmos.DrawWireSphere(target, 0.3f);
    }
}
}
