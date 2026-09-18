using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;
using Bloodrush.FX;
using Bloodrush.Player;
using Bloodrush.UI;
using Bloodrush.Shared.Audio;

namespace Bloodrush.Flow
{
// CH2 acilisi: oyuncu koridora bir yerden ATILMIS gibi. Siyah ekranda dusme ruzgari, pat diye
// carpma + inleme; perde acilinca kamera yerde yan yatik ve bulanik, iki goz kirpma, sendeleyerek
// ayaga kalkis, silah cekme, kontrol oyuncuya gecer. HUD bu sure boyunca gizli.
//
// Sahneye bos bir objeye ekle, baska bir sey gerekmez (sesler bilesen eklenince otomatik dolar).
// Her sahne yuklemesinde oynar; checkpoint dirilisi sahneyi yeniden yuklemedigi icin orada oynamaz.
//
// CH1'den gelen tasinan siyah perde (SceneFadeIn, sort 200) bunun ALTINDA kalir; SceneFadeIn
// perdeyi acinca PlayerMovement'i geri acar — kilit bu yuzden her karede yeniden uygulanir.
public class FallIntroSequence : MonoBehaviour
{
    const int OverlaySortingOrder = 32000;   // her sahne/menu canvas'inin ustunde

    [Header("Sesler")]
    public AudioClip fallWindClip;
    [SerializeField] [Range(0f, 1f)] float fallWindVolume = 0.8f;
    public AudioClip impactClip;
    [SerializeField] [Range(0f, 1f)] float impactVolume = 1f;
    public AudioClip gruntClip;
    [Tooltip("Carpma sesini bastirmasin: dusuk tut.")]
    [SerializeField] [Range(0f, 1f)] float gruntVolume = 0.5f;
    [Tooltip("Carpmadan kac sn sonra duyulsun (0.25 = darbenin tepesi gectikten sonra).")]
    [SerializeField] float gruntDelay = 0.25f;

    [Header("Zamanlama (sn)")]
    [SerializeField] float startDelay       = 0.2f;
    [Tooltip("Siyah ekranda ruzgarin suresi (dusus).")]
    [SerializeField] float fallDuration     = 1.6f;
    [Tooltip("Carpmadan sonra ekranin karanlik kaldigi sure.")]
    [SerializeField] float blackAfterImpact = 0.8f;
    [SerializeField] float openDuration     = 0.8f;
    [Tooltip("Ekran acildiktan sonra yerde yatma (goz kirpma) suresi.")]
    [SerializeField] float lieDuration      = 1.2f;
    [SerializeField] float standDuration    = 1.7f;
    [SerializeField] float hudFadeIn        = 0.4f;

    [Header("Kamera pozlari (varsayilan goz konumuna gore)")]
    [Tooltip("Yerde yatarken: yerel konum farki (m). Y eksi = yere yakin.")]
    [SerializeField] Vector3 lyingOffset = new Vector3(0.25f, -1.05f, 0f);
    [Tooltip("Yerde yatarken: pitch (eksi = yukari), yaw, roll (yan yatis).")]
    [SerializeField] Vector3 lyingEuler  = new Vector3(-8f, 20f, 80f);
    [Tooltip("Kalkarken ara poz: dizler ustunde, yere bakiyor.")]
    [SerializeField] Vector3 kneelOffset = new Vector3(0.05f, -0.55f, 0f);
    [SerializeField] Vector3 kneelEuler  = new Vector3(28f, 0f, 12f);
    [Tooltip("Kalkarken sendeleme genligi (derece).")]
    [SerializeField] float   standWobble = 3f;

    [Header("Bulaniklik")]
    [Tooltip("Baslangic bulanikligi (HDRP yakin odak mesafesi, m).")]
    [SerializeField] float blurStart     = 8f;
    [SerializeField] float vignetteStart = 0.55f;
    [SerializeField] int   blinkCount    = 2;

    [Header("Bitince")]
    public UnityEvent onFinished;

    PlayerMovement player;
    PlayerShoot    shoot;
    CameraShake    shake;
    Camera         playerCam;
    Transform      camT;
    Vector3        defaultCamLocal;

    HDAdditionalCameraData          hdCam;
    HDAdditionalCameraData.ClearColorMode prevClearMode;
    Color                           prevClearColor;
    int                             prevCullingMask;
    bool                            cameraDarkened;

