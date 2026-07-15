using System.Collections.Generic;
using UnityEngine;

// Kill-gate: oyuncu trigger'a girince giriş kapanır, düşmanlar aktifleşir;
// hepsi ölünce çıkış bariyeri açılır.
// Kullanım: boş GO + BoxCollider (isTrigger) + bu script.
// Düşmanlar SAHNEYE elle dizilir (prefab değil, instance) ve enemies[]'e sürüklenir.
using Bloodrush.Shared;
using Bloodrush.Player;

namespace Bloodrush.Flow
{
[RequireComponent(typeof(BoxCollider))]
public class Arena : MonoBehaviour
{
    [Header("Bariyerler")]
    [SerializeField] GameObject entryBarrier;   // savaş sırasında aktif
    [SerializeField] GameObject exitBarrier;    // savaş bitince kapanır

    [Header("Düşmanlar")]
    [SerializeField] GameObject[] enemies;

    [Header("Ses")]
    [SerializeField] AudioClip lockClip;
    [SerializeField] [Range(0f,1f)] float lockVolume = 0.8f;
    [SerializeField] AudioClip clearClip;
    [SerializeField] [Range(0f,1f)] float clearVolume = 0.9f;

    readonly List<GameObject> validEnemies = new();
    int  aliveCount;
    bool triggered;
    AudioSource audioSrc;

    void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;

        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake  = false;
        audioSrc.spatialBlend = 0f;

        if (entryBarrier) entryBarrier.SetActive(false);
        if (exitBarrier)  exitBarrier.SetActive(true);

        foreach (var e in enemies)
        {
            if (e == null) continue;

            // Prefab dosyası mı, sahne objesi mi? Prefab ise sahnede çalışmaz.
            if (!e.scene.IsValid())
            {
                Debug.LogWarning($"[Arena] '{e.name}' bir PREFAB, sahne objesi değil — atlanıyor. " +
                                 "Prefab'ı sahneye koyup oluşan instance'ı diziye sürükle.", this);
                continue;
            }

            validEnemies.Add(e);
            e.SetActive(false);

            if (e.TryGetComponent(out Health h))
                h.onDeath.AddListener(OnEnemyDied);
            else
                Debug.LogWarning($"[Arena] '{e.name}' üzerinde Health yok — ölümü sayılamaz.", this);
        }

        Debug.Log($"[Arena] {validEnemies.Count} geçerli düşman hazır.", this);
    }

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;

        triggered  = true;
        aliveCount = validEnemies.Count;

        foreach (var e in validEnemies)
            e.SetActive(true);

        if (entryBarrier) entryBarrier.SetActive(true);
        if (lockClip) audioSrc.PlayOneShot(lockClip, lockVolume);

        Debug.Log($"[Arena] Oyuncu girdi — {aliveCount} düşman aktif.", this);

        if (aliveCount <= 0) OpenGates();   // boş arena — kilitleme
    }

    void OnEnemyDied()
    {
        aliveCount--;
        Debug.Log($"[Arena] Düşman öldü — kalan: {aliveCount}", this);
        if (aliveCount <= 0) OpenGates();
    }

    void OpenGates()
    {
        Debug.Log("[Arena] Tüm düşmanlar öldü — kapı açılıyor.", this);

        if (entryBarrier) entryBarrier.SetActive(false);
        if (exitBarrier)  exitBarrier.SetActive(false);
        if (clearClip) audioSrc.PlayOneShot(clearClip, clearVolume);

        var shoot = FindObjectOfType<PlayerShoot>();
        shoot?.RefillWave();
    }
}
}
