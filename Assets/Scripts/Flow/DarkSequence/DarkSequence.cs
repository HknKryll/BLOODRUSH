using System;
using UnityEngine;

namespace Bloodrush.Flow
{
// "Karanlık Sekans"ın tek durum sahibi.
//
// Bu bölüm KAPALI bir bütündür: kendi içinde başlar, kendi içinde biter. Sonrasında gelecek
// hiçbir bölüme (gözlem odası, kasaba) referansı YOKTUR — sadece "bittim" diye haber verir.
// Bir sonraki bölümü kuran kişi OnComplete'e abone olur ya da IsComplete'e bakar; buraya
// dokunmasına gerek kalmaz.
public static class DarkSequence
{
    public static bool IsComplete { get; private set; }

    // Sekans bitince bir kez tetiklenir.
    public static event Action OnComplete;

    // DarkSequenceExit çağırır (oyuncu son kapıdan geçince).
    public static void Complete()
    {
        if (IsComplete) return;
        IsComplete = true;
        Debug.Log("[DarkSequence] Karanlık sekans TAMAMLANDI.");
        OnComplete?.Invoke();
    }

    // Domain reload kapalıyken static'ler oturumlar arası taşınmasın (PlayerLoadout ile aynı dert).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Boot()
    {
        IsComplete = false;
        OnComplete = null;
    }
}
}
