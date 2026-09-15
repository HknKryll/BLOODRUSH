using UnityEngine;
using Bloodrush.Player;
using Bloodrush.UI;

namespace Bloodrush.Flow
{
// Açık kitap oturumu — TEK merkez. Sayfa çevirme, kapatma, oyuncu kilidi ve imleç
// yönetimi burada; hem InteractableBook (tek kitap) hem SeatInteractable (oturunca
// menüden seçilen kitap) bunu kullanır. Böylece mantık iki yerde çoğaltılmaz ve
// aynı anda BİRDEN FAZLA kitap açılması imkânsız olur.
//
// BookUI/DialogueUI ile aynı desen: kendini oluşturan singleton, sahneye bir şey koymazsın.
public class BookSession : MonoBehaviour
{
    static BookSession instance;

    BookData          data;
    int               page;
    KeyCode           key;
    PlayerMovement    player;
    bool              disabledPlayer;   // PlayerMovement'ı BİZ mi kapattık
    System.Action     onClosed;

    public static bool IsOpen => instance != null && instance.data != null;

    static BookSession Ensure()
    {
        if (instance == null)
            instance = new GameObject("BookSession").AddComponent<BookSession>();
        return instance;
    }

    // closedCallback: kitap kapanınca çağrılır (koltuk bunu kullanıp menüye geri döner).
    public static void Open(BookData book, KeyCode interactKey, System.Action closedCallback = null)
    {
        if (book == null || book.PageCount == 0)
        {
            Debug.LogWarning("[BookSession] BookData boş ya da sayfası yok.");
            return;
        }

        var i = Ensure();
        i.data     = book;
        i.page     = 0;
        i.key      = interactKey;
        i.onClosed = closedCallback;

        // Panel açıkken hareket VE kamera dönüşü kilitli. Oyuncu OTURUYORSA PlayerMovement
        // zaten kapalıdır — o zaman biz kapatmadığımız için AÇMAYIZ da (yoksa kitabı
        // kapatınca kamera oturma pozundan fırlardı).
        var cam = Camera.main;
        i.player = cam != null ? cam.GetComponentInParent<PlayerMovement>() : null;
        i.disabledPlayer = i.player != null && i.player.enabled;
        if (i.disabledPlayer) i.player.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        i.ShowPage();
    }

    public static void Close()
    {
        if (instance != null) instance.CloseInternal();
    }

    void Update()
    {
        if (data == null) return;

        if (KeyBindings.DownKey(KeyCode.Escape)) { CloseInternal(); return; }

        // TryConsume: kapatan/sayfa çeviren E'nin aynı frame'de koltuğu da tetiklemesini önler.
        if (!KeyBindings.DownKey(key) || !InteractionInput.TryConsume()) return;

        if (page < data.PageCount - 1) { page++; ShowPage(); }
        else                            CloseInternal();
    }

    void ShowPage()
    {
        bool last = page >= data.PageCount - 1;
        BookUI.ShowBook(data.title, data.subtitle, data.Page(page), page, data.PageCount,
                        key.ToString(), last ? "Kapat" : "Sonraki Sayfa",
                        last ? UIIcons.Exit : UIIcons.Next);
    }

    void CloseInternal()
    {
        data = null;
        BookUI.HideBook();

        // SADECE biz kapattıysak geri açarız (oturmadaki kilit koltuğun sorumluluğu).
        if (disabledPlayer && player != null) player.enabled = true;
        disabledPlayer = false;
        player = null;

        if (!KeepCursorFree())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        var cb = onClosed;
        onClosed = null;
        cb?.Invoke();      // koltuk: menüye geri dön
    }

    // Kitap kapandıktan sonra imleç serbest kalmalı mı? (Koltuk menüsü hâlâ açıksa evet.)
    bool KeepCursorFree() => onClosed != null;
}
}
