using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Bölüm sonu geçiş noktası: oyuncu girince fade → sıradaki sahne.
// Kullanım: boş GO + BoxCollider (isTrigger) + bu script.
using Bloodrush.Player;
using Bloodrush.Enemy;

namespace Bloodrush.Flow
{
[RequireComponent(typeof(BoxCollider))]
public class LevelExit : MonoBehaviour
{
    [Tooltip("Yüklenecek sahne. Boşsa sıradaki build index'i yüklenir. Sahne Build Settings'te olmalı!")]
    [SerializeField] string nextSceneName    = "";
    [SerializeField] bool  requireNoEnemies = false;  // true: sahnede canlı düşman varken çalışmaz
    [SerializeField] float fadeDuration    = 1f;

    bool leaving;

    void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (leaving) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;
        if (requireNoEnemies && FindObjectOfType<EnemyAI>() != null) return;

        leaving = true;
        StartCoroutine(ExitRoutine());
    }

    IEnumerator ExitRoutine()
    {
        var img = GameFlow.CreateOverlay(Color.black);
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            img.color = new Color(0f, 0f, 0f, elapsed / fadeDuration);
            yield return null;
        }

        Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(nextSceneName))
            SceneManager.LoadScene(nextSceneName);
        else
            GameFlow.LoadNext();
    }
}
}
