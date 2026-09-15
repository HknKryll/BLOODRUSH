using System.Collections;
using UnityEngine;
using Bloodrush.FX;
using Bloodrush.Player;
using Bloodrush.UI;

namespace Bloodrush.Flow
{
// Oturma: koltuğa yaklaş → "[E] Otur" → SADECE KAMERA yumuşak bir geçişle oturma
// noktasına gider. Tekrar E → kamera geldiği yere döner, kontrol geri gelir.
//
// Oyuncunun GÖVDESİ hiç hareket etmez — bu yüzden CharacterController'a HİÇ dokunulmuyor.
// (Önceki sürüm gövdeyi taşımak için CC'yi kapatıyordu; PlayerMovement.Move() her frame
// koşulsuz cc.Move() çağırdığı için Unity "Move called on inactive controller" hatası
// basıyordu. Kamera-tabanlı modelde bu sorun yapısal olarak yok.)
//
// Geçiş sırasında ve otururken PlayerMovement KAPATILIR: Look() ve Move() aynı Update'te
// olduğu için, açık kalsaydı her frame cameraHolder'ın pozisyon/rotasyonunu geri ezip
// kamerayı koltuktan koparırdı. Bu yüzden otururken fareyle bakış da durur (kameranın
// sabit bir "oturma manzarası" olması isteniyor).
public class SeatInteractable : MonoBehaviour
{
    [Header("Oturma Noktası")]
    [Tooltip("Kameranın gideceği pozisyon+bakış yönü. Koltuğun üstüne boş bir child obje " +
             "koy, GÖZ hizasına getir (~1.1 m), +Z'si oyuncunun bakacağı yön olsun.")]
    [SerializeField] Transform seatPoint;
    [Tooltip("Kameranın oturma noktasına gidiş/dönüş süresi (saniye).")]
    [SerializeField] float transitionTime = 0.45f;

    [Header("Oturunca Açılacak Kitaplar (en fazla 9)")]
    [Tooltip("Oturunca bu listeden bir seçim menüsü açılır (sayı tuşuyla seçilir). " +
             "Boş bırakılırsa oturmak sadece kamerayı taşır, menü açılmaz.")]
    [SerializeField] BookData[] books;
    [Tooltip("Seçim menüsünün üstünde yazan başlık.")]
    [SerializeField] string menuHeader = "OKUMA KÖŞESİ";

    [Header("Etkileşim")]
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [SerializeField] float   range       = 2.5f;
    [Tooltip("1 = tam koltuğa bakınca, 0 = her açıdan.")]
    [SerializeField] float   lookDot     = 0.35f;
    [SerializeField] string  sitPrompt   = "[E] Otur";
    [SerializeField] string  standPrompt = "[E] Kalk";

    Camera         cam;
    PlayerMovement player;
    CameraShake    shake;            // LateUpdate'te kamera konumunu geri yazıyor — geçişte kapatılır
    Transform      camTr;
    Vector3        camLocalPos;      // oturmadan önceki local konum (aynen geri konur)
    Quaternion     camLocalRot;
    bool           seated;
    bool           transitioning;
    bool           promptShown;
    int            selectedIndex;    // menüde W/S ile gezinilen satır

    public bool IsSeated => seated;

    void Update()
    {
        if (cam == null) { cam = Camera.main; if (cam == null) return; }
        if (transitioning) return;

        // Bir kitap AÇIKKEN hiçbir şeye karışma — sayfa çevirme/kapatma BookSession'ın.
        if (BookSession.IsOpen) { ClearPrompt(); return; }

        if (seated)
        {
            HandleSeatedInput();
            return;
        }

        Vector3 aim  = seatPoint != null ? seatPoint.position : transform.position;
        Vector3 to   = aim - cam.transform.position;
        float   dist = to.magnitude;
        float   dot  = dist > 0.001f ? Vector3.Dot(cam.transform.forward, to / dist) : 1f;

        if (dist <= range && dot >= lookDot)
        {
            DialogueUI.ShowPrompt(sitPrompt, this, UIIcons.Chair);
            promptShown = true;
            if (KeyBindings.DownKey(interactKey) && InteractionInput.TryConsume()) StartCoroutine(Sit());
        }
        else
        {
            ClearPrompt();
        }
    }

    // Otururken: W/S (ya da ok tuşları) ile gez, Enter ile aç, sayı tuşu doğrudan
    // seçip açar (kısayol), E ile kalk.
    void HandleSeatedInput()
    {
        int count = books != null ? Mathf.Min(books.Length, 9) : 0;

        if (count > 0)
        {
            if (KeyBindings.DownKey(KeyCode.S) || KeyBindings.DownKey(KeyCode.DownArrow))
            {
                selectedIndex = (selectedIndex + 1) % count;
                ShowMenu();
            }
            else if (KeyBindings.DownKey(KeyCode.W) || KeyBindings.DownKey(KeyCode.UpArrow))
            {
                selectedIndex = (selectedIndex - 1 + count) % count;
                ShowMenu();
            }
            else if (KeyBindings.DownKey(KeyCode.Return) || KeyBindings.DownKey(KeyCode.KeypadEnter))
            {
                OpenBook(selectedIndex);
                return;
            }
            else
            {
                for (int n = 0; n < count; n++)
                {
                    if (!KeyBindings.DownKey((KeyCode)((int)KeyCode.Alpha1 + n))) continue;
                    selectedIndex = n;
                    OpenBook(n);
                    return;
                }
            }
        }

        DialogueUI.ShowPrompt(standPrompt, this, UIIcons.Exit);
        promptShown = true;
        if (KeyBindings.DownKey(interactKey) && InteractionInput.TryConsume()) StartCoroutine(Stand());
    }

