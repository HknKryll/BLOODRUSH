using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Final sahnesi (Ch6): son UploadTerminal'in onComplete event'ine
// Begin() bağlanır. Upload %100 → slow-mo → beyaz fade → kapanış
// metni → BLOODRUSH → herhangi bir tuş → ana menü.
using Bloodrush.Player;
using Bloodrush.Enemy;

namespace Bloodrush.Flow
{
public class EndingSequence : MonoBehaviour
{
    [SerializeField] float slowMoDuration   = 1.5f;
    [SerializeField] float whiteFadeDuration = 3f;
    [SerializeField] float charDelay        = 0.05f;

    static readonly string[] Lines =
    {
        "Yükleme tamamlandı.",
        "",
        "Sırları artık herkes biliyor.",
        "",
        "Beden bitti — ama iş bitti."
    };

    bool started;

    public void Begin()
    {
        if (started) return;
        started = true;
        StartCoroutine(PlayEnding());
    }

    IEnumerator PlayEnding()
    {
        // Düşmanları durdur
        foreach (var enemy in FindObjectsOfType<EnemyAI>())
        {
            if (enemy.TryGetComponent(out NavMeshAgent agent) && agent.enabled)
                agent.ResetPath();
            enemy.enabled = false;
        }

        // Oyuncu silahını sustur
        var shoot = FindObjectOfType<PlayerShoot>();
        if (shoot) shoot.enabled = false;
        var hook = FindObjectOfType<GrapplingHook>();
        if (hook) hook.enabled = false;

        // Kademeli slow-mo
        float elapsed = 0f;
        while (elapsed < slowMoDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(1f, 0.2f, elapsed / slowMoDuration);
            yield return null;
        }

        // Beyaza fade
        var img = GameFlow.CreateOverlay(Color.white);
        elapsed = 0f;
        while (elapsed < whiteFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            img.color = new Color(1f, 1f, 1f, elapsed / whiteFadeDuration);
            yield return null;
        }

        var movement = FindObjectOfType<PlayerMovement>();
        if (movement) movement.enabled = false;

        // Kapanış metni — beyaz zemin üstüne koyu daktilo
        var canvas = img.canvas;
        var txt = new GameObject("EndText").AddComponent<Text>();
        txt.transform.SetParent(canvas.transform, false);
        txt.font        = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize    = 22;
        txt.color       = new Color(0.15f, 0.15f, 0.15f, 1f);
        txt.alignment   = TextAnchor.MiddleCenter;
        txt.lineSpacing = 1.4f;
        var txtRT = txt.GetComponent<RectTransform>();
        txtRT.anchorMin = new Vector2(0.12f, 0.25f);
        txtRT.anchorMax = new Vector2(0.88f, 0.85f);
        txtRT.offsetMin = txtRT.offsetMax = Vector2.zero;

        string full = "";
        foreach (var line in Lines)
        {
            foreach (char c in line)
            {
                full += c;
                txt.text = full;
                yield return new WaitForSecondsRealtime(charDelay);
            }
            full += "\n";
            txt.text = full;
            yield return new WaitForSecondsRealtime(0.35f);
        }

        yield return new WaitForSecondsRealtime(1.2f);

        // BLOODRUSH damgası
        var title = new GameObject("Title").AddComponent<Text>();
        title.transform.SetParent(canvas.transform, false);
        title.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title.fontSize  = 56;
        title.fontStyle = FontStyle.Bold;
        title.color     = new Color(0.8f, 0.05f, 0.05f, 1f);
        title.alignment = TextAnchor.MiddleCenter;
        title.text      = "BLOODRUSH";
        var titleRT = title.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 0.05f);
        titleRT.anchorMax = new Vector2(1f, 0.3f);
        titleRT.offsetMin = titleRT.offsetMax = Vector2.zero;

        // Herhangi bir tuş → ana menü
        yield return new WaitForSecondsRealtime(0.5f);
        while (!Input.anyKeyDown) yield return null;

        Time.timeScale   = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        SceneManager.LoadScene("MainMenu");
    }
}
}
