using UnityEngine;

namespace Bloodrush.Player
{
public class ProceduralWeaponMotion : MonoBehaviour
{
    [Header("Silah Objesi")]
    [SerializeField] Transform weaponHolder;

    [Header("Bob (Koşma Sallanması)")]
    [SerializeField] float bobFrequency  = 8f;
    [SerializeField] float bobAmplitudeX = 0.04f;
    [SerializeField] float bobAmplitudeY = 0.025f;
    [SerializeField] float bobSmoothing  = 12f;

    [Header("Recoil (Geri Tepme)")]
    [Tooltip("Ateşte namlunun yukarı kalkma açısı (derece). Ekranı dolduran silahta EN görünür etki budur — asıl tepme hissi buradan gelir.")]
    [SerializeField] float recoilPitch      = 9f;
    [Tooltip("Ateşte silahın geriye (kameraya) doğru itilmesi.")]
    [SerializeField] float recoilBack       = 0.10f;
    [Tooltip("Ateşte silahın hafif yukarı zıplaması.")]
    [SerializeField] float recoilUp         = 0.04f;
    [Tooltip("Tepmenin tepe noktasına ne kadar hızlı ulaştığı (büyük = daha ani/sert).")]
    [SerializeField] float recoilSnappiness = 20f;
    [Tooltip("Tepmenin sıfıra ne kadar hızlı döndüğü (küçük = daha yavaş, daha görünür).")]
    [SerializeField] float recoilReturn     = 7f;

    [Header("Silah Çekiş/İndirme (Draw/Holster)")]
    [Tooltip("Çekiş/indirme sırasında silahın ekrandan dikey çıkış mesafesi.")]
    [SerializeField] float drawDropDown  = 0.4f;
    [Tooltip("Çıkış yönünde namlu eğimi (derece).")]
    [SerializeField] float drawTiltAngle = 45f;
    [Tooltip("Yana (sağa) kayma — bel kılıfı / omuz askısı sağda hissi verir.")]
    [SerializeField] float drawSideOffset = 0.22f;
    [Tooltip("Bilek dönmesi (derece) — kılıftaki silah yan durur, çekerken düzelir. Ters his için negatif yap.")]
    [SerializeField] float drawRollAngle  = 35f;
    [Tooltip("İndirme (holster) hızı — birim/sn. Büyük = daha hızlı iner.")]
    [SerializeField] float holsterSpeed  = 7f;
    [Tooltip("Çekme (draw) hızı — birim/sn. Büyük = daha hızlı çekilir.")]
    [SerializeField] float drawSpeed     = 9f;

    [Header("Slide")]
    [SerializeField] float slideLower     = 0.25f;
    [SerializeField] float slideRollAngle = 25f;

    [Header("Kanca")]
    [SerializeField] float grappleExtend  = 0.12f;

    [Header("Fare Sway")]
    [SerializeField] float swayAmount     = 0.06f;
    [SerializeField] float swaySmoothing  = 8f;

    [Header("Duvar Koruması")]
    [SerializeField] float wallCheckDist   = 1.5f;
    [SerializeField] float wallRetractDist = 0.45f;
    [SerializeField] LayerMask wallMask    = ~0;

    PlayerMovement movement;
    GrapplingHook  hook;
    Camera         cam;

    Vector3    defaultPos;
    Quaternion defaultRot;
    Vector3    currentPos;
    float      bobTimer;
    Vector3    swayPos;
    bool       poseInitialized;

    // ── Recoil yay durumu ──
    // "target" ateşte anlık zıplar, sonra sıfıra doğru sönümlenir (recoilReturn).
    // "current" hedefi hızla takip eder (recoilSnappiness). Bu iki aşama, göz
    // görecek kadar tutan ama yumuşak dönen klasik tepme hissini verir.
    Vector3 recoilPosTarget,  recoilPosCurrent;   // pozisyon ofseti (kamera uzayı)
    float   recoilPitchTarget, recoilPitchCurrent; // namlu yukarı açısı (derece)

    // Sahnedeki tek silah-hareket motoru. PlayerShoot hangi objede olursa olsun
    // (Inspector referansı kopmuş olsa bile) recoil'i buradan bulur — silah
    // kurulumundaki referans kopuklukları recoil'i bir daha öldüremesin.
    public static ProceduralWeaponMotion Instance { get; private set; }
    void OnEnable()  { Instance = this; }
    void OnDisable() { if (Instance == this) Instance = null; }

    // ── Çekiş/İndirme durumu ──
    // holsterAmount: 0 = silah normal pozunda, 1 = tam ekran dışında.
    // holsterDir: +1 = ekranın ALTI üzerinden (revolver), -1 = ÜSTÜ üzerinden (diğerleri).
    // Silah değişiminde PlayerShoot önce Holster(yön) (hedef=1), en uçta model değişir,
    // sonra PlayDraw(yön) (anı=1, hedef=0) ile yeni silah kendi yönünden çekilir.
    float holsterAmount;
    float holsterTarget;
    float holsterDir = 1f;

    public void Holster(bool viaBottom = true)
    {
        holsterDir    = viaBottom ? 1f : -1f;
        holsterTarget = 1f;
    }

    public void PlayDraw(bool fromBottom = true)
    {
        holsterDir    = fromBottom ? 1f : -1f;
        holsterAmount = 1f;
        holsterTarget = 0f;
    }

    void Start()
    {
        movement   = GetComponentInParent<PlayerMovement>();
        hook       = GetComponentInParent<GrapplingHook>();
        cam        = GetComponentInParent<Camera>();
        if (cam == null) cam = Camera.main;

        // weaponHolder atanmamışsa kameranın altındaki "WeaponHolder" objesini bul
        if (weaponHolder == null && cam != null)
            weaponHolder = cam.transform.Find("WeaponHolder");
        if (weaponHolder == null) weaponHolder = transform;
    }

