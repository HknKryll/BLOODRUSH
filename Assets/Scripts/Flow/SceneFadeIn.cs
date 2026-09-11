using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Bloodrush.Player;

namespace Bloodrush.Flow
{
// Sahne geçişinde karartmayı KARŞI TARAFA taşır. GameFlow.CreateOverlay normal bir sahne
// objesi üretiyor; SceneManager.LoadScene onu yok eder ve yeni sahne bir kare TAM
// PARLAKLIKTA patlar, ancak ondan sonra kararabilirdi. Bu bileşen overlay'i
// DontDestroyOnLoad ile taşıyıp yeni sahnede devralır: önce sessiz karanlık, sonra açılış.
//
// Yeni sahneye HİÇBİR ŞEY eklemen gerekmez — overlay kendi söndürücüsünü yanında taşır.
public class SceneFadeIn : MonoBehaviour
{
    Image img;
    float hold;
    float fadeDuration;
    bool  started;

    // Elevator, sahne yüklemeden HEMEN ÖNCE çağırır.
    public static void CarryOverlay(Image overlay, float hold, float fadeDuration)
    {
        if (overlay == null || overlay.canvas == null) return;

        var root = overlay.canvas.gameObject;
        root.transform.SetParent(null);          // DontDestroyOnLoad kök obje ister
        DontDestroyOnLoad(root);

        var fader = root.AddComponent<SceneFadeIn>();
        fader.img          = overlay;
        fader.hold         = hold;
        fader.fadeDuration = fadeDuration;
        SceneManager.sceneLoaded += fader.OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (started) return;
        started = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        if (img != null) img.color = Color.black;

        // Karanlıkta oyuncu kör kör yürümesin — yeni sahnenin oyuncusunu kısa süre dondur.
        var playerGo = GameObject.FindGameObjectWithTag("Player");
        var pm       = playerGo != null ? playerGo.GetComponent<PlayerMovement>() : null;
        if (pm != null) pm.enabled = false;

        // unscaled: sahne açılışında timeScale'in ne olduğuna güvenmiyoruz.
        if (hold > 0f) yield return new WaitForSecondsRealtime(hold);

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            if (img != null) img.color = new Color(0f, 0f, 0f, 1f - t / fadeDuration);
            yield return null;
        }

        if (pm != null) pm.enabled = true;
        Destroy(gameObject);
    }
}
}
