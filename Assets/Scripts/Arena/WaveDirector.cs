using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using TMPro;
using Bloodrush.Shared;
using Bloodrush.Shared.Audio;
using Bloodrush.Player;
using Bloodrush.Enemy;

// Birlesik dalga/spawn sistemi — eski Assets/Scripts/Arena/WaveManager.cs
// (sabit sayida dalga, Arena) ile eski Assets/Scripts/Flow/WaveDirector.cs
// (heat-bazli surekli eskalasyon, Ch3 Server Core) tek sinifta birlesti
// (bkz. BLOODRUSH_YENIDEN_YAPILANDIRMA_PLANI.md, Faz 5).
//
// Hangi modun calisacagini `data.hasFixedWaveCount` belirler — iki mod da
// KENDI ORIJINAL algoritmasini AYNEN korur (bilerek genellenmedi, bkz. Faz 5
// notu): sabit-dalga modu coroutine+event tabanli (Health.onDeath dinler),
// heat modu Update()'te surekli calisir. Ortak olan sadece: Instance
// singleton'i, SfxPlayer, ve WaveData'dan okunan parametreler.
namespace Bloodrush.Arena
{
public class WaveDirector : MonoBehaviour
{
    public static WaveDirector Instance { get; private set; }

    [Header("Veri")]
    [SerializeField] WaveData data;

    [Header("Spawn Noktaları")]
    [SerializeField] Transform[] spawnPoints;
    [Tooltip("Sadece heat modunda kullanılır — üst kat/köprü spawn'ları. Menzilli (zırhlı) " +
             "tip öncelikle buradan doğar, köprüden baskı kurar.")]
    [SerializeField] Transform[] elevatedSpawnPoints;

    [Header("Düşman Prefabları — Sabit Dalga Modu")]
    [Tooltip("Sadece data.hasFixedWaveCount=true iken kullanılır — dalga başına rastgele seçilir.")]
    [SerializeField] GameObject[] fixedWaveEnemyPrefabs;

    [Header("Düşman Prefabları — Heat Modu")]
    [Tooltip("Sadece data.hasFixedWaveCount=false iken kullanılır. Bu bölümde sadece 3 tip: " +
             "küçük (melee grunt), zırhlı (menzilli), büyük (ağır melee). Heat arttıkça önce " +
             "zırhlı, sonra büyük katılır; erken dalga küçük ağırlıklı. (fast/jumper/exploder " +
             "alanları kullanılmıyor — Inspector'da boş kalabilir.)")]
    [SerializeField] GameObject smallEnemy;
    [SerializeField] GameObject bigEnemy;
    [SerializeField] GameObject armoredEnemy;
    [SerializeField] GameObject fastEnemy;
    [SerializeField] GameObject jumperEnemy;
    [SerializeField] GameObject exploderEnemy;

    [Header("Oyuncu — Sabit Dalga Modu")]
    [SerializeField] Health      playerHealth;
    [SerializeField] PlayerShoot playerShoot;

    [Header("UI — Sabit Dalga Modu")]
    [SerializeField] TextMeshProUGUI waveText;
    [SerializeField] TextMeshProUGUI enemyCountText;
    [SerializeField] TextMeshProUGUI messageText;
    [SerializeField] GameObject      victoryPanel;
    [SerializeField] GameObject      defeatPanel;

    [Header("Bitiş — Heat Modu")]
    [SerializeField] GameObject exitBarrier;

    SfxPlayer sfx;
    bool FixedMode => data != null && data.hasFixedWaveCount;

    // GameHUD'un "VERİ x/y" satırı sadece heat modunda görünmeli (bkz. GameHUD.UpdateVeriRow).
    public bool IsFixedWaveMode => FixedMode;

    void Awake() => Instance = this;

    IEnumerator Start()
    {
        sfx = SfxPlayer.Create(gameObject, spatialBlend: 0f);

        if (FixedMode) yield return StartFixedWave();
        else           StartHeat();
    }

    void Update()
    {
        if (!FixedMode) UpdateHeat();
    }

    // ═══════════════════════ Sabit Dalga Modu (eski WaveManager) ═══════════════════════

    int  currentWave;
    int  aliveEnemies;
    bool gameEnded;

    public static bool IsGameOver { get; private set; }

    IEnumerator StartFixedWave()
    {
        IsGameOver = false;

        if (victoryPanel) victoryPanel.SetActive(false);
        if (defeatPanel)  defeatPanel.SetActive(false);
        if (messageText)  messageText.text = "";

        yield return new WaitForSeconds(0.2f);

        if (playerHealth != null)
            playerHealth.onDeath.AddListener(OnPlayerDied);

        StartNextWave();
    }

    void StartNextWave()
    {
        currentWave++;

        if (currentWave > data.maxWaves)
        {
            EndGame(true);
            return;
        }

        aliveEnemies = 0;
        int count = data.baseEnemyCount + (currentWave - 1) * data.enemyCountPerWave;

        for (int i = 0; i < count; i++)
            SpawnFixedWaveEnemy();

        if (messageText) messageText.text = $"WAVE {currentWave}";
        sfx.Play(data.waveStartClip, data.waveStartVolume);
        StartCoroutine(ClearMessage(2f));
        UpdateUI();
    }

