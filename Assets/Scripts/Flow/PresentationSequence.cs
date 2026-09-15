using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Video;
using Bloodrush.FX;
using Bloodrush.Player;
using Bloodrush.Shared.Audio;
using Bloodrush.UI;

namespace Bloodrush.Flow
{
// Sunum odasi sekansi: sandalyeye otur -> ekran kararir -> video tam ekran oynar
// (ayri calan muzikle senkron) -> video bitince siyah ekran uzerinden tutorial sahnesine gecilir.
//
// Oyuncu sandalyeden KALKMAZ: sekans bitiminde zaten baska sahneye geciliyor.
//
// RENDER YONTEMI — RenderTexture + RawImage, Camera Near Plane DEGIL:
// VideoRenderMode.CameraNearPlane built-in pipeline'in kamera callback'lerine dayaniyor ve
// HDRP'de guvenilir calismiyor (cogu zaman hic render etmiyor). RenderTexture yolu HDRP'de
// sorunsuz ve siralama kontrolu veriyor. RenderTexture KOD ICINDE uretiliyor — asset dosyasi
// yok, projeye ek diff yok.
//
// MUZIK — RoomMusic KULLANILMIYOR, cunku o loop'lu ortam muzigi icin (loop sabit acik, fade'li).
// Buradaki muzik tek seferlik ve videoyla AYNI KAREDE baslamak zorunda. Ikinci bir muzik sistemi
// degil: RoomMusic ile cakismadan calisiyor, sunum baslayinca onu susturuyor.
//
// OTURMA — SeatInteractable'daki kanitlanmis desen kopyalandi (o dosya BookData/BookUI/
// BookSession'a kilitli oldugu icin dogrudan kullanilamiyor, ve ona dokunmamamiz istendi).
public class PresentationSequence : MonoBehaviour
{
    [Header("Icerik")]
    [SerializeField] VideoClip videoClip;
    [Tooltip("Videoyla senkron calacak muzik. Video sessiz, ses buradan geliyor.")]
    [SerializeField] AudioClip musicClip;
    [Tooltip("Projede AudioMixer yok; ileride kurarsan Music grubunu buraya baglarsin.")]
    [SerializeField] AudioMixerGroup musicMixerGroup;
    [Range(0f, 1f)]
    [SerializeField] float musicVolume = 0.85f;

    [Header("Oturma")]
    [Tooltip("Kameranin oturacagi konum/yon. Mavi ok ekrana baksin.")]
    [SerializeField] Transform seatPoint;
    [SerializeField] float transitionTime = 0.6f;

    [Header("Etkilesim")]
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [SerializeField] float   range       = 2.5f;
    [Tooltip("1 = tam ustune bakinca, 0 = her acidan.")]
    [SerializeField] float   lookDot     = 0.35f;
    [SerializeField] string  sitPrompt   = "[E] Otur";

    [Header("Karartma")]
    [SerializeField] float fadeToBlack = 0.8f;
    [Tooltip("Tam siyahta, video baslamadan onceki bekleme.")]
    [SerializeField] float blackHold   = 0.6f;

    [Header("Oda muzigi (opsiyonel)")]
    [Tooltip("Sunum baslayinca susturulacak ortam muzigi. Bos birakilabilir.")]
    [SerializeField] RoomMusic roomMusicToSilence;

    [Header("Atlama")]
    [Tooltip("Varsayilan KAPALI. Acarsan sunum sirasinda asagidaki tusla atlanabilir.")]
    [SerializeField] bool    allowSkip = false;
    [SerializeField] KeyCode skipKey   = KeyCode.Space;
    [Tooltip("Atlarken video ve muzigin birlikte sonme suresi.")]
    [SerializeField] float   skipFade  = 0.6f;

    [Header("Sonraki sahne")]
    [Tooltip("Bos birakilirsa Build Settings'teki SIRADAKI sahne yuklenir (CH1 -> CH2).")]
    [SerializeField] string nextScene = "";
    [Tooltip("Yeni sahnede ekranin siyah kalacagi sure.")]
    [SerializeField] float  sceneHoldBlack = 0.8f;
    [Tooltip("Yeni sahnede siyahtan acilma suresi.")]
    [SerializeField] float  sceneFadeIn = 1.2f;

    [Header("Bitince")]
    [Tooltip("Gecis aninda tetiklenir — ses/efekt baglamak icin.")]
    public UnityEvent onPresentationFinished;

    // ── Calisma zamani ─────────────────────────────────────────────────

