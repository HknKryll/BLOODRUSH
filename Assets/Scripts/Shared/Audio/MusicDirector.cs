using System.Collections;
using UnityEngine;
using Bloodrush.Shared;

namespace Bloodrush.Shared.Audio
{
// Bir BOLUMUN muzik akisinin tek merci. Sahnede tek tane bulunur, tetikler ona "su
// parcaya gec" der. Sonraki bolumlerde aynen kopyalanir — icinde CH2'ye ozel hicbir sey
// yok, her sey Inspector'dan gelir.
//
// KENDI AudioSource'unu KURMAZ. Her parca bir RoomMusic child'idir; loop/fade/pause
// mantigi zaten orada calisiyor ve CH1'de kanitlandi. Boylece klip, ses seviyesi ve
// (ileride) mixer grubu parca basina Inspector'dan ayarlanir — boss muzigini 2-3 dB
// kisma istegi bununla karsilanir, kod degistirmeye gerek kalmaz.
//
// SAHNELER ARASI YASAMAZ (DontDestroyOnLoad yok): LevelExit sonraki sahneyi yukleyince
// kaynaklar sahneyle birlikte yok olur, yani "sonraki bolume gecerken muzik calmasin"
// kendiliginden saglanir.
[DisallowMultipleComponent]
public class MusicDirector : MonoBehaviour
{
    [System.Serializable]
    public class Track
    {
        [Tooltip("Tetiklerin kullandigi kisa ad, orn. 'tutorial' / 'boss' / 'ambiyans'.")]
        public string    id;
        public RoomMusic music;
    }

    [Header("Parcalar")]
    [SerializeField] Track[] tracks;

    [Header("Tempo — olcuye hizalama")]
    [Tooltip("Parcalarin BPM'i. Iki loop da ayni tempoda olmali.")]
    [SerializeField] float bpm = 160f;
    [SerializeField] int   beatsPerBar = 4;
    [Tooltip("Acikken yeni parca, calan parcanin bir sonraki OLCU sinirinda baslar. " +
             "Ayni tempoda olmak tek basina yetmez — fazlari tutmazsa crossfade sirasinda " +
             "ritim cift duyulur. Kapatirsan gecis aninda baslar (faz kaymasi duyulabilir).")]
    [SerializeField] bool  alignToBar = true;

    [Header("Boss")]
    [Tooltip("Boss objesindeki Health. Oldugunde muzik soner.")]
    [SerializeField] Health bossHealth;
    [SerializeField] float  bossDeathFade = 3f;
    [Tooltip("Boss oldukten sonra tetikler muzigi tekrar baslatamasin — bolum sonu sessiz kalir.")]
    [SerializeField] bool   lockAfterBossDeath = true;

    [Header("Oyuncu olumu")]
    [Tooltip("Checkpoint respawn sahneyi YENIDEN YUKLEMIYOR (GameFlow oyuncuyu isinliyor), " +
             "yani boss muzigi oyuncu platform bolumune dondugu halde calmaya devam ederdi. " +
             "Acikken olunce asagidaki parcaya geri donulur.")]
    [SerializeField] bool   revertOnPlayerDeath = true;
    [SerializeField] string revertTrackId   = "tutorial";
    [SerializeField] float  revertCrossfade = 1f;

    public static MusicDirector Instance { get; private set; }

    Track     active;
    Track     previous;   // aktiften once calan parca — revert id'si tutmazsa buna donulur
    Coroutine pendingSwitch;
    bool      locked;

    double BarLength => (60.0 / Mathf.Max(1f, bpm)) * Mathf.Max(1, beatsPerBar);

    void Awake()
    {
        Instance = this;

        // Start'ta StopNow yetmiyordu: RoomMusic'lerin kendi Start'i bundan SONRA kosarsa
        // Play On Start yuzunden muzik sahne acilisinda basliyordu (Start sirasi belirsiz).
        // Awake her Start'tan once kosar — parcalari burada kesin olarak sustur.
        foreach (var t in tracks)
            if (t?.music != null) t.music.SuppressPlayOnStart();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // Hepsi sessiz baslar; ilk sesi tetikler verir.
        foreach (var t in tracks)
        {
            if (t?.music == null) continue;
            t.music.StopNow();
        }

        if (bossHealth != null)
            bossHealth.onDeath.AddListener(OnBossDied);
        else
            Debug.LogWarning("[MusicDirector] Boss Health atanmamis — boss olunce muzik sonmez.", this);

        if (revertOnPlayerDeath)
        {
            var pgo = GameObject.FindGameObjectWithTag("Player");
            if (pgo != null && pgo.TryGetComponent(out Health ph))
                ph.onDeath.AddListener(OnPlayerDied);
        }
    }

