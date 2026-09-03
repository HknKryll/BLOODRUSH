using UnityEngine;
using Bloodrush.UI;

namespace Bloodrush.Flow
{
// Tek başına duran okunabilir kitap/not. HERHANGİ bir objeye eklenebilir (masadaki dosya,
// duvardaki not, raftan ayrı duran bir kitap): yaklaş + bak → "[E] Oku" → panel açılır.
//
// Sayfa çevirme/kapatma/oyuncu kilidi burada DEĞİL — hepsi BookSession'da (koltuktan
// menüyle açılan kitaplarla ortak, tek merkez).
//
// NOT: Koltukta oturunca menüden seçilecek kitaplar için BU component'e gerek YOK —
// onlar doğrudan SeatInteractable'ın "Books" listesindeki BookData asset'leridir.
public class InteractableBook : MonoBehaviour
{
    [Header("İçerik")]
    [Tooltip("Create → Bloodrush → Kitap Verisi ile oluşturduğun asset.")]
    [SerializeField] BookData data;

    [Header("Etkileşim")]
    [Tooltip("E aynı zamanda varsayılan Kanca tuşu (KeyBindings.Grapple) — çakışma " +
             "gözlemlersen F'ye çevir.")]
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [Tooltip("Bu objenin merkezine göre, oyuncunun bakması beklenen nokta.")]
    [SerializeField] Vector3 lookOffset = new Vector3(0f, 0.1f, 0f);
    [SerializeField] float   range      = 2.2f;
    [Tooltip("1 = tam üstüne bakınca, 0 = her açıdan.")]
    [SerializeField] float   lookDot    = 0.5f;
    [SerializeField] string  promptText = "[E] Oku";

    Camera cam;
    bool   promptShown;

    void Update()
    {
        if (cam == null) { cam = Camera.main; if (cam == null) return; }

        // Bir kitap/menü zaten açıkken yakınlık prompt'u gösterme.
        if (BookSession.IsOpen || BookUI.IsMenuOpen) { ClearPrompt(); return; }

        Vector3 aim  = transform.TransformPoint(lookOffset);
        Vector3 to   = aim - cam.transform.position;
        float   dist = to.magnitude;
        float   dot  = dist > 0.001f ? Vector3.Dot(cam.transform.forward, to / dist) : 1f;

        if (dist <= range && dot >= lookDot)
        {
            DialogueUI.ShowPrompt(promptText, this, UIIcons.Book);
            promptShown = true;
            if (Input.GetKeyDown(interactKey) && InteractionInput.TryConsume())
            {
                ClearPrompt();
                BookSession.Open(data, interactKey);
            }
        }
        else
        {
            ClearPrompt();
        }
    }

    void ClearPrompt()
    {
        if (!promptShown) return;
        DialogueUI.HidePrompt(this);   // owner: başkasının prompt'unu söndürmeyiz
        promptShown = false;
    }

    void OnDisable() => ClearPrompt();

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.8f, 0.3f, 0.8f);
        Gizmos.DrawWireSphere(transform.TransformPoint(lookOffset), range);
    }
}
}
