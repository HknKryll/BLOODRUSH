using UnityEngine;

namespace Bloodrush.Flow
{
// Tek bir okunabilir içerik (kitap, not kağıdı, rapor...). Her içerik için Project'te
// ayrı bir asset oluşturulur: Create → Bloodrush → Kitap Verisi.
//
// Sahnedeki her kitap objesi kendi InteractableBook'unda BU asset'e referans verir —
// aynı UI'yi paylaşırlar, içerik burada durur. İçerik asset'te olduğu için sahne/prefab
// yeniden kurulsa bile metinler KAYBOLMAZ.
[CreateAssetMenu(menuName = "Bloodrush/Kitap Verisi", fileName = "YeniKitap")]
public class BookData : ScriptableObject
{
    [Tooltip("Panelin üstünde görünen başlık.")]
    public string title = "İsimsiz";

    [Tooltip("Okuma ekranında başlığın altında küçük, amber, mono bir alt bilgi (ör. tarih/kod). Boşsa gösterilmez.")]
    public string subtitle;

    [Tooltip("Her eleman = bir sayfa. Oyuncu E ile sayfalar arasında ilerler.")]
    [TextArea(4, 20)]
    public string[] pages;

    public int PageCount => pages != null ? pages.Length : 0;

    public string Page(int index)
    {
        if (pages == null || index < 0 || index >= pages.Length) return "";
        return pages[index];
    }
}
}
