using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodrush.Flow
{
// ADIM 7 — İki valf senkronizasyonu + mini oyunun aracısı.
//
// TASARIM NOTU (spec'teki çelişkinin çözümü): "B açılınca A kapansın" + "ikisi aynı anda
// açık olsun" birebir uygulanırsa bulmaca ÇÖZÜLEMEZ olur — simetrik bir otomatik kapanma
// sonsuz döngü yaratır. İstenen gerilim ("geri dönüp kontrol et") aslında bir ZAMAN
// PENCERESİ: açılan valf `windowSeconds` boyunca açık kalır, oyuncunun bu sürede diğerine
// koşup onu da açması gerekir. İkisi aynı anda açık olduğu an kapı açılır; yetişemezse
// valf kendiliğinden kapanır ve oyuncu geri döner.
//
// Valf ARTIK doğrudan açılmıyor: ValveInteractable RequestTurn der, burada ibre yakalama mini
// oyunu (ValveMiniGame) başlar. Diğer valfin penceresi işliyorsa zor kademe seçilir ve mini
// oyun süreye dahildir. Gösterge (ValveGaugeUI) ve basınç sesleri (PressureAmbience) bu
// objeye çalışma zamanında eklenir — sahneye bir şey koymak gerekmez.
public class ValveSequence : MonoBehaviour
{
    [Header("Valfler")]
    [SerializeField] ValveInteractable valveA;
    [SerializeField] ValveInteractable valveB;

    [Header("Ayarlar")]
    [Tooltip("Boşsa Resources/DarkSequence/ValvePressureConfig kullanılır. Süre, zorluk, sıklık ve " +
             "sesler oradan ayarlanır.")]
    [SerializeField] ValvePressureConfig config;

    [Header("İkisi de açılınca")]
    [Tooltip("Final kapıyı buraya bağla: ElevatorDoor.Open")]
    public UnityEvent onBothOpen;

    // Loş durum satırı (ValveGaugeUI dinler).
    public event Action<string>            Status;
    // Süresi dolup kendiliğinden kapanan valf (PressureAmbience dinler).
    public event Action<ValveInteractable> ValveClosed;

    public bool IsSolved => solved;

    // Açık valf(ler)in penceresinden kalan en küçük oran (0..1). Pencere işlemiyorsa -1.
    public float RemainingFraction
    {
        get
        {
            float r = -1f;
            float window = Mathf.Max(0.01f, Config.windowSeconds);
            if (timerA != null) r = Mathf.Clamp01(1f - (Time.time - openedAtA) / window);
            if (timerB != null)
            {
                float rb = Mathf.Clamp01(1f - (Time.time - openedAtB) / window);
                r = r < 0f ? rb : Mathf.Min(r, rb);
            }
            return r;
        }
    }

    bool      solved;
    Coroutine timerA, timerB;
    float     openedAtA, openedAtB;

    ValveMiniGame    miniGame;
    PressureAmbience ambience;

    ValvePressureConfig Config => config != null ? config : (config = ValvePressureConfig.Load());

    void Awake()
    {
        miniGame = GetComponent<ValveMiniGame>();
        if (miniGame == null) miniGame = gameObject.AddComponent<ValveMiniGame>();

        var gauge = GetComponent<ValveGaugeUI>();
        if (gauge == null) gauge = gameObject.AddComponent<ValveGaugeUI>();
        gauge.Bind(miniGame, this);

        ambience = GetComponent<PressureAmbience>();
        if (ambience == null) ambience = gameObject.AddComponent<PressureAmbience>();
        ambience.Bind(miniGame, this, Config);

        miniGame.Ended += OnMiniGameEnded;
    }

    void Start()
    {
        // Sahnede ikisinin de adı "VALF" — bildirimler ve göstergede hangisi olduğu belli olsun.
        if (valveA != null) valveA.DisplayName = "A VALFİ";
        if (valveB != null) valveB.DisplayName = "B VALFİ";
    }

    void OnDestroy()
    {
        if (miniGame != null) miniGame.Ended -= OnMiniGameEnded;
    }

    public bool IsTurning(ValveInteractable v) => miniGame != null && miniGame.IsActive && miniGame.Valve == v;
    public bool AnyTurning => miniGame != null && miniGame.IsActive;

    // ValveInteractable çağırır: E'ye basıldı, mini oyunu başlat.
    public void RequestTurn(ValveInteractable v, KeyCode interactKey)
    {
        if (solved || v == null || v.IsOpen || miniGame.IsActive) return;

        ValveInteractable other = v == valveA ? valveB : valveA;
        int tier = other != null && other.IsOpen ? 1 : 0;
        miniGame.Begin(v, Config, tier, interactKey);
    }

    void OnMiniGameEnded(ValveInteractable v, bool opened)
    {
        if (!opened || solved) return;
        v.SetOpen(true);
        OnValveOpened(v);
    }

    void OnValveOpened(ValveInteractable v)
    {
        if (valveA != null && valveB != null && valveA.IsOpen && valveB.IsOpen)
        {
            Solve();
            return;
        }

        if (v == valveA)      { openedAtA = Time.time; Restart(ref timerA, valveA); }
        else if (v == valveB) { openedAtB = Time.time; Restart(ref timerB, valveB); }
        Status?.Invoke($"{v.DisplayName} AÇIK — DİĞERİNE YETİŞ");
    }

    void Restart(ref Coroutine slot, ValveInteractable valve)
    {
        if (slot != null) StopCoroutine(slot);
        slot = StartCoroutine(HoldThenClose(valve));
    }

    IEnumerator HoldThenClose(ValveInteractable valve)
    {
        yield return new WaitForSeconds(Config.windowSeconds);
        if (solved) yield break;

        if (valve == valveA) timerA = null;
        else                 timerB = null;

        valve.ForceClose();
        ValveClosed?.Invoke(valve);
        Status?.Invoke($"{valve.DisplayName} KAPANDI");
    }

    void Solve()
    {
        solved = true;
        if (timerA != null) StopCoroutine(timerA);
        if (timerB != null) StopCoroutine(timerB);
        timerA = timerB = null;

        ambience.End();
        Status?.Invoke("HAT BASINÇLANDI");
        Debug.Log("[ValveSequence] İki valf de açık — onBothOpen tetikleniyor.", this);
        onBothOpen?.Invoke();
    }
}
}
