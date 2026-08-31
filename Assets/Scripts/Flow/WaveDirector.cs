using UnityEngine;
using UnityEngine.AI;

// Ch3 Server Core dalga yönetmeni. Spawn'lar "heat" değerine bağlı: heat hem
// zamanla (kamp cezası) hem her terminal tamamlandıkça (ilerleme) yükselir.
// Heat arttıkça spawn sıklaşır, canlı düşman tavanı ve zor düşman oranı artar.
// Tüm terminaller bitince spawn durur, çıkış bariyeri açılır.
using Bloodrush.Shared;
using Bloodrush.Enemy;

namespace Bloodrush.Flow
{
public class WaveDirector : MonoBehaviour
{
    public static WaveDirector Instance { get; private set; }

    // HUD "VERİ x/y" sayacı için (bkz. GameHUD)
    public int TerminalsDone  => terminalsDone;
    public int TotalTerminals => totalTerminals;

    // Tüm terminaller bitti mi? (SequenceLock puzzle kapısını buna göre açar)
    public bool IsFinished => finished;

    [Header("Spawn Noktaları")]
    [SerializeField] Transform[] spawnPoints;
    [Tooltip("Üst kat/köprü spawn'ları — menzilli (zırhlı) tip öncelikle buradan doğar, köprüden baskı kurar.")]
    [SerializeField] Transform[] elevatedSpawnPoints;
    [SerializeField] [Range(0f,1f)] float elevatedChance = 0.7f;

    [Header("Düşman Prefabları")]
    [SerializeField] GameObject smallEnemy;
    [SerializeField] GameObject bigEnemy;
    [SerializeField] GameObject armoredEnemy;
    [SerializeField] GameObject fastEnemy;
    [Tooltip("Sıçrayıcı (EnemyAI.isJumper işaretli varyant). Boşsa yerine küçük düşman doğar.")]
    [SerializeField] GameObject jumperEnemy;
    [Tooltip("Şişkin/Patlayıcı (EnemyExplosive'li varyant). Boşsa yerine küçük düşman doğar.")]
    [SerializeField] GameObject exploderEnemy;

    [Header("Heat / Escalation")]
    [SerializeField] float timeHeatRate     = 0.05f;   // saniyede heat artışı (oyalanma cezası)
    [SerializeField] float terminalHeatStep = 1.5f;    // her terminal +heat
    [SerializeField] float heatMax          = 10f;     // ölçekleme tavanı
    [SerializeField] float baseSpawnInterval = 3.5f;
    [SerializeField] float minSpawnInterval  = 0.8f;
    [SerializeField] int   baseAliveCap      = 6;
    [SerializeField] int   maxAliveCap       = 20;

    [Header("Bitiş")]
    [SerializeField] int        totalTerminals = 4;
    [SerializeField] GameObject exitBarrier;

    float elapsed;
    int   terminalsDone;
    float spawnTimer;
    int   aliveCount;
    bool  finished;

    void Awake() => Instance = this;

    void Start()
    {
        if (exitBarrier) exitBarrier.SetActive(true);
    }

    void Update()
    {
        if (finished) return;

        elapsed += Time.deltaTime;
        float heat = timeHeatRate * elapsed + terminalsDone * terminalHeatStep;
        float t    = Mathf.Clamp01(heat / heatMax);

        float interval = Mathf.Lerp(baseSpawnInterval, minSpawnInterval, t);
        int   cap      = Mathf.RoundToInt(Mathf.Lerp(baseAliveCap, maxAliveCap, t));

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f && aliveCount < cap)
        {
            spawnTimer = interval;
            Spawn(heat);
        }
    }

    void Spawn(float heat)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;
        GameObject prefab = PickEnemy(heat);
        if (prefab == null) return;

        // Menzilli tip üst kat spawn'larını tercih eder — yukarıda doğunca
        // EnemyRangedAttack menzildeyse zaten durur, kendiliğinden köprüden ateş eder
        var pool = spawnPoints;
        if (prefab == armoredEnemy && elevatedSpawnPoints != null && elevatedSpawnPoints.Length > 0
            && Random.value < elevatedChance)
            pool = elevatedSpawnPoints;

        var pt = pool[Random.Range(0, pool.Length)];
        var go = Instantiate(prefab, pt.position, pt.rotation);
        aliveCount++;

        // Spawn marker tam NavMesh üzerine denk gelmeyebilir (bake sonrası birkaç
        // cm sapma bile "isOnNavMesh=false" yapıp düşmanı olduğu yerde donduruyordu
        // — DoChase sessizce hiçbir şey yapmadan dönüyordu). En yakın geçerli
        // noktaya yapıştırıp garantiye al.
        if (go.TryGetComponent(out NavMeshAgent navAgent) &&
            NavMesh.SamplePosition(pt.position, out NavMeshHit navHit, 4f, NavMesh.AllAreas))
            navAgent.Warp(navHit.position);

        if (go.TryGetComponent(out Health h))
            h.onDeath.AddListener(() => aliveCount = Mathf.Max(0, aliveCount - 1));

        // Takviye olarak doğuyor — devriye/görüş beklemeden doğrudan oyuncunun
        // peşine düşer (zaten alarmda, nerede olduğunu biliyor). Menzilli tipler
        // (behavior=Ranged) bu durumda bile DoRangedChase/GetChaseMove ile
        // mesafesini koruyor — Chase, sadece Melee tipler için "üstüne gelmek"
        // anlamına geliyor.
        if (go.TryGetComponent(out EnemyAI ai))
            ai.AlertNow();
    }

    // Bu bölümde sadece 3 tip: küçük (melee grunt), zırhlı (menzilli), büyük (ağır melee).
    // Heat arttıkça önce zırhlı, sonra büyük katılır; erken dalga küçük ağırlıklı.
    // (fast/jumper/exploder alanları kullanılmıyor — Inspector'da boş kalabilir.)
    GameObject PickEnemy(float heat)
    {
        float r = Random.value;
        if (heat < 2f) return smallEnemy;                                 // giriş: sadece küçük
        if (heat < 5f) return r < 0.70f ? smallEnemy : Or(armoredEnemy);  // zırhlı katılır
        return r < 0.45f ? smallEnemy
             : r < 0.75f ? Or(armoredEnemy)
             :             Or(bigEnemy);                                  // büyük ~%25
    }

    // Atanmamış tip yerine küçük düşman — spawn hakkı boşa gitmesin
    GameObject Or(GameObject pick) => pick != null ? pick : smallEnemy;

    // DataTerminal tamamlanınca çağırır
    public void OnTerminalDone()
    {
        terminalsDone++;
        if (terminalsDone >= totalTerminals) Finish();
    }

    void Finish()
    {
        finished = true;
        if (exitBarrier) exitBarrier.SetActive(false);   // çıkış açılır
    }

    // Null prefab varsa spawn atlar; eksik prefab'lar için güvenli
}
}