    Camera         cam;
    Transform      camTr;
    PlayerMovement player;
    PlayerShoot    shoot;
    CameraShake    shake;

    VideoPlayer   video;
    AudioSource   music;
    RenderTexture rt;
    Canvas        videoCanvas;
    RawImage      videoImage;
    Image         blackOverlay;

    bool promptShown;
    bool started;      // sekans basladi (bir daha tetiklenmesin)
    bool finishing;    // bitis/atlama bir kez calissin
    bool playing;      // video oynuyor (atlama tusu bu sirada dinlenir)

    void Update()
    {
        if (playing && allowSkip && KeyBindings.DownKey(skipKey))
        {
            StartCoroutine(SkipOut());
            return;
        }

        if (started) { ClearPrompt(); return; }

        if (cam == null) { cam = Camera.main; if (cam == null) return; }

        Vector3 to   = transform.position - cam.transform.position;
        float   dist = to.magnitude;
        float   dot  = dist > 0.001f ? Vector3.Dot(cam.transform.forward, to / dist) : 1f;

        if (dist <= range && dot >= lookDot)
        {
            DialogueUI.ShowPrompt(sitPrompt, this);
            promptShown = true;

            if (KeyBindings.DownKey(interactKey) && InteractionInput.TryConsume())
            {
                ClearPrompt();
                StartCoroutine(Run());
            }
        }
        else
        {
            ClearPrompt();
        }
    }

    void ClearPrompt()
    {
        if (!promptShown) return;
        DialogueUI.HidePrompt(this);
        promptShown = false;
    }

    void OnDisable() => ClearPrompt();

    // ── Sekans ─────────────────────────────────────────────────────────

    IEnumerator Run()
    {
        if (started) yield break;
        started = true;

        if (videoClip == null)
            Debug.LogWarning("[PresentationSequence] Video klibi atanmamis.", this);

        // 1) Kontrolu al + kamerayi sandalyeye tasi
        yield return SitDown();

        // 2) Ekrani karart
        blackOverlay = GameFlow.CreateOverlay(Color.black);
        yield return FadeOverlay(0f, 1f, fadeToBlack);
        if (blackHold > 0f) yield return new WaitForSeconds(blackHold);

        // 3) Video + muzigi hazirla, AYNI KAREDE baslat
        yield return PrepareAndPlay();
    }

    IEnumerator FadeOverlay(float from, float to, float seconds)
    {
        if (blackOverlay == null) yield break;

        if (seconds <= 0f)
        {
            blackOverlay.color = new Color(0f, 0f, 0f, to);
            yield break;
        }

        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            blackOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }
        blackOverlay.color = new Color(0f, 0f, 0f, to);
    }

