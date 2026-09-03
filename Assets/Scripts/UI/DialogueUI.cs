using UnityEngine;
using UnityEngine.UI;

namespace Bloodrush.UI
{
// Diyalog + etkileşim yazısı UI'si. Kendi canvas'ını kurar (sahneye bir şey koymazsın),
// statik metodlarla sürülür. NpcDialogue kullanır. BossHealthUI/Notification deseni.
public class DialogueUI : MonoBehaviour
{
    static DialogueUI instance;

    GameObject boxRoot;
    Image      promptIcon;
    Text       promptLabel, speakerLabel, lineLabel;

    static DialogueUI Ensure()
    {
        if (instance == null)
            instance = new GameObject("DialogueUI").AddComponent<DialogueUI>();
        return instance;
    }

    // Prompt'u en son KİM gösterdi. Sahnede birden fazla etkileşim (NPC, koltuk, kitap)
    // aynı anda Update çalıştırdığı için, menzil dışındaki biri her frame HidePrompt
    // çağırırsa menzildeki başkasının prompt'unu söndürüyordu — bu yüzden sahip takibi var.
    static Object promptOwner;

    public static void ShowPrompt(string text, Object owner = null, Sprite icon = null)
    {
        var i = Ensure();
        i.promptLabel.text    = text;
        i.promptLabel.enabled = true;
        i.promptIcon.sprite   = icon;
        i.promptIcon.enabled  = icon != null;
        promptOwner = owner;
    }

    // owner verilirse: SADECE prompt'u gösteren o ise gizler (başkasınınkini söndürmez).
    public static void HidePrompt(Object owner = null)
    {
        if (owner != null && promptOwner != null && promptOwner != owner) return;
        if (instance != null)
        {
            instance.promptLabel.enabled = false;
            instance.promptIcon.enabled  = false;
        }
        promptOwner = null;
    }

    public static void ShowLine(string speaker, string line)
    {
        var i = Ensure();
        i.promptLabel.enabled = false;
        i.speakerLabel.text   = speaker;
        i.lineLabel.text      = line;
        i.boxRoot.SetActive(true);
    }

    public static void HideLine()
    {
        if (instance != null) instance.boxRoot.SetActive(false);
    }

    void Awake()
    {
        instance = this;
        Build();
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        // Etkileşim yazısı ("[E] Konuş") — ekran ortasının biraz altında. Kutu ekranda
        // ortalı sabit bir alan; içine önce ikon (varsa), sonra soldan başlayan metin
        // konur — ikon yoksa metin sadece biraz sola kaymış görünür, sorun değil.
        var promptGroup = new GameObject("PromptGroup");
        promptGroup.transform.SetParent(transform, false);
        var pgRT = promptGroup.AddComponent<RectTransform>();
        pgRT.anchorMin = new Vector2(0.3f, 0.30f);
        pgRT.anchorMax = new Vector2(0.7f, 0.36f);
        pgRT.offsetMin = pgRT.offsetMax = Vector2.zero;

        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(promptGroup.transform, false);
        promptIcon = iconGo.AddComponent<Image>();
        promptIcon.raycastTarget = false;
        promptIcon.preserveAspect = true;
        var iconRT = promptIcon.rectTransform;
        iconRT.anchorMin = new Vector2(0f, 0.1f);
        iconRT.anchorMax = new Vector2(0.13f, 0.9f);
        iconRT.offsetMin = iconRT.offsetMax = Vector2.zero;
        promptIcon.enabled = false;

        promptLabel = MakeText("Prompt", promptGroup.transform, 26, new Color(1f, 1f, 1f, 0.92f), TextAnchor.MiddleLeft,
            new Vector2(0.17f, 0f), new Vector2(1f, 1f));
        promptLabel.fontStyle = FontStyle.Bold;
        promptLabel.enabled   = false;

        // Diyalog kutusu (alt) — arka pano + konuşan + satır + [E] ipucu
        boxRoot = new GameObject("Box");
        boxRoot.transform.SetParent(transform, false);
        var boxRT = boxRoot.AddComponent<RectTransform>();
        boxRT.anchorMin = new Vector2(0.14f, 0.07f);
        boxRT.anchorMax = new Vector2(0.86f, 0.27f);
        boxRT.offsetMin = boxRT.offsetMax = Vector2.zero;
        var bg = boxRoot.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.06f, 0.08f, 0.9f);

        speakerLabel = MakeText("Speaker", boxRoot.transform, 22, new Color(0.35f, 0.75f, 1f, 1f), TextAnchor.UpperLeft,
            new Vector2(0.03f, 0.62f), new Vector2(0.97f, 0.94f));
        speakerLabel.fontStyle = FontStyle.Bold;

        lineLabel = MakeText("Line", boxRoot.transform, 22, new Color(0.95f, 0.95f, 0.95f, 1f), TextAnchor.UpperLeft,
            new Vector2(0.03f, 0.14f), new Vector2(0.97f, 0.62f));

        var hint = MakeText("Hint", boxRoot.transform, 16, new Color(1f, 1f, 1f, 0.5f), TextAnchor.LowerRight,
            new Vector2(0.5f, 0.03f), new Vector2(0.97f, 0.16f));
        hint.text = "[E]";

        boxRoot.SetActive(false);
    }

    Text MakeText(string name, Transform parent, int size, Color color, TextAnchor align, Vector2 aMin, Vector2 aMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize      = size;
        t.color         = color;
        t.alignment     = align;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow   = VerticalWrapMode.Overflow;
        var rt = t.rectTransform;
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return t;
    }
}
}