    bool    locked;
    Vector3 poseOffset, poseEuler, joltOffset;
    bool    prevShakeEnabled;
    readonly List<Behaviour>   pausedAbilities = new List<Behaviour>();
    readonly List<CanvasGroup> hudGroups       = new List<CanvasGroup>();
    float   hudAlpha;

    Image        overlay;
    Volume       volume;
    VolumeProfile profile;
    Vignette     vignette;
    DepthOfField dof;
    AudioSource  audioSrc;

    void Start() => StartCoroutine(Run());

    IEnumerator Run()
    {
        // Diger Start'lar (HUD canvas'lari, silah, CameraShake taban konumu) otursun.
        if (!BeginLock()) yield break;
        yield return null;

        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        // 1) Dusus — siyah ekranda ruzgar.
        if (fallWindClip != null)
        {
            audioSrc.clip   = fallWindClip;
            audioSrc.volume = 0f;
            audioSrc.Play();
            float t = 0f;
            while (t < fallDuration)
            {
                t += Time.deltaTime;
                audioSrc.volume = fallWindVolume * Mathf.Clamp01(t / 0.4f) * Mathf.Lerp(0.7f, 1f, t / fallDuration);
                yield return null;
            }
            audioSrc.Stop();   // sert kesim: carpma ani
        }
        else if (fallDuration > 0f) yield return new WaitForSeconds(fallDuration);

        // 2) Pat + inleme + tek sert sarsinti.
        if (impactClip != null) audioSrc.PlayOneShot(impactClip, impactVolume);
        StartCoroutine(Jolt());
        if (gruntDelay > 0f) yield return new WaitForSeconds(gruntDelay);
        if (gruntClip != null) audioSrc.PlayOneShot(gruntClip, gruntVolume);

        // Sahneyi perde HALA kapaliyken cizmeye basla: otomatik pozlama karanliga gore
        // ayarlanip perde acilirken patlamasin.
        SetCameraRendering(true);

        float afterGrunt = blackAfterImpact - gruntDelay;
        if (afterGrunt > 0f) yield return new WaitForSeconds(afterGrunt);

        // 3) Ekran acilir — bulanik, yan yatik.
        yield return FadeOverlay(1f, 0f, openDuration);

        // 4) Yerde yatis: goz kirpmalar, bulaniklik biraz duzelir.
        float lieStart = Time.time;
        for (int i = 0; i < blinkCount; i++)
        {
            yield return new WaitForSeconds(lieDuration / (blinkCount + 1) * 0.6f);
            yield return FadeOverlay(0f, 0.95f, 0.09f);
            yield return new WaitForSeconds(0.05f);
            yield return FadeOverlay(0.95f, 0f, 0.14f);
            SetBlur(Mathf.Lerp(1f, 0.6f, (Time.time - lieStart) / Mathf.Max(0.01f, lieDuration)));
        }
        float rest = lieDuration - (Time.time - lieStart);
        if (rest > 0f) yield return new WaitForSeconds(rest);

        // 5) Sendeleyerek kalkis: yatis -> diz -> ayakta.
        float s = 0f;
        while (s < standDuration)
        {
            s += Time.deltaTime;
            float k = Mathf.Clamp01(s / standDuration);
            if (k < 0.5f)
            {
                float u = Mathf.SmoothStep(0f, 1f, k / 0.5f);
                poseOffset = Vector3.Lerp(lyingOffset, kneelOffset, u);
                poseEuler  = Vector3.Lerp(lyingEuler, kneelEuler, u);
            }
            else
            {
                float u = Mathf.SmoothStep(0f, 1f, (k - 0.5f) / 0.5f);
                poseOffset = Vector3.Lerp(kneelOffset, Vector3.zero, u);
                poseEuler  = Vector3.Lerp(kneelEuler, Vector3.zero, u);
            }
            poseEuler.z += Mathf.Sin(k * Mathf.PI * 3f) * standWobble * (1f - k);
            SetBlur(Mathf.Lerp(0.6f, 0f, k));
            yield return null;
        }

        // 6) Silah + kontrol + HUD.
        EndLock(true);
        float h = 0f;
        while (h < hudFadeIn)
        {
            h += Time.unscaledDeltaTime;
            SetHudAlpha(Mathf.Clamp01(h / Mathf.Max(0.01f, hudFadeIn)));
            yield return null;
        }
        SetHudAlpha(1f);
        Cleanup();
        onFinished?.Invoke();
    }

    // ── Kilit ──────────────────────────────────────────────────────────