    IEnumerator SitDown()
    {
        if (roomMusicToSilence != null) roomMusicToSilence.FadeOut();

        camTr  = cam.transform;
        player = cam.GetComponentInParent<PlayerMovement>();
        shoot  = player != null ? player.GetComponentInChildren<PlayerShoot>(true) : null;
        shake  = cam.GetComponentInParent<CameraShake>();

        // Look() + Move() dursun: kamerayi biz surecegiz.
        if (player != null) player.enabled = false;

        // CameraShake LateUpdate'te KOSULSUZ localPosition yaziyor (trauma 0 iken bile) —
        // kapatmazsak kamera konumu her frame geri alinir, sandalyeye hic gitmez.
        if (shake != null) shake.enabled = false;

        // Silah kamera child'i: gecis sirasinda kadraja girer.
        if (shoot != null) { shoot.CanShoot = false; shoot.WeaponsHidden = true; }

        if (seatPoint == null)
        {
            Debug.LogWarning("[PresentationSequence] seatPoint atanmamis — kamera tasinmiyor.", this);
            yield break;
        }

        Vector3    fromPos = camTr.position;
        Quaternion fromRot = camTr.rotation;
        float t = 0f;
        while (t < transitionTime)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / transitionTime));
            camTr.SetPositionAndRotation(Vector3.Lerp(fromPos, seatPoint.position, p),
                                         Quaternion.Slerp(fromRot, seatPoint.rotation, p));
            yield return null;
        }
        camTr.SetPositionAndRotation(seatPoint.position, seatPoint.rotation);
    }

    IEnumerator PrepareAndPlay()
    {
        BuildVideoSurface();

        video = gameObject.AddComponent<VideoPlayer>();
        video.clip            = videoClip;
        video.renderMode      = VideoRenderMode.RenderTexture;
        video.targetTexture   = rt;
        video.isLooping       = false;
        video.playOnAwake     = false;
        video.waitForFirstFrame = true;                 // ilk kare hazir olmadan gosterme
        video.audioOutputMode = VideoAudioOutputMode.None;   // video sessiz, ses ayri

        music = gameObject.AddComponent<AudioSource>();
        music.clip                  = musicClip;
        music.loop                  = false;
        music.playOnAwake           = false;
        music.spatialBlend          = 0f;               // 2D
        music.volume                = musicVolume;
        music.outputAudioMixerGroup = musicMixerGroup;

        bool prepared = false;
        video.prepareCompleted += _ => prepared = true;
        video.loopPointReached += _ => { if (!finishing) StartCoroutine(FinishOut()); };
        video.Prepare();

        // Hazir olana kadar bekle (guvenlik tavani: 10 sn)
        float wait = 0f;
        while (!prepared && wait < 10f) { wait += Time.deltaTime; yield return null; }
        if (!prepared) Debug.LogWarning("[PresentationSequence] Video hazirlanamadi, yine de baslatiliyor.", this);

        // AYNI KARE: ikisi de burada basliyor, arada yield YOK.
        video.Play();
        if (musicClip != null) music.Play();

        videoCanvas.enabled = true;
        playing = true;
    }

    void BuildVideoSurface()
    {
        rt = new RenderTexture(1920, 1080, 0) { name = "SunumRT" };
        rt.Create();

        var go = new GameObject("SunumVideoCanvas");
        videoCanvas = go.AddComponent<Canvas>();
        videoCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        videoCanvas.sortingOrder = 210;          // siyah perdenin (200) USTUNDE
        videoCanvas.enabled      = false;        // ilk kare hazir olunca acilir
        go.AddComponent<CanvasScaler>();

        videoImage = new GameObject("Video").AddComponent<RawImage>();
        videoImage.transform.SetParent(go.transform, false);
        videoImage.texture       = rt;
        videoImage.raycastTarget = false;
        var r = videoImage.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    // ── Bitis yollari ──────────────────────────────────────────────────

    // Video dogal olarak bitti: zaten siyaha kararak bitiyor, altta siyah perde hazir.
    IEnumerator FinishOut()
    {
        if (finishing) yield break;
        finishing = true;
        playing   = false;

        if (video != null) video.Stop();
        if (videoCanvas != null) videoCanvas.enabled = false;

        yield return GoToNextScene();
    }

    // Atlandi: video ve muzik BIRLIKTE kisa bir fade ile sonuyor.
    IEnumerator SkipOut()
    {
        if (finishing) yield break;
        finishing = true;
        playing   = false;

        float startVol = music != null ? music.volume : 0f;
        float t = 0f;
        while (t < skipFade)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / skipFade);
            if (videoImage != null) videoImage.color = new Color(1f, 1f, 1f, k);
            if (music      != null) music.volume     = startVol * k;
            yield return null;
        }

        if (video != null) video.Stop();
        if (music != null) music.Stop();
        if (videoCanvas != null) videoCanvas.enabled = false;

        yield return GoToNextScene();
    }

    IEnumerator GoToNextScene()
    {
        if (music != null) music.Stop();

        // Perde tam siyah olsun — video kapaninca arada oyun goruntusu gorunmesin.
        if (blackOverlay == null) blackOverlay = GameFlow.CreateOverlay(Color.black);
        blackOverlay.color = Color.black;
        yield return null;

        onPresentationFinished?.Invoke();

        ReleaseVideo();

        // Perdeyi YENI SAHNEYE TASI: SceneFadeIn onu DontDestroyOnLoad ile goturur, orada
        // sceneHoldBlack kadar tutar, sonra sondurur. Yukleme sirasinda oyun goruntusu gorunmez.
        SceneFadeIn.CarryOverlay(blackOverlay, sceneHoldBlack, sceneFadeIn);

        if (string.IsNullOrEmpty(nextScene))
            GameFlow.LoadNext();
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
    }

    // Kod icinde uretilen RenderTexture otomatik toplanmiyor — elle birak.
    void ReleaseVideo()
    {
        if (video != null) { video.targetTexture = null; }
        if (rt != null)
        {
            rt.Release();
            Destroy(rt);
            rt = null;
        }
        if (videoCanvas != null) Destroy(videoCanvas.gameObject);
    }

    void OnDestroy() => ReleaseVideo();

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, range);
        if (seatPoint != null)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(seatPoint.position, 0.25f);
            Gizmos.DrawRay(seatPoint.position, seatPoint.forward * 1.5f);
        }
    }
}
}
