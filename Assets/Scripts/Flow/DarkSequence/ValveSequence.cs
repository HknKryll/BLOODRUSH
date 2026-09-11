using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Bloodrush.UI;

namespace Bloodrush.Flow
{
// ADIM 7 — İki valf senkronizasyonu.
//
// TASARIM NOTU (spec'teki çelişkinin çözümü): "B açılınca A kapansın" + "ikisi aynı anda
// açık olsun" birebir uygulanırsa bulmaca ÇÖZÜLEMEZ olur — simetrik bir otomatik kapanma
// sonsuz döngü yaratır. İstenen gerilim ("geri dönüp kontrol et") aslında bir ZAMAN
// PENCERESİ: açılan valf `holdSeconds` boyunca açık kalır, oyuncunun bu sürede diğerine
// koşup onu da açması gerekir. İkisi aynı anda açık olduğu an kapı açılır; yetişemezse
// valf kendiliğinden kapanır ve oyuncu geri döner.
public class ValveSequence : MonoBehaviour
{
    [Header("Valfler")]
    [SerializeField] ValveInteractable valveA;
    [SerializeField] ValveInteractable valveB;

    [Header("Zamanlama")]
    [Tooltip("Açılan valfin kendiliğinden kapanmadan önce açık kalma süresi (sn). " +
             "Oyuncunun diğerine koşup yetişmesi gereken pencere — iki valf arasındaki " +
             "gerçek yürüme süresine göre ayarla.")]
    [SerializeField] float holdSeconds = 14f;
    [Tooltip("Kapanmaya bu kadar kala uyarı bildirimi çıkar (sn). 0 = uyarı yok.")]
    [SerializeField] float warnBefore = 4f;

    [Header("İkisi de açılınca")]
    [Tooltip("Final kapıyı buraya bağla: ElevatorDoor.Open")]
    public UnityEvent onBothOpen;

    bool      solved;
    Coroutine timerA, timerB;

    // ValveInteractable çağırır.
    public void OnValveOpened(ValveInteractable v)
    {
        if (solved) return;

        if (valveA != null && valveB != null && valveA.IsOpen && valveB.IsOpen)
        {
            Solve();
            return;
        }

        if      (v == valveA) Restart(ref timerA, valveA);
        else if (v == valveB) Restart(ref timerB, valveB);
    }

    void Restart(ref Coroutine slot, ValveInteractable valve)
    {
        if (slot != null) StopCoroutine(slot);
        slot = StartCoroutine(HoldThenClose(valve));
    }

    IEnumerator HoldThenClose(ValveInteractable valve)
    {
        float wait = Mathf.Max(0f, holdSeconds - warnBefore);
        if (wait > 0f) yield return new WaitForSeconds(wait);
        if (solved) yield break;

        if (warnBefore > 0f)
        {
            Notification.Show($"{valve.ValveName} BASINÇ DÜŞÜYOR");
            yield return new WaitForSeconds(warnBefore);
        }
        if (solved) yield break;

        valve.ForceClose();
        Notification.Show($"{valve.ValveName} KAPANDI");
    }

    void Solve()
    {
        solved = true;
        if (timerA != null) StopCoroutine(timerA);
        if (timerB != null) StopCoroutine(timerB);
        timerA = timerB = null;

        Notification.Show("HAT BASINÇLANDI");
        Debug.Log("[ValveSequence] İki valf de açık — onBothOpen tetikleniyor.", this);
        onBothOpen?.Invoke();
    }
}
}