    void LateUpdate()
    {
        // Varsayılan pozu ilk karede yakala (Start()'ta değil) — weaponHolder'ı
        // Start()'ında konumlandıran başka bir script varsa bile (Unity Start()
        // çağrı sırasını garanti etmez) doğru başlangıç pozunu almış oluruz.
        if (!poseInitialized)
        {
            defaultPos = weaponHolder.localPosition;
            defaultRot = weaponHolder.localRotation;
            currentPos = defaultPos;
            poseInitialized = true;
        }

        Vector3 vel     = movement.Velocity;
        float   speed   = new Vector2(vel.x, vel.z).magnitude;
        bool    ground  = movement.IsGrounded;
        bool    slide   = movement.IsSliding;
        bool    grapple = hook != null && hook.IsHooked;

        // ── Bob ──────────────────────────────
        Vector3 target = defaultPos;

        if (speed > 1f && ground && !slide)
        {
            bobTimer += Time.deltaTime * bobFrequency;
            target += new Vector3(
                Mathf.Sin(bobTimer)            * bobAmplitudeX,
                Mathf.Abs(Mathf.Sin(bobTimer)) * bobAmplitudeY,
                0f);
        }
        else
        {
            bobTimer = Mathf.Lerp(bobTimer, 0f, Time.deltaTime * 6f);
        }

        // ── Slide ────────────────────────────
        float slideRoll = 0f;
        if (slide)
        {
            target   += Vector3.down * slideLower;
            slideRoll = slideRollAngle;
        }

        // ── Kanca ────────────────────────────
        if (grapple)
            target += Vector3.forward * grappleExtend;

        // ── Fare Sway ─────────────────────────
        float mx = Input.GetAxisRaw("Mouse X");
        float my = Input.GetAxisRaw("Mouse Y");
        Vector3 swayTarget = new Vector3(-mx, -my, 0f) * swayAmount;
        swayPos = Vector3.Lerp(swayPos, swayTarget, Time.deltaTime * swaySmoothing);

        // ── Recoil yay güncellemesi ──────────
        // Hedef sıfıra sönümlenir, uygulanan değer hedefi hızla takip eder.
        recoilPosTarget    = Vector3.Lerp(recoilPosTarget,    Vector3.zero, Time.deltaTime * recoilReturn);
        recoilPosCurrent   = Vector3.Lerp(recoilPosCurrent,   recoilPosTarget,   Time.deltaTime * recoilSnappiness);
        recoilPitchTarget  = Mathf.Lerp (recoilPitchTarget,   0f,                Time.deltaTime * recoilReturn);
        recoilPitchCurrent = Mathf.Lerp (recoilPitchCurrent,  recoilPitchTarget, Time.deltaTime * recoilSnappiness);

        // ── Çekiş/İndirme ilerlemesi ─────────
        // Sabit hızla (MoveTowards) yürür — süre öngörülebilir; PlayerShoot model
        // değişimini switchHolsterTime'a göre zamanlıyor.
        float hSpeed = holsterTarget > holsterAmount ? holsterSpeed : drawSpeed;
        holsterAmount = Mathf.MoveTowards(holsterAmount, holsterTarget, Time.deltaTime * hSpeed);

        // ── Duvar Koruması ───────────────────
        float wallPush = 0f;
        if (cam != null && Physics.Raycast(weaponHolder.position, cam.transform.forward,
                out RaycastHit wallHit, wallCheckDist, wallMask, QueryTriggerInteraction.Ignore))
        {
            float t = 1f - (wallHit.distance / wallCheckDist);
            wallPush = wallRetractDist * t;
        }

        // ── Uygula ───────────────────────────
        // Çekiş ofseti recoil'in üstüne, doğrudan (bob lerp'ine karışmadan) biner.
        // holsterDir yönü belirler: +1 = belden (alt-sağ), -1 = omuzdan (üst-sağ).
        // Yana kayma + bilek dönmesi, düz kaymayı "kılıftan/omuzdan çekme" hissine çevirir.
        currentPos = Vector3.Lerp(currentPos, target, Time.deltaTime * bobSmoothing);
        weaponHolder.localPosition = currentPos + recoilPosCurrent
                                   + Vector3.down  * (drawDropDown   * holsterAmount * holsterDir)
                                   + Vector3.right * (drawSideOffset * holsterAmount)
                                   + Vector3.back * wallPush + swayPos;

        // Pitch: çekişte namlu gidiş yönüne eğilir (belde aşağı, omuzda yukarı), recoil
        // hep YUKARI kaldırır (−X). Roll: kılıfta/askıda yan duran silah çekilirken
        // düzelir. İkisi de kamera uzayında (ön-çarpım) — modelin garip local
        // eksenlerinden bağımsız. Slide roll silahın kendi eksenine (art-çarpım) gider.
        float drawPitch = drawTiltAngle * holsterAmount * holsterDir;
        float drawRoll  = drawRollAngle * holsterAmount * holsterDir;
        weaponHolder.localRotation =
            Quaternion.Euler(drawPitch - recoilPitchCurrent, 0f, drawRoll)
            * defaultRot * Quaternion.Euler(0f, 0f, slideRoll);
    }

    // scale: silah başına tepme çarpanı (PlayerShoot'tan gelir).
    // 1 = temel değerler; shotgun >1 (sert), LMG <1 (hafif ama seri).
    public void ApplyRecoil(float scale = 1f)
    {
        recoilPosTarget   += new Vector3(0f, recoilUp * scale, -recoilBack * scale);
        recoilPitchTarget += recoilPitch * scale;
    }
}
}
