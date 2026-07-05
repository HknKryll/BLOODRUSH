using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class WaveManager : MonoBehaviour
{
    [Header("Dalga Ayarları")]
    [SerializeField] int   maxWaves          = 10;
    [SerializeField] int   baseEnemyCount    = 3;
    [SerializeField] int   enemyCountPerWave = 1;
    [SerializeField] float timeBetweenWaves  = 5f;

    [Header("Spawn")]
    [SerializeField] Transform[]  spawnPoints;
    [SerializeField] GameObject[] enemyPrefabs;
    [SerializeField] Health       playerHealth;
    [SerializeField] PlayerShoot  playerShoot;

    [Header("UI")]
    [SerializeField] TextMeshProUGUI waveText;
    [SerializeField] TextMeshProUGUI enemyCountText;
    [SerializeField] TextMeshProUGUI messageText;
    [SerializeField] GameObject      victoryPanel;
    [SerializeField] GameObject      defeatPanel;

    int  currentWave;
    int  aliveEnemies;
    bool gameEnded;

    IEnumerator Start()
    {
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

        if (currentWave > maxWaves)
        {
            EndGame(true);
            return;
        }

        aliveEnemies = 0;
        int count = baseEnemyCount + (currentWave - 1) * enemyCountPerWave;

        for (int i = 0; i < count; i++)
            SpawnEnemy();

        if (messageText) messageText.text = $"WAVE {currentWave}";
        StartCoroutine(ClearMessage(2f));
        UpdateUI();
    }

    void SpawnEnemy()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return;
        if (spawnPoints  == null || spawnPoints.Length  == 0) return;

        Vector3 pos    = spawnPoints[Random.Range(0, spawnPoints.Length)].position;
        var     prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        var     go     = Instantiate(prefab, pos, Quaternion.identity);

        var h = go.GetComponent<Health>();
        if (h != null) h.onDeath.AddListener(OnEnemyDied);

        aliveEnemies++;
    }

    void OnEnemyDied()
    {
        aliveEnemies--;
        UpdateUI();

        if (aliveEnemies > 0) return;

        playerShoot?.RefillWave();

        if (currentWave >= maxWaves)
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
        for (int i = (int)timeBetweenWaves; i > 0; i--)
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
        gameEnded = true;
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
        if (waveText)       waveText.text       = $"WAVE  {currentWave} / {maxWaves}";
        if (enemyCountText) enemyCountText.text  = $"ENEMIES  {aliveEnemies}";
    }
}