    void OpenBook(int index)
    {
        if (books == null || index < 0 || index >= books.Length || books[index] == null) return;
        BookUI.HideMenu();
        // Kitap kapanınca menüye geri dön (hâlâ oturuyoruz).
        BookSession.Open(books[index], interactKey, ShowMenu);
    }

    void ShowMenu()
    {
        if (books == null || books.Length == 0) return;
        int count = Mathf.Min(books.Length, 9);
        selectedIndex = Mathf.Clamp(selectedIndex, 0, count - 1);

        var titles = new string[count];
        for (int n = 0; n < count; n++)
            titles[n] = books[n] != null && !string.IsNullOrEmpty(books[n].title)
                      ? books[n].title : $"(boş {n + 1})";

        var hints = new (string, string, Sprite)[] {
            ("W/S", "Gez", UIIcons.Navigate),
            ("Enter", "Aç", UIIcons.Book),
            (interactKey.ToString(), "Kalk", UIIcons.Exit),
        };
        BookUI.ShowMenu(titles, selectedIndex, menuHeader, hints);
    }

    IEnumerator Sit()
    {
        if (seatPoint == null)
        {
            Debug.LogWarning("[SeatInteractable] seatPoint atanmamış — oturulamıyor.", this);
            yield break;
        }
        player = cam.GetComponentInParent<PlayerMovement>();
        if (player == null)
        {
            Debug.LogWarning("[SeatInteractable] PlayerMovement bulunamadı.", this);
            yield break;
        }

        transitioning = true;
        ClearPrompt();

        camTr = cam.transform;
        camLocalPos = camTr.localPosition;     // dönüşte birebir geri konacak
        camLocalRot = camTr.localRotation;

        // Look() + Move() dursun: kamerayı biz süreceğiz, PlayerMovement her frame geri ezmesin.
        player.enabled = false;

        // CameraShake, LateUpdate'te KOŞULSUZ "localPosition = basePos" yazıyor (trauma 0 iken
        // bile). Kapatmazsak kameranın konumunu her frame geri alır — rotasyon değişir ama
        // konum hiç kıpırdamaz. Geçiş+oturma boyunca kapalı, kalkışta geri açılır.
        shake = cam.GetComponentInParent<CameraShake>();
        if (shake != null) shake.enabled = false;

        yield return MoveCamera(camTr.position, camTr.rotation, seatPoint.position, seatPoint.rotation);

        seated = true;
        transitioning = false;

        // Otururken imleç serbest (menüden seçim yapılacak).
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        selectedIndex = 0;
        ShowMenu();
    }

    IEnumerator Stand()
    {
        transitioning = true;
        ClearPrompt();
        BookUI.HideAll();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        // Oturmadan önceki local poz/rot'un ŞU ANKİ dünya karşılığı (gövde hiç kıpırdamadı,
        // ama yine de parent'tan hesaplıyoruz — güvenli).
        Transform parent = camTr.parent;
        Vector3    backPos = parent != null ? parent.TransformPoint(camLocalPos) : camLocalPos;
        Quaternion backRot = parent != null ? parent.rotation * camLocalRot      : camLocalRot;

        yield return MoveCamera(camTr.position, camTr.rotation, backPos, backRot);

        // Local değerleri birebir geri koy — PlayerMovement açılınca kaldığı yerden sürsün.
        camTr.localPosition = camLocalPos;
        camTr.localRotation = camLocalRot;

        if (shake != null)  shake.enabled  = true;
        if (player != null) player.enabled = true;

        seated = false;
        transitioning = false;
    }

    IEnumerator MoveCamera(Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot)
    {
        float t = 0f;
        while (t < transitionTime)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / transitionTime));
            camTr.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, p),
                                         Quaternion.Slerp(fromRot, toRot, p));
            yield return null;
        }
        camTr.SetPositionAndRotation(toPos, toRot);
    }

    void ClearPrompt()
    {
        if (!promptShown) return;
        DialogueUI.HidePrompt(this);
        promptShown = false;
    }

    void OnDisable()
    {
        // Otururken devre dışı kalırsak oyuncuyu kilitli/kamerayı koltukta bırakma.
        if (seated && camTr != null)
        {
            camTr.localPosition = camLocalPos;
            camTr.localRotation = camLocalRot;
            if (shake != null)  shake.enabled  = true;
            if (player != null) player.enabled = true;
            BookUI.HideAll();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }
        seated = false;
        transitioning = false;
        ClearPrompt();
    }

    void OnDrawGizmosSelected()
    {
        if (seatPoint == null) return;
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(seatPoint.position, 0.18f);
        Gizmos.DrawLine(seatPoint.position, seatPoint.position + seatPoint.forward * 1.2f);
    }
}
}
