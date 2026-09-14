using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using Bloodrush.Enemy;
using Bloodrush.Player;
using Bloodrush.Shared;
using Bloodrush.UI;

namespace Bloodrush.Flow
{
// "Aşırı Yük" bulmaca odasının denetleyicisi.
//
// AKIŞ: Oyuncu odaya girer → giriş mühürlenir → dalgalar doğar → oyuncu düşmanları
// kancayla şarj bölgesine çekip orada öldürür → jeneratör dolunca çıkış açılır.
//
// Projede zaten üç bulmaca var (SequenceLock = sıra, FusePanel = toplama,
// ValveSequence = süreli iki anahtar); hiçbiri ÇATIŞMAYI araç yapmıyordu. Buradaki
// fikir, savaşın kendisini bulmacanın çözümü hâline getirmek.
//
// Düşman doğurma sırası RoomEnemySpawner'dan alındı (Instantiate → NavMesh.SamplePosition
// + Warp → AlertNow). O bileşen tek-seferlik ve trigger'a bağlı olduğu için doğrudan
// kullanılamıyor; dalga tekrarı gerektiği için sıra buraya kopyalandı.
[RequireComponent(typeof(BoxCollider))]
public class OverloadRoom : MonoBehaviour
{
    enum State { Idle, Active, Solved }

    [Header("Bağlantılar")]
    [SerializeField] ChargeZone        zone;
    [SerializeField] OverloadGenerator generator;
    [Tooltip("Oyuncu girince AÇILIR (odayı mühürler). Başlangıçta kapalı olmalı.")]
    [SerializeField] GameObject entryBarrier;
    [Tooltip("Şarj dolunca KAPANIR (yol açılır). Başlangıçta açık olmalı.")]
    [SerializeField] GameObject exitBarrier;

    [Header("Düşman dalgaları")]
    [Tooltip("KANCAYLA ÇEKİLEBİLEN küçük düşman olmalı — GrapplingHook, IsLarge " +
             "düşmanları çekmiyor. Zırhlı/Büyük_Düşman burada işe yaramaz.")]
    [SerializeField] GameObject  enemyPrefab;
    [SerializeField] Transform[] spawnPoints;
    [SerializeField] int   waveCount     = 3;
    [SerializeField] int   enemiesPerWave = 4;
    [Tooltip("Dalgadaki düşmanların bu oranı ölünce sonraki dalga gelir.")]
    [Range(0.1f, 1f)]
    [SerializeField] float nextWaveKillRatio = 0.5f;
    [Tooltip("Oran dolmasa bile en geç bu kadar sonra sonraki dalga gelir (sn).")]
    [SerializeField] float maxWaveDelay = 25f;
    [Tooltip("İlk dalgadan önceki gecikme (sn).")]
    [SerializeField] float firstWaveDelay = 1.5f;
    [Tooltip("Şarj dolmadan dalgalar biterse yeniden dalga gönder (oda kilitlenmesin).")]
    [SerializeField] bool  endlessUntilCharged = true;

    [Header("Olaylar")]
    public UnityEvent onRoomStarted;
    public UnityEvent onRoomSolved;

    State state = State.Idle;
    readonly List<EnemyAI> spawned = new List<EnemyAI>();
    int aliveInWave;
    int spawnedInWave;

    void Reset()
    {
        var box = GetComponent<BoxCollider>();
        if (box != null)
        {
            box.isTrigger = true;
            box.size      = new Vector3(38f, 8f, 19f);
            box.center    = new Vector3(0f, 4f, 0f);
        }
    }

