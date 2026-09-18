using UnityEngine;
using Bloodrush.UI;
using Bloodrush.Player;

namespace Bloodrush.Flow
{
// Karanlık Sekans'ın etkileşimli objeleri için ortak taban: yaklaşma + bakış açısı + prompt
// + tuş hakemi. InteractableBook'taki kanıtlanmış desenin aynısı.
//
// NOT — proje geleneğinden bilinçli sapma: bu projede her etkileşim script'i yakınlık
// mantığının kendi kopyasını taşıyor (NpcDialogue, InteractableBook, SeatInteractable).
// Burada TEK bir sekansta DÖRT etkileşim var (sigorta, panel, valf, raf); aynı 30 satırı
// dört kez kopyalamak yerine tabana aldım. Alt sınıflara sadece "ne yazsın" ve "ne olsun"
// kalıyor — istenen modülerlik de bu.
public abstract class ProximityInteractable : MonoBehaviour
{
    [Header("Etkileşim")]
    [Tooltip("E aynı zamanda varsayılan Kanca tuşu; çakışma görürsen F'ye çevir. " +
             "(KeyBindings.Interact = G ama proje geneli etkileşimlerde E kullanıyor.)")]
    [SerializeField] protected KeyCode interactKey = KeyCode.E;
    [Tooltip("AÇIK: hedef nokta objenin GÖRSEL merkezi (renderer bounds). Builder ile kurulan " +
             "objelerin pivotu genelde tabanda/yerde kalır; pivotu hedef almak, oyuncu gövdeye " +
             "baksa bile açının tutmamasına ve prompt'un hiç çıkmamasına yol açar. " +
             "KAPALI: pivot + Look Offset kullanılır.")]
    [SerializeField] bool    aimAtBoundsCenter = true;
    [Tooltip("Hedef noktaya eklenen sapma (aimAtBoundsCenter açıkken de uygulanır).")]
    [SerializeField] Vector3 lookOffset = Vector3.zero;
    [SerializeField] float   range      = 2.5f;
    [Tooltip("1 = tam üstüne bakınca, 0 = her açıdan.")]
    [SerializeField] float   lookDot    = 0.45f;

    Camera     cam;
    bool       promptShown;
    Renderer[] visuals;
    bool       visualsCached;

    // null/boş dönerse prompt hiç gösterilmez (ör. iş bitmiş bir obje).
    protected abstract string PromptText { get; }
    protected virtual  Sprite PromptIcon => null;
    protected abstract void   OnInteract();

    protected virtual void Update()
    {
        if (cam == null) { cam = Camera.main; if (cam == null) return; }

        string prompt = PromptText;
        if (string.IsNullOrEmpty(prompt)) { ClearPrompt(); return; }

        Vector3 aim  = AimPoint();
        Vector3 to   = aim - cam.transform.position;
        float   dist = to.magnitude;
        float   dot  = dist > 0.001f ? Vector3.Dot(cam.transform.forward, to / dist) : 1f;

        if (dist <= range && dot >= lookDot)
        {
            DialogueUI.ShowPrompt(prompt, this, PromptIcon);
            promptShown = true;

            if (KeyBindings.DownKey(interactKey) && InteractionInput.TryConsume())
            {
                ClearPrompt();
                OnInteract();
            }
        }
        else
        {
            ClearPrompt();
        }
    }

    // Bakis hedefi: varsa objenin gorsel merkezi, yoksa pivot. Renderer listesi bir kez
    // onbellege alinir — her karede GetComponentsInChildren cagirmak pahali olurdu.
    protected Vector3 AimPoint()
    {
        if (!aimAtBoundsCenter) return transform.TransformPoint(lookOffset);

        // Editor'da onbellege ALMA: raf/prop yeniden kurulunca gizmo eski veriyi gostermesin.
        if (!visualsCached || !Application.isPlaying)
        {
            visuals       = GetComponentsInChildren<Renderer>(true);
            visualsCached = true;
        }

        if (visuals == null || visuals.Length == 0)
            return transform.TransformPoint(lookOffset);

        bool   any = false;
        Bounds b   = default;
        foreach (var r in visuals)
        {
            // Gizli parcalar (ör. kutudaki henuz takilmamis sigortalar) sayilmaz: kapali bir
            // renderer'in bounds'u guvenilir degil ve hedefi dunya merkezine kaydirabilir.
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (!any) { b = r.bounds; any = true; }
            else       b.Encapsulate(r.bounds);
        }
        return any ? b.center + lookOffset : transform.TransformPoint(lookOffset);
    }

    protected void ClearPrompt()
    {
        if (!promptShown) return;
        DialogueUI.HidePrompt(this);   // owner: başkasının prompt'unu söndürmeyiz
        promptShown = false;
    }

    protected virtual void OnDisable() => ClearPrompt();

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireSphere(AimPoint(), range);
    }
}
}