    bool BeginLock()
    {
        player = FindFirstObjectByType<PlayerMovement>();
        if (player == null)
        {
            Debug.LogWarning("[FallIntroSequence] PlayerMovement bulunamadi — giris atlandi.", this);
            return false;
        }

        shoot = player.GetComponentInChildren<PlayerShoot>(true);
        shake = player.GetComponentInChildren<CameraShake>(true);
        playerCam = player.GetComponentInChildren<Camera>(true);
        var cam = playerCam;
        camT = shake != null ? shake.transform : cam != null ? cam.transform : null;
        if (camT == null)
        {
            Debug.LogWarning("[FallIntroSequence] Oyuncu kamerasi bulunamadi — giris atlandi.", this);
            return false;
        }
        // PlayerMovement kendi varsayilanini Awake'te aldi; biz de ayni degeri Start'ta aliyoruz.
        defaultCamLocal = camT.localPosition;

        if (shake != null) { prevShakeEnabled = shake.enabled; shake.enabled = false; }

        foreach (var b in new Behaviour[]
                 {
                     player.GetComponentInChildren<PlayerParry>(true),
                     player.GetComponentInChildren<GrapplingHook>(true),
                     player.GetComponentInChildren<StimulantSystem>(true),
                 })
        {
            if (b != null && b.enabled) { b.enabled = false; pausedAbilities.Add(b); }
        }

        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake  = false;
        audioSrc.spatialBlend = 0f;
        audioSrc.outputAudioMixerGroup = AudioRouting.Sfx;

        BuildOverlay();
        BuildVolume();
        SetBlur(1f);
        SetCameraRendering(false);   // siyahin altinda sahne isigi hic cizilmesin

        poseOffset = lyingOffset;
        poseEuler  = lyingEuler;
        hudAlpha   = 0f;
        locked     = true;   // poz ve kilit ayni karenin LateUpdate'inde (tum Start'lardan sonra) uygulanir
        return true;
    }

    void LateUpdate()
    {
        if (locked) EnforceLock();
    }

    // SceneFadeIn, silah Start'i ve PlayerLoadout.Apply bu degerleri geri yazabiliyor: her kare zorla.
    // LateUpdate'te: PlayerMovement'in bu kare kamerayi yazmis olabilecegi Update'ten SONRA.
    void EnforceLock()
    {
        if (player.enabled) player.enabled = false;
        if (shoot != null)
        {
            if (shoot.CanShoot)       shoot.CanShoot = false;
            if (!shoot.WeaponsHidden) shoot.WeaponsHidden = true;
        }
        CollectHud();
        SetHudAlpha(hudAlpha);

        camT.localPosition = defaultCamLocal + poseOffset + joltOffset;
        camT.localRotation = Quaternion.Euler(poseEuler);
    }

    void EndLock(bool drawWeapon)
    {
        if (!locked) return;
        locked = false;
        SetCameraRendering(true);
        if (player == null || camT == null) return;   // sahne kapanirken yok edilmis olabilir

        camT.localPosition = defaultCamLocal;
        camT.localRotation = Quaternion.identity;
        if (shake != null)
        {
            shake.SetBaseLocalPos(defaultCamLocal);
            shake.enabled = prevShakeEnabled;
        }

        player.SetLookPitch(0f);
        player.enabled = true;

        foreach (var b in pausedAbilities) if (b != null) b.enabled = true;
        pausedAbilities.Clear();

        // Eski degerleri elle geri yazmak yerine yukleme durumunu yeniden uygula: CanShoot,
        // silah gorunurlugu ve yetenekler PlayerLoadout'a gore duzelir (silahsiz bir sahnede
        // yanlislikla silah vermez). Silah gizli oldugu icin Apply onu ForceEquip ile
        // asagidan cekerek ele verir — "silahi ceker" ani bu.
        PlayerLoadout.Apply(player.gameObject);
        if (!drawWeapon && shoot != null && PlayerLoadout.AnyFirearm) shoot.WeaponsHidden = false;
    }

    void OnDisable()
    {
        // Yarida kesilirse (sahne kapanisi vb.) oyuncu kilitli ve ekran kara kalmasin.
        SetCameraRendering(true);
        if (locked && player != null) EndLock(false);
        SetHudAlpha(1f);
        Cleanup();
    }

