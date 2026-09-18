using UnityEngine;

namespace Bloodrush.Flow
{
// Etkileşim tuşu hakemi. Sahnede birden fazla etkileşim (koltuk, kitap, NPC) aynı tuşu
// bağımsızca dinlediği için, TEK bir E basışı aynı frame'de birden fazlasını tetikleyebiliyordu
// (ör. kitabı kapatmak için basılan E, aynı anda koltuğa oturmayı da başlatıyordu).
//
// Kullanım: KeyBindings.DownKey(key) && InteractionInput.TryConsume()
// İlk çağıran o frame'in tuşunu alır, diğerleri false görür.
public static class InteractionInput
{
    static int consumedFrame = -1;

    public static bool TryConsume()
    {
        if (consumedFrame == Time.frameCount) return false;
        consumedFrame = Time.frameCount;
        return true;
    }

    // ESC icin ayni hakem. ESC'yi alti ayri yer dinliyor (PauseMenuController,
    // ConfirmDialog, SettingsPanel ve rebind yakalayicisi, CreditsPanel, BookSession) ve
    // iki GameObject'in Update sirasi Unity'de TANIMSIZ. Hakem olmadan tek bir ESC ayni
    // karede birden cok katmani kapatiyordu: onay penceresini kapatan ESC oyunu da devam
    // ettiriyordu, kitabi kapatan ESC pause'u acip imleci kilitli birakabiliyordu.
    //
    // Kullanim: esc && InteractionInput.TryConsumeEscape()
    // Modallar ESC'yi islemeden once cagirir; pause ise bir modal aciksa hic dinlemez.
    static int escapeConsumedFrame = -1;

    public static bool TryConsumeEscape()
    {
        if (escapeConsumedFrame == Time.frameCount) return false;
        escapeConsumedFrame = Time.frameCount;
        return true;
    }
}
}
