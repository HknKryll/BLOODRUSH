using UnityEngine;
using UnityEngine.Events;

// Çekirdek kampanya döngüsü: oyuncu terminale yaklaşınca upload otomatik
// başlar, süre boyunca dalga dalga düşman gelir, HUD'daki YÜKLEME barı dolar.
// Kendi payı (uploadShare) dolunca spawn durur ve çıkış bariyeri açılır.
// Kullanım: boş GO + BoxCollider (isTrigger) + bu script.
using Bloodrush.Player;
using Bloodrush.UI;
using Bloodrush.Shared.Audio;

namespace Bloodrush.Flow
{
[RequireComponent(typeof(BoxCollider))]
public class UploadTerminal : MonoBehaviour
{
    [Header("Upload")]
    [SerializeField] [Range(0f,1f)] float uploadShare = 0.4f;  // toplam bara katkısı
    [SerializeField] float uploadDuration = 90f;               // saniye

    [Header("Düşman Dalgası")]
    [SerializeField] Transform[]  spawnPoints;
    [SerializeField] GameObject[] enemyPrefabs;
    [SerializeField] float spawnInterval = 6f;

    [Header("Kapı / Görsel")]
    [SerializeField] GameObject exitBarrier;    // upload bitince kapanır
    [SerializeField] GameObject activeVisual;   // upload sırasında yanıp söner (emissive küp vs.)

    [Header("Bitince")]
    public UnityEvent onComplete;               // Ch6: EndingSequence.Begin buraya bağlanır

    [Header("Ses")]
    [SerializeField] AudioClip startClip;
    [SerializeField] [Range(0f,1f)] float startVolume = 0.9f;
    [SerializeField] AudioClip completeClip;
    [SerializeField] [Range(0f,1f)] float completeVolume = 1f;

    float progress;      // bu terminalin kendi payı, 0-1
    bool  uploading;
    bool  completed;
    float spawnTimer;
    SfxPlayer sfx;

    void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;

        sfx = SfxPlayer.Create(gameObject, spatialBlend: 0f);

        if (exitBarrier)  exitBarrier.SetActive(true);
        if (activeVisual) activeVisual.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (uploading || completed) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;

        uploading  = true;
        spawnTimer = spawnInterval * 0.5f;   // ilk dalga çabuk gelsin
        sfx.Play(startClip, startVolume);
    }

    void Update()
    {
        if (!uploading) return;

        float delta = Time.deltaTime / uploadDuration;
        progress += delta;
        GameHUD.AddUploadProgress(uploadShare * delta);

        // Yanıp sönen terminal görseli
        if (activeVisual)
            activeVisual.SetActive(Mathf.FloorToInt(Time.time / 0.4f) % 2 == 0);

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            spawnTimer = spawnInterval;
            SpawnEnemy();
        }

        if (progress >= 1f) Complete();
    }

    void SpawnEnemy()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return;
        if (spawnPoints  == null || spawnPoints.Length  == 0) return;

        Vector3 pos    = spawnPoints[Random.Range(0, spawnPoints.Length)].position;
        var     prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        Instantiate(prefab, pos, Quaternion.identity);
    }

    void Complete()
    {
        uploading = false;
        completed = true;
        if (activeVisual) activeVisual.SetActive(true);
        if (exitBarrier)  exitBarrier.SetActive(false);
        sfx.Play(completeClip, completeVolume);
        onComplete?.Invoke();
    }
}
}