    // Siyah perde tek basina yetmiyordu: kullanici perdenin ustunde sahne isigini hafifce
    // gorebiliyordu. Karanlik boyunca kamera HICBIR SEY cizmez (culling mask 0) ve ekrani
    // siyaha temizler. Camera.enabled kapatilmiyor — o zaman Game view "No cameras rendering"
    // yazisini basardi.
    void SetCameraRendering(bool on)
    {
        if (playerCam == null) return;

        if (!on)
        {
            if (cameraDarkened) return;
            cameraDarkened  = true;
            prevCullingMask = playerCam.cullingMask;
            playerCam.cullingMask = 0;

            hdCam = playerCam.GetComponent<HDAdditionalCameraData>();
            if (hdCam != null)
            {
                prevClearMode  = hdCam.clearColorMode;
                prevClearColor = hdCam.backgroundColorHDR;
                hdCam.clearColorMode    = HDAdditionalCameraData.ClearColorMode.Color;
                hdCam.backgroundColorHDR = Color.black;
            }
            return;
        }

        if (!cameraDarkened) return;
        cameraDarkened = false;
        playerCam.cullingMask = prevCullingMask;
        if (hdCam != null)
        {
            hdCam.clearColorMode     = prevClearMode;
            hdCam.backgroundColorHDR = prevClearColor;
        }
    }

    IEnumerator Jolt()
    {
        float t = 0f;
        const float dur = 0.25f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / dur);
            joltOffset = new Vector3(0f, -0.07f, 0f) * (k * k);
            yield return null;
        }
        joltOffset = Vector3.zero;
    }

    // ── HUD ────────────────────────────────────────────────────────────

    // HUD canvas'lari kendi Start'larinda kuruluyor; bulunana kadar her kare aranir.
    // WeaponHUD canvas.enabled'i her kare kendisi yazdigi icin gizleme CanvasGroup alpha ile.
    void CollectHud()
    {
        if (hudGroups.Count >= 3) return;
        AddHud(GameObject.Find("GameHUD_Canvas"));
        var weapon = FindFirstObjectByType<WeaponHUD>();
        if (weapon != null) AddHud(weapon.gameObject);
        var cross = FindFirstObjectByType<CrosshairHUD>();
        if (cross != null) AddHud(cross.gameObject);
    }

    void AddHud(GameObject go)
    {
        if (go == null) return;
        var g = go.GetComponent<CanvasGroup>();
        if (g == null) g = go.AddComponent<CanvasGroup>();
        if (!hudGroups.Contains(g)) hudGroups.Add(g);
    }

    void SetHudAlpha(float a)
    {
        hudAlpha = a;
        foreach (var g in hudGroups) if (g != null) g.alpha = a;
    }

    // ── Perde ve bulaniklik ────────────────────────────────────────────

    void BuildOverlay()
    {
        var go = new GameObject("FallIntroOverlay");
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OverlaySortingOrder;

        var imgGo = new GameObject("Siyah", typeof(RectTransform));
        imgGo.transform.SetParent(go.transform, false);
        var rt = (RectTransform)imgGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        // Ekrandan biraz TASIR: kenarlarda yuvarlama/olcekleme yuzunden ince bir seritten
        // sahne isigi sizmasin.
        rt.offsetMin = new Vector2(-100f, -100f);
        rt.offsetMax = new Vector2( 100f,  100f);
        overlay = imgGo.AddComponent<Image>();
        overlay.color = Color.black;
        overlay.raycastTarget = false;
    }

    IEnumerator FadeOverlay(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            if (overlay != null) overlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        if (overlay != null) overlay.color = new Color(0f, 0f, 0f, to);
    }

    // WakeUpEffect'teki kurulumun aynisi: global Volume + Vignette + yakin alan DoF.
    void BuildVolume()
    {
        volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 150f;   // SceneVolumeSetup (100) ustunde; sadece DoF + vinyet ekler

        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.profile = profile;

        vignette = profile.Add<Vignette>();
        vignette.active = true;
        vignette.color.Override(Color.black);
        vignette.intensity.Override(0f);

        dof = profile.Add<DepthOfField>();
        dof.active = true;
        dof.focusMode.Override(DepthOfFieldMode.Manual);
        dof.nearFocusStart.Override(0f);
        dof.nearFocusEnd.Override(0f);
    }

    void SetBlur(float amount)
    {
        if (vignette != null) vignette.intensity.value = vignetteStart * amount;
        if (dof      != null) dof.nearFocusEnd.value   = blurStart     * amount;
    }

    void Cleanup()
    {
        if (overlay != null) Destroy(overlay.canvas.gameObject);
        overlay = null;
        if (volume  != null) Destroy(volume);
        if (profile != null) Destroy(profile);
        volume = null;
        profile = null;
    }
}
}
