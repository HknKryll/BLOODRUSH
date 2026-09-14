using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Bloodrush.Player;

namespace Bloodrush.Enemy
{
// Dusman AI teshis kaplamasi — SUREKLI GORUNUR, tus girdisi YOK.
//
// NEDEN OnGUI: IMGUI, projede hangi Input System (eski/yeni/ikisi) aktif olursa olsun
// calisir ve hicbir tusa bagli degildir. Kullanicinin sarti buydu.
//
// Kendini [RuntimeInitializeOnLoadMethod] ile kurar — sahneye ya da prefab'a dokunmak
// gerekmez. Kapatmak icin: Hierarchy'de "EnemyDebugOverlay" objesini devre disi birak
// ya da bu dosyayi sil.
public class EnemyDebugOverlay : MonoBehaviour
{
    [Tooltip("Oyun icinde F3 ile acilip kapanir. Varsayilan KAPALI.")]
    [SerializeField] bool show = false;
    [SerializeField] KeyCode legacyToggleKey = KeyCode.F3;
    [Tooltip("Bu mesafeden uzaktaki dusmanlar listelenmez (0 = hepsi).")]
    [SerializeField] float maxDistance = 60f;

    static EnemyDebugOverlay instance;

    readonly List<EnemyAI> buffer = new List<EnemyAI>();
    float   refreshAt;
    Camera  cam;
    Transform player;

    GUIStyle panelStyle, labelStyle, worldStyle;
    Texture2D panelTex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("EnemyDebugOverlay");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<EnemyDebugOverlay>();
    }

    // F3 — HER IKI INPUT SISTEMINI DE DESTEKLER.
    //
    // ProjectSettings'te activeInputHandler = 2 (Both), yani derleyicide
    // ENABLE_INPUT_SYSTEM ve ENABLE_LEGACY_INPUT_MANAGER sembollerinin IKISI de tanimli.
    // Ayar ileride "New" ya da "Old" olarak degistirilse bile bu kod derlenmeye ve
    // calismaya devam eder — hangi sembol tanimliysa o yol devreye girer.
    bool TogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.f3Key.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(legacyToggleKey)) return true;
#endif
        return false;
    }

    void Update()
    {
        if (TogglePressed())
        {
            show = !show;
            // Overlay cizilmese bile tusun ateslendigini buradan gorursun.
            Debug.Log($"[EnemyDebugOverlay] Görünürlük: {(show ? "AÇIK" : "KAPALI")} (F3)");
        }

        if (!show) return;

        // Dusman listesini surekli taramak pahali — yarim saniyede bir tazele.
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 0.5f;

        buffer.Clear();
        buffer.AddRange(FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));

        if (cam == null) cam = Camera.main;
        if (player == null)
        {
            var pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null) player = pm.transform;
        }
    }

    void EnsureStyles()
    {
        if (panelStyle != null) return;

        panelTex = new Texture2D(1, 1);
        panelTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
        panelTex.Apply();

        panelStyle = new GUIStyle { normal = { background = panelTex } };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 12,
            richText  = true,
            alignment = TextAnchor.UpperLeft,
        };
        labelStyle.normal.textColor = Color.white;

        worldStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 11,
            richText  = true,
            alignment = TextAnchor.MiddleCenter,
        };
        worldStyle.normal.textColor = Color.white;
    }

    void OnGUI()
    {
        if (!show || buffer.Count == 0) return;
        EnsureStyles();
        if (cam == null) cam = Camera.main;

        // ── Sol ust panel ──
        float w = 430f, lineH = 15f;
        int shown = 0;
        foreach (var e in buffer) if (e != null && InRange(e)) shown++;

        float h = 34f + shown * (lineH * 2f + 4f);
        GUI.Box(new Rect(8f, 8f, w, h), GUIContent.none, panelStyle);

        var area = new Rect(16f, 14f, w - 16f, h);
        GUILayout.BeginArea(area);
        GUILayout.Label("<b>DÜŞMAN AI — CANLI TEŞHİS</b>", labelStyle);

        foreach (var e in buffer)
        {
            if (e == null || !InRange(e)) continue;

            string pathTxt  = Colored(e.PathStatus.ToString(), PathColor(e.PathStatus));
            string meshTxt  = Colored(e.OnNavMesh ? "onMesh" : "OFF-MESH",
                                      e.OnNavMesh ? "#8fe38f" : "#ff6b6b");
            Vector3 d = e.Destination;
            float   pd = player != null ? Vector3.Distance(e.transform.position, player.position) : -1f;

            GUILayout.Label(
                $"<b>{e.name}</b>  ·  state <b>{e.DebugState}</b>  ·  {meshTxt}  ·  path {pathTxt}",
                labelStyle);
            GUILayout.Label(
                $"    dest ({d.x:0.0}, <b>{d.y:0.0}</b>, {d.z:0.0})   ownY {e.transform.position.y:0.0}   " +
                $"oyuncuya {pd:0.0} m   vel {e.AgentSpeed:0.0}   anim {e.AnimSpeed:0.0}   " +
                $"warp {e.NavRecoverCount}",
                labelStyle);
        }
        GUILayout.EndArea();

        // ── Dusmanlarin ustunde dunya etiketi ──
        if (cam == null) return;
        foreach (var e in buffer)
        {
            if (e == null || !InRange(e)) continue;

            Vector3 sp = cam.WorldToScreenPoint(e.transform.position + Vector3.up * 2.2f);
            if (sp.z <= 0f) continue;   // kameranin arkasi

            string txt = $"{e.DebugState} · {(e.OnNavMesh ? "mesh" : "OFF")} · " +
                         $"{Short(e.PathStatus)} · y{e.Destination.y:0.0}";
            var rect = new Rect(sp.x - 110f, Screen.height - sp.y - 10f, 220f, 20f);
            GUI.Box(rect, GUIContent.none, panelStyle);
            GUI.Label(rect, Colored(txt, PathColor(e.PathStatus)), worldStyle);
        }
    }

    bool InRange(EnemyAI e)
    {
        if (maxDistance <= 0f || player == null) return true;
        return Vector3.Distance(e.transform.position, player.position) <= maxDistance;
    }

    static string Short(NavMeshPathStatus s) => s switch
    {
        NavMeshPathStatus.PathComplete => "Complete",
        NavMeshPathStatus.PathPartial  => "PARTIAL",
        _                              => "INVALID",
    };

    static string PathColor(NavMeshPathStatus s) => s switch
    {
        NavMeshPathStatus.PathComplete => "#8fe38f",
        NavMeshPathStatus.PathPartial  => "#ffd166",   // NavMesh adasi suphesi
        _                              => "#ff6b6b",
    };

    static string Colored(string text, string hex) => $"<color={hex}>{text}</color>";

    void OnDestroy()
    {
        if (panelTex != null) Destroy(panelTex);
        if (instance == this) instance = null;
    }
}
}
