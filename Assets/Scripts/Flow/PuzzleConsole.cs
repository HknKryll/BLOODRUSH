using UnityEngine;
using UnityEngine.UI;
using Bloodrush.Player;

// Sıralı güvenlik kilidinin tek konsolu. Oyuncu yaklaşıp Etkileşim tuşuna (G,
// KeyBindings.Interact) basınca SequenceLock'a bildirir. Sahnedeki SequenceLock'a
// Start'ta kendini otomatik kaydeder — elle referans bağlamaya gerek yok.
namespace Bloodrush.Flow
{
[RequireComponent(typeof(BoxCollider))]
public class PuzzleConsole : MonoBehaviour
{
    [Header("Kimlik")]
    [Tooltip("Bu konsolun sabit indeksi (0,1,2...). SequenceLock hedef sırada bu indeksi kullanır.")]
    [SerializeField] int   index;
    [SerializeField] Color consoleColor = new Color(0.3f, 0.8f, 1f);

    [Header("Bu konsolun çağırdığı düşman (KULLANICI doldurur)")]
    [SerializeField] GameObject  enemyPrefab;
    [SerializeField] int         enemyCount = 4;
    [SerializeField] Transform[] spawnPoints;

    [Header("Görsel (opsiyonel — yoksa 'Glow' child aranır)")]
    [SerializeField] Renderer glow;

    public int          Index       => index;
    public Color        Color       => consoleColor;
    public GameObject   EnemyPrefab => enemyPrefab;
    public int          EnemyCount  => enemyCount;
    public Transform[]  SpawnPoints => spawnPoints;
    public bool         Activated   { get; private set; }

    static readonly Color DoneColor = new Color(0.2f, 1f, 0.3f);

    SequenceLock lockCtrl;
    bool  playerInside;
    Text  prompt;

    void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (glow == null)
        {
            var g = transform.Find("Glow");
            if (g) glow = g.GetComponent<Renderer>();
        }
        SetGlowColor(consoleColor * 0.4f);   // idle: sönük kendi rengi

        lockCtrl = FindFirstObjectByType<SequenceLock>();
        if (lockCtrl) lockCtrl.Register(this);

        BuildPrompt();
    }

    void OnTriggerEnter(Collider o) { if (o.GetComponentInParent<PlayerMovement>()) playerInside = true; }
    void OnTriggerExit(Collider o)  { if (o.GetComponentInParent<PlayerMovement>()) playerInside = false; }

    void Update()
    {
        bool show = playerInside && !Activated && lockCtrl != null && !lockCtrl.Finished;
        if (prompt)
        {
            if (show && !prompt.enabled) prompt.text = $"[{KeyBindings.Interact}] Etkinleştir";
            if (prompt.enabled != show) prompt.enabled = show;
        }

        if (show && KeyBindings.Down(KeyBindings.Action.Interact))
            lockCtrl.OnConsoleActivated(this);
    }

    // PuzzleRoomBuilder kurulum-zamanında çağırır (referansları bağlar)
    public void Configure(int idx, Color col, Transform[] spawns)
    {
        index        = idx;
        consoleColor = col;
        spawnPoints  = spawns;
    }

    // SequenceLock çağırır: doğru sırada aktive edilince yeşil, sıfırlanınca idle
    public void SetActivated(bool on)
    {
        Activated = on;
        SetGlowColor(on ? DoneColor : consoleColor * 0.4f);
    }

    void SetGlowColor(Color c)
    {
        if (!glow) return;
        var m = glow.material;
        if      (m.HasProperty("_UnlitColor"))   m.SetColor("_UnlitColor", c);
        else if (m.HasProperty("_EmissiveColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissiveColor", c); }
        else if (m.HasProperty("_BaseColor"))     m.SetColor("_BaseColor", c);
        else                                       m.color = c;
    }

    void BuildPrompt()
    {
        var cgo = new GameObject("ConsolePrompt");
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15;
        cgo.AddComponent<CanvasScaler>();

        var go = new GameObject("Label");
        go.transform.SetParent(cgo.transform, false);
        prompt = go.AddComponent<Text>();
        prompt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        prompt.fontSize      = 22;
        prompt.alignment     = TextAnchor.MiddleCenter;
        prompt.color         = new Color(1f, 1f, 1f, 0.9f);
        prompt.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.34f);
        rt.anchorMax = new Vector2(1f, 0.42f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        prompt.enabled = false;
    }
}
}