    void Start()
    {
        var box = GetComponent<BoxCollider>();
        if (box != null) box.isTrigger = true;

        if (entryBarrier != null) entryBarrier.SetActive(false);   // oyuncu girebilsin
        if (exitBarrier  != null) exitBarrier.SetActive(true);     // çıkış kapalı

        if (zone != null)
        {
            zone.onCharged.AddListener(Solve);
            if (generator != null) zone.onChargeChanged.AddListener(generator.SetCharge);
        }
        else
        {
            Debug.LogError("[OverloadRoom] Charge Zone atanmamış — oda çalışmaz.", this);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (state != State.Idle) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;
        Begin();
    }

    void Begin()
    {
        state = State.Active;

        if (entryBarrier != null) entryBarrier.SetActive(true);    // arkadan mühürle
        Notification.Show("JENERATÖRÜ ŞARJ ET — SADECE BÖLGE İÇİNDEKİ ÖLÜMLER SAYILIR", 4f);
        Debug.Log("[OverloadRoom] Oda başladı.", this);

        onRoomStarted?.Invoke();
        StartCoroutine(RunWaves());
    }

    IEnumerator RunWaves()
    {
        yield return new WaitForSeconds(firstWaveDelay);

        int wave = 0;
        while (state == State.Active)
        {
            if (wave >= waveCount && !endlessUntilCharged) break;

            SpawnWave(wave);
            wave++;

            float deadline = Time.time + maxWaveDelay;
            int   needDead = Mathf.Max(1, Mathf.CeilToInt(spawnedInWave * nextWaveKillRatio));

            while (state == State.Active &&
                   (spawnedInWave - aliveInWave) < needDead &&
                   Time.time < deadline)
                yield return null;
        }

        if (state == State.Active)
            Debug.Log("[OverloadRoom] Dalgalar bitti, şarj hâlâ dolmadı. " +
                      "Endless kapalı — kalan düşmanlarla çözmen gerekiyor.", this);
    }

    void SpawnWave(int index)
    {
        if (enemyPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[OverloadRoom] Enemy Prefab veya Spawn Points atanmamış — " +
                           "dalga doğurulamadı.", this);
            return;
        }

        spawnedInWave = 0;
        aliveInWave   = 0;

        for (int i = 0; i < enemiesPerWave; i++)
        {
            // Bölgeden uzak bir spawn noktası seç — düşman bedava şarj olmasın.
            var pt = PickSpawnPoint(index * enemiesPerWave + i);
            var go = Instantiate(enemyPrefab, pt.position, pt.rotation);

            if (go.TryGetComponent(out NavMeshAgent agent) &&
                NavMesh.SamplePosition(pt.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                agent.Warp(hit.position);

            var ai = go.GetComponent<EnemyAI>();
            if (ai != null)
            {
                ai.AlertNow();
                spawned.Add(ai);

                if (zone != null)
                {
                    var avoid = go.AddComponent<ZoneAvoidance>();
                    avoid.Configure(zone);
                }
            }

            var health = go.GetComponent<Health>();
            if (health != null)
            {
                spawnedInWave++;
                aliveInWave++;
                health.onDeath.AddListener(() => aliveInWave = Mathf.Max(0, aliveInWave - 1));
                if (zone != null && ai != null) zone.Register(ai, health);
            }
        }

        Debug.Log($"[OverloadRoom] Dalga {index + 1}: {spawnedInWave} düşman doğdu.", this);
    }

    Transform PickSpawnPoint(int seed)
    {
        // Bölgenin içinde kalan spawn noktalarını atla; hiçbiri uygun değilse sıradakini kullan.
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            var pt = spawnPoints[(seed + i) % spawnPoints.Length];
            if (pt == null) continue;
            if (zone != null && zone.Contains(pt.position + Vector3.up * 0.9f)) continue;
            return pt;
        }
        return spawnPoints[seed % spawnPoints.Length];
    }

    void Solve()
    {
        if (state == State.Solved) return;
        state = State.Solved;

        if (exitBarrier  != null) exitBarrier.SetActive(false);
        if (entryBarrier != null) entryBarrier.SetActive(false);   // geri dönebilsin

        Debug.Log("[OverloadRoom] Çözüldü — çıkış açıldı.", this);
        onRoomSolved?.Invoke();
    }

    // Kurulum sırasında OverloadRoomBuilder çağırır.
    public void Configure(ChargeZone z, OverloadGenerator gen,
                          GameObject entry, GameObject exit, Transform[] points)
    {
        zone         = z;
        generator    = gen;
        entryBarrier = entry;
        exitBarrier  = exit;
        spawnPoints  = points;
    }

    void OnDrawGizmosSelected()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.color  = new Color(0.4f, 0.7f, 1f, 0.15f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
    }
}
}
