using UnityEngine;
using Bloodrush.UI;
using Bloodrush.Player;

namespace Bloodrush.Flow
{
// NPC diyaloğu: oyuncu yaklaşıp NPC'ye bakınca "[E] Konuş" çıkar; E'ye basınca satırlar
// SIRAYLA gösterilir (E ile ilerler, son satırda kapanır). Uzaklaşınca kapanır.
//
// TEKRAR: İlk TAM konuşmadan sonra tekrar konuşulunca sadece SON cümle tekrarlanır.
// Oyuncu üst üste (repeatThreshold kadar) konuşursa ÖZEL bir cümle çıkar (annoyedLine),
// sonra sayaç sıfırlanır (tekrar tetiklenebilir). Metinleri SEN yazarsın.
//
// Bileşeni herhangi bir NPC objesine ekle: kendi modelin ya da placeholder bir Capsule.
public class NpcDialogue : MonoBehaviour
{
    [Header("Kimlik")]
    [SerializeField] string speakerName = "RESEPSİYONİST";

    [Header("Diyalog — satırları SEN yaz (E ile ilerler)")]
    [TextArea(2, 5)]
    [SerializeField] string[] lines =
    {
        "Konsey'e hoş geldiniz, Aday.",
        "Kaydınız yapılıyor... lütfen bekleyin."
    };

    [Header("Tekrar Konuşma")]
    [Tooltip("Bu kadar kez üst üste tekrar konuşulunca özel cümle (annoyedLine) çıkar. 0 = kapalı.")]
    [SerializeField] int repeatThreshold = 5;
    [TextArea(2, 4)]
    [Tooltip("repeatThreshold kadar üstelenince çıkacak ÖZEL cümle — SEN yaz.")]
    [SerializeField] string annoyedLine = "Başka bir sorunuz yoksa lütfen bekleme alanında oturun, Aday.";

    [Header("Etkileşim")]
    [SerializeField] KeyCode talkKey    = KeyCode.E;
    [SerializeField] string  promptText = "[E] Konuş";
    [SerializeField] float   range      = 3f;
    [Tooltip("1 = tam NPC'ye bakınca, 0 = her açı. 0.5 civarı iyi.")]
    [SerializeField] float   lookDot    = 0.5f;
    [Tooltip("Bakış/mesafe hedefi NPC pivotundan bu kadar yukarısı (baş hizası).")]
    [SerializeField] float   headOffset = 1.5f;

    [Header("Animasyon (opsiyonel)")]
    [Tooltip("Boş bırakılırsa child'lardan otomatik bulunur. Yoksa animasyon atlanır.")]
    [SerializeField] Animator animator;
    [Tooltip("Animator'daki bool parametre adı — konuşurken true olur (Idle↔Talking geçişi).")]
    [SerializeField] string   talkingBool = "Talking";

    Camera   cam;
    string[] activeLines;
    int      index;
    bool     talking;
    bool     firstDone;      // ilk TAM konuşma tamamlandı mı
    int      repeatCount;    // firstDone sonrası üst üste tekrar sayısı

    bool hasTalkingParam;

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();

        // Parametre gerçekten var mı? (Sadece idle'lı basit controller'da yok —
        // kontrol etmezsek Unity her çağrıda "parameter does not exist" uyarısı basar.)
        if (animator != null && !string.IsNullOrEmpty(talkingBool))
            foreach (var prm in animator.parameters)
                if (prm.type == AnimatorControllerParameterType.Bool && prm.name == talkingBool)
                { hasTalkingParam = true; break; }
    }

    // Konuşma animasyonunu aç/kapa (Talking parametresi yoksa sessizce atlanır)
    void SetTalkingAnim(bool on)
    {
        if (hasTalkingParam) animator.SetBool(talkingBool, on);
    }

    void Update()
    {
        if (cam == null) { cam = Camera.main; if (cam == null) return; }

        Vector3 aim  = transform.position + Vector3.up * headOffset;
        Vector3 to   = aim - cam.transform.position;
        float   dist = to.magnitude;
        float   dot  = dist > 0.001f ? Vector3.Dot(cam.transform.forward, to / dist) : 1f;
        bool    inRange = dist <= range && dot >= lookDot;

        if (talking)
        {
            if (!inRange) { Close(); return; }               // uzaklaşınca kapat (firstDone bozulmaz)
            if (KeyBindings.DownKey(talkKey)) Advance();
            return;
        }

        if (inRange)
        {
            DialogueUI.ShowPrompt(promptText, this, UIIcons.Chat);
            if (KeyBindings.DownKey(talkKey)) Begin();
        }
        else
        {
            // owner verilir: menzil dışındayken BAŞKA bir etkileşimin (koltuk/kitap)
            // prompt'unu söndürmesin — sadece kendi gösterdiğini gizler.
            DialogueUI.HidePrompt(this);
        }
    }

    void Begin()
    {
        activeLines = SelectLines();
        if (activeLines == null || activeLines.Length == 0) return;
        talking = true;
        index   = 0;
        SetTalkingAnim(true);                          // Idle → Talking
        DialogueUI.ShowLine(speakerName, activeLines[0]);
    }

    // Hangi satır seti oynanacak?
    string[] SelectLines()
    {
        if (lines == null || lines.Length == 0) return null;

        if (!firstDone) return lines;                          // ilk kez: tüm diyalog

        repeatCount++;
        if (repeatThreshold > 0 && repeatCount >= repeatThreshold && !string.IsNullOrEmpty(annoyedLine))
        {
            repeatCount = 0;                                   // döngü baştan
            return new[] { annoyedLine };                      // özel cümle
        }
        return new[] { lines[lines.Length - 1] };              // sadece son cümle
    }

    void Advance()
    {
        index++;
        if (index >= activeLines.Length) { Finish(); return; }
        DialogueUI.ShowLine(speakerName, activeLines[index]);
    }

    void Finish()   // son satırdan sonra E → doğal bitiş
    {
        if (activeLines == lines) firstDone = true;            // ilk TAM konuşma tamamlandı
        Close();
    }

    void Close()    // bitiş ya da uzaklaşma
    {
        talking     = false;
        activeLines = null;
        SetTalkingAnim(false);                         // Talking → Idle
        DialogueUI.HideLine();
    }

    void OnDisable()
    {
        if (talking) Close();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * headOffset, range);
    }
}
}