    void SpawnFixedWaveEnemy()
    {
        if (fixedWaveEnemyPrefabs == null || fixedWaveEnemyPrefabs.Length == 0) return;
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        Vector3 pos    = spawnPoints[Random.Range(0, spawnPoints.Length)].position;
        var     prefab = fixedWaveEnemyPrefabs[Random.Range(0, fixedWaveEnemyPrefabs.Length)];
        var     go     = Instantiate(prefab, pos, Quaternion.identity);

        var h = go.GetComponent<Health>();
        if (h != null) h.onDeath.AddListener(OnFixedWaveEnemyDied);

        aliveEnemies++;
    }

    void OnFixedWaveEnemyDied()
    {
        aliveEnemies--;
        UpdateUI();

        if (aliveEnemies > 0) return;

        playerShoot?.RefillWave();

        if (currentWave >= data.maxWaves)
            EndGame(true);
        else
            StartCoroutine(NextWaveCountdown());
    }

    void OnPlayerDied()
    {
        if (!gameEnded) EndGame(false);
    }

    IEnumerator NextWaveCountdown()
    {
        for (int i = (int)data.timeBetweenWaves; i > 0; i--)
        {
            if (messageText) messageText.text = $"NEXT WAVE  {i}";
            yield return new WaitForSeconds(1f);
        }
        if (messageText) messageText.text = "";
        StartNextWave();
    }

    IEnumerator ClearMessage(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (messageText && !gameEnded) messageText.text = "";
    }

    void EndGame(bool victory)
    {
        gameEnded  = true;
        IsGameOver = true;
        sfx.Play(victory ? data.victoryClip : data.defeatClip, victory ? data.victoryVolume : data.defeatVolume);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        if (victoryPanel) victoryPanel.SetActive(victory);
        if (defeatPanel)  defeatPanel.SetActive(!victory);
    }

    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    void UpdateUI()
    {
        if (waveText)       waveText.text       = $"WAVE  {currentWave} / {data.maxWaves}";
        if (enemyCountText) enemyCountText.text  = $"ENEMIES  {aliveEnemies}";
    }

    // ═══════════════════════ Heat Modu (eski Flow/WaveDirector) ═══════════════════════

    float elapsed;
    int   terminalsDone;
    float spawnTimer;
    int   aliveCount;
    bool  finished;

    // HUD "VERİ x/y" sayacı için (bkz. GameHUD)
    public int TerminalsDone  => terminalsDone;
    public int TotalTerminals => data != null ? data.totalTerminals : 0;

    // Tüm terminaller bitti mi? (SequenceLock puzzle kapısını buna göre açar)
    public bool IsFinished => finished;

    void StartHeat()
    {
        if (exitBarrier) exitBarrier.SetActive(true);
    }

    void UpdateHeat()
    {
        if (finished) return;

        elapsed += Time.deltaTime;
        float heat = data.timeHeatRate * elapsed + terminalsDone * data.terminalHeatStep;
        float t    = Mathf.Clamp01(heat / data.heatMax);

        float interval = Mathf.Lerp(data.baseSpawnInterval, data.minSpawnInterval, t);
        int   cap      = Mathf.RoundToInt(Mathf.Lerp(data.baseAliveCap, data.maxAliveCap, t));

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f && aliveCount < cap)
        {
            spawnTimer = interval;
            SpawnHeatEnemy(heat);
        }
    }

    void SpawnHeatEnemy(float heat)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;
        GameObject prefab = PickHeatEnemy(heat);
        if (prefab == null) return;

        // Menzilli tip üst kat spawn'larını tercih eder — yukarıda doğunca
        // EnemyRangedAttack menzildeyse zaten durur, kendiliğinden köprüden ateş eder
        var pool = spawnPoints;
        if (prefab == armoredEnemy && elevatedSpawnPoints != null && elevatedSpawnPoints.Length > 0
            && Random.value < data.elevatedSpawnChance)
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

    GameObject PickHeatEnemy(float heat)
    {
        float r = Random.value;
        if (heat < 2f) return smallEnemy;                                     // giriş: sadece küçük
        if (heat < 5f) return r < 0.70f ? smallEnemy : OrSmall(armoredEnemy); // zırhlı katılır
        return r < 0.45f ? smallEnemy
             : r < 0.75f ? OrSmall(armoredEnemy)
             :             OrSmall(bigEnemy);                                 // büyük ~%25
    }

    // Atanmamış tip yerine küçük düşman — spawn hakkı boşa gitmesin
    GameObject OrSmall(GameObject pick) => pick != null ? pick : smallEnemy;

    // DataTerminal tamamlanınca çağırır
    public void OnTerminalDone()
    {
        terminalsDone++;
        if (terminalsDone >= data.totalTerminals) FinishHeat();
    }

    void FinishHeat()
    {
        finished = true;
        if (exitBarrier) exitBarrier.SetActive(false);   // çıkış açılır
    }

    // Null prefab varsa spawn atlar; eksik prefab'lar için güvenli
}
}
