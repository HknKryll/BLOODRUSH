using System.Collections;

namespace Bloodrush.Flow
{
// Asansör yolculuğunun belirli anlarına dışarıdan (ör. ElevatorAccidentSequence) takılmak
// için. Elevator bunu AYNI GameObject'te GetComponent ile arar — hiç Inspector bağlantısı
// gerekmez, dolayısıyla yanlışlıkla başka bir asansöre bağlanma riski yok. Hook yoksa
// (null) asansör eskisi gibi, birebir aynı davranır.
//
// Neden UnityEvent değil: kabin sineması Run() coroutine'ini BEKLETEBİLMELİ. UnityEvent
// senkrondur, coroutine'i duraklatamaz; arayüz metodu IEnumerator döndürebiliyor.
public interface IElevatorSequenceHook
{
    // Kapı kapandı, kararma HENÜZ başlamadı. Döndürülen coroutine bitene kadar asansör bekler.
    IEnumerator OnCabinCinematic(Elevator elevator);

    // Kararma penceresi başlıyor. chaosWindow = ekranın tamamen kararmasına kalan süre;
    // efektler buna göre kendini ölçekler (kaos tam siyahta bitsin diye).
    void OnBlackoutBegin(float chaosWindow);

    // Ekran %100 siyah — ışınlanmadan hemen önce. Görsel "pop" olmadan durum değiştirmenin
    // tek güvenli anı (silahların alınması burada olur).
    void OnFullyBlack();

    // Fade geri açıldı, oyuncu kontrolü geri aldı.
    void OnRideEnd();
}
}