    // ───────────────── Public API (tetikler ve UnityEvent'ler cagirir) ─────────────────

    public void PlayInstant(string id) => Play(id, 0f);

    public void Play(string id, float crossfade)
    {
        if (locked) return;

        var next = Find(id);
        if (next == null)
        {
            Debug.LogWarning($"[MusicDirector] '{id}' adinda bir parca yok.", this);
            return;
        }
        if (next.music == null || !next.music.HasClip)
        {
            // Ambiyans gibi klibi henuz atanmamis parcalar icin sessiz gecis: aktif olani
            // yine de sustur ki yanlis muzik calmaya devam etmesin.
            if (active != null) active.music.FadeOut(crossfade);
            active = null;
            return;
        }

        // Zaten bu parca caliyorsa DOKUNMA. Tetige tekrar girmek muzigi bastan
        // baslatmasin, seviyesini dusurmesin — "tutorial boyunca kesintisiz" sarti bu.
        if (next == active && next.music.IsPlaying) return;

        if (pendingSwitch != null) { StopCoroutine(pendingSwitch); pendingSwitch = null; }

        var prev = active;
        if (prev != null) previous = prev;
        active = next;

        bool canAlign = alignToBar && crossfade > 0f &&
                        prev != null && prev.music != null && prev.music.IsPlaying &&
                        prev.music.Source != null && prev.music.Source.clip != null;

        if (!canAlign)
        {
            // Hicbir sey calmiyor ya da hizalama kapali → dogrudan basla.
            next.music.FadeIn(crossfade);
            if (prev != null && prev.music != null) prev.music.FadeOut(crossfade);
            return;
        }

        pendingSwitch = StartCoroutine(SwitchOnBar(prev, next, crossfade));
    }

    public void StopAll(float fade)
    {
        if (pendingSwitch != null) { StopCoroutine(pendingSwitch); pendingSwitch = null; }
        foreach (var t in tracks)
            if (t?.music != null) t.music.FadeOut(fade);
        active = null;
    }

    // ───────────────── Olcuye hizali gecis ─────────────────

    IEnumerator SwitchOnBar(Track prev, Track next, float crossfade)
    {
        var src = prev.music.Source;

        // Calan parcanin loop icindeki konumu → bir sonraki olcu sinirina kalan sure.
        double pos     = src.timeSamples / (double)src.clip.frequency;
        double bar     = BarLength;
        double intoBar = pos % bar;
        double wait    = bar - intoBar;

        // Sinir cok yakinsa (planlama icin zaman kalmadiysa) bir sonrakini hedefle.
        if (wait < 0.05) wait += bar;

        double startAt = AudioSettings.dspTime + wait;

        // Yeni parcayi tam o ana kur — ornek hassasiyetinde, sessiz baslar.
        next.music.PlayScheduledAt(startAt, 0f);

        while (AudioSettings.dspTime < startAt) yield return null;

        // Iki fade de ayni anda, faz kilitli olarak baslar.
        next.music.FadeIn(crossfade);
        prev.music.FadeOut(crossfade);

        pendingSwitch = null;
    }

    // ───────────────── Olaylar ─────────────────

    void OnBossDied()
    {
        Debug.Log($"[MusicDirector] Boss oldu — muzik {bossDeathFade:0.0} sn'de soneceek.", this);
        StopAll(bossDeathFade);
        if (lockAfterBossDeath) locked = true;
    }

    void OnPlayerDied()
    {
        // Boss oldukten sonra ya da hicbir sey calmiyorken geri donecek bir sey yok.
        if (locked || active == null) return;

        // Revert id'si parca listesinde yoksa (CH2'de "tutorial" yazili, parca "TuturoialDovus")
        // sessizce hic donulmuyordu — bir onceki parcaya don.
        var target = Find(revertTrackId);
        if (target == null && previous != null)
        {
            if (!warnedRevert)
            {
                Debug.LogWarning($"[MusicDirector] Revert parcasi '{revertTrackId}' yok — bir onceki parcaya " +
                                 $"('{previous.id}') donuluyor. Inspector'da Revert Track Id'yi duzelt.", this);
                warnedRevert = true;
            }
            target = previous;
        }
        if (target != null && target != active) Play(target.id, revertCrossfade);
    }

    bool warnedRevert;

    Track Find(string id)
    {
        if (tracks == null || string.IsNullOrWhiteSpace(id)) return null;
        foreach (var t in tracks)
            if (t != null && !string.IsNullOrEmpty(t.id) &&
                string.Equals(t.id.Trim(), id.Trim(), System.StringComparison.OrdinalIgnoreCase))
                return t;
        return null;
    }
}
}
