using UnityEngine;

namespace Bloodrush.Flow
{
// Etkileşim tuşu hakemi. Sahnede birden fazla etkileşim (koltuk, kitap, NPC) aynı tuşu
// bağımsızca dinlediği için, TEK bir E basışı aynı frame'de birden fazlasını tetikleyebiliyordu
// (ör. kitabı kapatmak için basılan E, aynı anda koltuğa oturmayı da başlatıyordu).
//
// Kullanım: Input.GetKeyDown(key) && InteractionInput.TryConsume()
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
}
}
