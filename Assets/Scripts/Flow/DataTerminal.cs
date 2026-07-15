using System.Collections;
using UnityEngine;

// Veri terminali: oyuncu içinde durdukça dolar; dolunca upload'ı bir parça
// ilerletir, WaveDirector'a "escalation basamağı" bildirir ve EMP şok dalgası
// yayar (yakın düşmanları sersemletir + iter — terminale ulaşmak nefes aldırır).
// Kullanım: boş GO + BoxCollider (isTrigger) + bu script. Farklı katlara 4 tane koy.
using Bloodrush.UI;
using Bloodrush.FX;
using Bloodrush.Player;
using Bloodrush.Enemy;
using Bloodrush.Shared.Audio;

namespace Bloodrush.Flow
{
[RequireComponent(typeof(BoxCollider))]
public class DataTerminal : MonoBehaviour
{
    [SerializeField] float activateTime = 4f;
    [SerializeField] [Range(0f,1f)] float uploadShare = 0.25f;

    [Header("EMP Dalgası (tamamlanınca)")]
    [SerializeField] float empRadius    = 12f;
    [SerializeField] float empStun      = 2.5f;
    [SerializeField] float empKnockback = 9f;

    [Header("Görsel (opsiyonel)")]
    [SerializeField] Renderer  glow;      // idle gri / aktif sarı / bitti yeşil
    [SerializeField] Transform fillBar;   // X ölçeği ilerlemeyi gösterir

    [Header("Ses")]
    [SerializeField] AudioClip completeClip;

    static readonly Color IdleColor   = new Color(0.3f, 0.3f, 0.3f);
    static readonly Color ActiveColor = new Color(1f,   0.8f, 0.1f);
    static readonly Color DoneColor   = new Color(0.2f, 1f,   0.3f);

    float   progress;
    bool    playerInside;
    bool    done;
    Vector3 fillBaseScale;
    SfxPlayer sfx;

    void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        sfx = SfxPlayer.Create(gameObject, spatialBlend: 0f);
        if (fillBar) { fillBaseScale = fillBar.localScale; var s = fillBaseScale; s.x = 0f; fillBar.localScale = s; }
        SetGlow(IdleColor);
    }

    void OnTriggerEnter(Collider o) { if (o.GetComponentInParent<PlayerMovement>() != null) playerInside = true; }
    void OnTriggerExit(Collider o)  { if (o.GetComponentInParent<PlayerMovement>() != null) playerInside = false; }

    void Update()
    {
        if (done) return;

        if (playerInside)
        {
            progress += Time.deltaTime / activateTime;
            SetGlow(ActiveColor);
            UpdateFill(Mathf.Clamp01(progress));
            if (progress >= 1f) Complete();
        }
        else
        {
            SetGlow(IdleColor);   // dışarıda → dur (ilerleme korunur)
        }
    }

    void Complete()
    {
        done = true;
        GameHUD.AddUploadProgress(uploadShare);
        if (WaveDirector.Instance != null) WaveDirector.Instance.OnTerminalDone();
        SetGlow(DoneColor);
        UpdateFill(1f);
        sfx.Play(completeClip);
        FireEMP();
    }

    // EMP: yakın düşmanları sersemlet + terminalden dışa fırlat
    void FireEMP()
    {
        var hitEnemies = new System.Collections.Generic.HashSet<EnemyAI>();
        foreach (var col in Physics.OverlapSphere(transform.position, empRadius))
        {
            var enemy = col.GetComponentInParent<EnemyAI>();
            if (enemy == null || !hitEnemies.Add(enemy)) continue;

            enemy.Stun(empStun);
            Vector3 away = enemy.transform.position - transform.position;
            enemy.Knockback(away, empKnockback);   // isLarge olanlar itilmez ama stun yer
        }

        CameraShake.Shake(0.15f, 0.2f);
        StartCoroutine(EmpVisual());
    }

    // Zeminde hızla genişleyen parlak disk (şok dalgası görseli)
    IEnumerator EmpVisual()
    {
        var disk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(disk.GetComponent<Collider>());
        disk.transform.position = transform.position + Vector3.up * 0.1f;
        var mat = disk.GetComponent<Renderer>().material;
        mat.color = new Color(0.5f, 0.85f, 1f);

        float t = 0f;
        const float dur = 0.35f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float r = Mathf.Lerp(0.5f, empRadius, t / dur) * 2f;
            disk.transform.localScale = new Vector3(r, 0.05f, r);
            yield return null;
        }
        Destroy(disk);
    }

    void UpdateFill(float t)
    {
        if (!fillBar) return;
        var s = fillBaseScale; s.x = fillBaseScale.x * t; fillBar.localScale = s;
    }

    void SetGlow(Color c) { if (glow) glow.material.color = c; }
}
}
