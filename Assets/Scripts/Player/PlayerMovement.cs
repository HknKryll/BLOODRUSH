using UnityEngine;
using Bloodrush.Flow;
using Bloodrush.Shared.Audio;

namespace Bloodrush.Player
{
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed  = 14f;
    [SerializeField] float airControl = 0.65f;
    [SerializeField] float gravity    = -30f;

    [Header("Jump")]
    [SerializeField] float jumpForce  = 13f;
    [SerializeField] float coyoteTime = 0.12f;

    [Header("Duvardan Zıplama")]
    [SerializeField] float wallJumpCheckDist    = 0.6f;
    [SerializeField] float wallJumpPushForce    = 11f;
    [SerializeField] float wallJumpLockDuration = 0.18f;
    [SerializeField] int   maxWallJumpsPerWall  = 3;
    [SerializeField] float wallJumpRefillTime   = 2f;
    [SerializeField] LayerMask wallJumpMask     = ~0;

    [Header("Duvarda Koşma (Wallride)")]
    [Tooltip("Sadece bu katmandaki yüzeylerde wall-run olur. Plane'ini bir 'Wallrun' layer'ına koyup burada seç.")]
    [SerializeField] LayerMask wallRunMask;
    [SerializeField] float wallRunCheckDist = 1f;
    [SerializeField] float wallRunSpeed     = 14f;
    [SerializeField] float wallRunMaxTime   = 2.5f;
    [SerializeField] float wallRunGravity   = -3f;   // koşarken hafif aşağı kayma
    [SerializeField] float wallRunJumpUp    = 12f;   // duvardan zıplayınca yukarı
    [SerializeField] float wallRunJumpOut   = 9f;    // duvardan zıplayınca dışa itiş
    [SerializeField] float wallRunTilt      = 14f;   // kamera yatması

    [Header("Slide")]
    [SerializeField] float   slideSpeed    = 22f;
    [Tooltip("Kayma sürtünmesi (hız/sn azalma). Küçük = daha uzun kayma.")]
    [SerializeField] float   slideFriction = 9f;
    [Tooltip("Bu hızın altına düşünce kayma biter (≈ yürüme hızı).")]
    [SerializeField] float   minSlideSpeed = 11f;
    [Tooltip("Güvenlik: en fazla bu kadar saniye kayılır (sonsuz kaymayı önler).")]
    [SerializeField] float   maxSlideTime  = 3f;
    [SerializeField] float   slideTilt     = 8f;

    [Header("Camera")]
    [SerializeField] Transform cameraHolder;
    [SerializeField] float sensitivityField = 2f;
    public float sensitivity { get => sensitivityField; set => sensitivityField = value; }
    [SerializeField] float maxPitch    = 85f;
    [Tooltip("Flip'te (tavanda) gözün tavanın altında ne kadar aşağıda olacağı. Ayaklar tavanda hissi için ayarla.")]
    [SerializeField] float flipEyeHeight = 1.6f;

    [Header("Ses")]
    [SerializeField] AudioClip footstepClip;
    [SerializeField] [Range(0f,1f)] float footstepVolume = 0.4f;
    [SerializeField] AudioClip jumpClip;
    [SerializeField] [Range(0f,1f)] float jumpVolume = 0.8f;
    [SerializeField] AudioClip landClip;
    [SerializeField] [Range(0f,1f)] float landVolume = 0.9f;
    [SerializeField] AudioClip slideClip;
    [SerializeField] [Range(0f,1f)] float slideVolume = 0.7f;

    CharacterController cc;
    Vector3 velocity;
    Vector3 launchVelocity;
    float   pitch;
    float   coyoteTimer;

    // Yerçekimi flip (yerçekimi odası): zıplayınca yerçekimi ters döner, tavan zemin olur
    bool           flipped;
    float          flipRoll;         // kamera hedef roll'ü (0 / 180)
    float          camRoll;          // kameranın anlık roll'ü (euler geri-okuma bug'ı olmasın)
    CollisionFlags lastFlags;        // flip zemin algısı için cc.Move sonucu
    Vector3        defaultCamLocal;  // normal göz yerel konumu; flip'te Y aynalanır
    Bloodrush.FX.CameraShake camShake; // varsa kamera taban konumunu buna veririz

    PlayerWallRun wallRun;
    PlayerSlide   slide;

    SfxPlayer sfx;
    float footstepTimer;
    bool  wasGrounded;

    void Awake()
    {
        cc           = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        float saved = PlayerPrefs.GetFloat("Sensitivity", sensitivity);
        if (saved >= 0.5f) sensitivity = saved;

        sfx = SfxPlayer.Create(gameObject, spatialBlend: 0f);

        wallRun = new PlayerWallRun(cc, transform,
            wallJumpMask, wallJumpCheckDist, wallJumpRefillTime, maxWallJumpsPerWall, wallJumpLockDuration,
            wallRunMask, wallRunCheckDist, wallRunSpeed, wallRunMaxTime, wallRunGravity, wallRunJumpOut);
        slide = new PlayerSlide(cc, slideSpeed, slideFriction, minSlideSpeed, maxSlideTime, moveSpeed);

        var cam = cameraHolder.GetComponentInChildren<Camera>();
        if (cam) cam.nearClipPlane = 0.05f;

        defaultCamLocal = cameraHolder.localPosition;
        camShake        = cameraHolder.GetComponentInChildren<Bloodrush.FX.CameraShake>();
    }

    // Kamera TABAN yerel konumu: normalde defaultCamLocal, flip'te göz tavanın (capsule
    // üst teması) flipEyeHeight kadar ALTINA iner — böylece tavanın içinde kalmaz.
    // TEK SAHİP burasıdır: CameraShake varsa tabanı ona veririz (o base+sarsıntı yazar,
    // yoksa CameraShake her kare 0.48'e geri ezip flip kamerasını bozuyordu). Yoksa
    // doğrudan yazarız.
    void ApplyCameraBase()
    {
        Vector3 baseLocal = defaultCamLocal;
        if (flipped)
            baseLocal.y = cc.center.y + cc.height * 0.5f - flipEyeHeight;

        if (camShake != null) camShake.SetBaseLocalPos(baseLocal);
        else                  cameraHolder.localPosition = baseLocal;
    }

    void Update()
    {
        Look();
        Move();

        slide.Tick(Input.GetKeyDown(KeyBindings.Jump), Input.GetKey(KeyBindings.Slide),
                   out bool slideEnded, out bool slideJumpedOut, out Vector3 slideLaunch);
        if (slideEnded)
        {
            launchVelocity = slideLaunch;
            if (slideJumpedOut) velocity.y = jumpForce;
        }
    }

    // Kamera tabanını her kare CameraShake'e ver (normal + flip) — CameraShake bunun
    // üstüne sarsıntı ekler, artık kavga yok (execution-order'a bağlı değil).
    void LateUpdate()
    {
        ApplyCameraBase();
    }

    void Look()
    {
        if (Time.timeScale == 0f) return;
        float mx = Input.GetAxisRaw("Mouse X") * sensitivity;
        float my = Input.GetAxisRaw("Mouse Y") * sensitivity;

        // Flip'te bakış tersine döner (ekran 180° dönük olduğu için kontroller ters gelmesin)
        if (flipped) { mx = -mx; my = -my; }

        pitch -= my;
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

        float roll = FlipEnabled     ? flipRoll
                   : slide.IsSliding ? -slideTilt
                   : wallRun.Running ? wallRun.Side * wallRunTilt
                   : 0f;
        float rollLerp = FlipEnabled ? 3.5f : 10f;   // flip'te daha yavaş/yumuşak 180° dönüş
        // Roll'ü kendi float'ımızda izle — euler'dan geri okumak (pitch≠0'da) bozuk
        // değer verip 180°'e oturmasını engelliyordu.
        camRoll = Mathf.LerpAngle(camRoll, roll, Time.deltaTime * rollLerp);
        cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, camRoll);
        transform.Rotate(Vector3.up * mx);
    }

    void Move()
    {
        if (FlipEnabled) { MoveFlip(); return; }   // yerçekimi odası — izole flip yolu

        bool grounded = cc.isGrounded;
        wallRun.CheckWall();

        // İniş sesi
        if (grounded && !wasGrounded && velocity.y < -2f)
            sfx.Play(landClip, landVolume);
        wasGrounded = grounded;

        if (grounded)
        {
            coyoteTimer = coyoteTime;
            if (velocity.y < 0f) velocity.y = -2f;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 wish = transform.right * h + transform.forward * v;
        if (wish.magnitude > 1f) wish.Normalize();

        // ── Wall-run (duvarda koşma) ──
        if (wallRun.Running) { TickWallRun(); return; }
        if (!grounded && velocity.y < 2f) wallRun.TryStart(v > 0.1f);
        if (wallRun.Running) { TickWallRun(); return; }

        if (!slide.IsSliding && Time.time >= wallRun.WallJumpLockUntil)
        {
            float control = grounded ? 1f : airControl;
            cc.Move(wish * moveSpeed * SpeedMultiplier * control * Time.deltaTime);
        }

        // Adım sesi
        if (grounded && !slide.IsSliding && wish.magnitude > 0.1f)
        {
            footstepTimer -= Time.deltaTime;
            if (footstepTimer <= 0f)
            {
                sfx.Play(footstepClip, footstepVolume);
                footstepTimer = 0.32f;
            }
        }
        else
        {
            footstepTimer = 0f;
        }

        bool jumpAllowed = coyoteTimer > 0f || Time.time < slide.SlideJumpDeadline || wallRun.CanWallJump;
        if (Input.GetKeyDown(KeyBindings.Jump) && jumpAllowed && JumpEnabled)
        {
            if (wallRun.CanWallJump)
                Launch(wallRun.ConsumeWallJump() * wallJumpPushForce); // duvardan yatay itiş

            velocity.y  = jumpForce * JumpMultiplier;
            coyoteTimer = 0f;
            slide.ConsumeJumpDeadline();
            sfx.Play(jumpClip, jumpVolume);
        }

        if (!DisableGravity)
            velocity.y += gravity * GravityScale * Time.deltaTime;

        var moveFlags = cc.Move(velocity * Time.deltaTime);
        // Tavana/bir platformun altına kafa çarpınca yukarı hız hemen sıfırlansın —
        // yoksa velocity.y pozitif kalmaya devam edip yerçekimi onu ancak birkaç kare
        // içinde yavaşça düşürüyordu (oyuncu tavana "yapışıp" zıplama süresi bitene
        // kadar yukarı bakar halde kalıyordu). Sıfırlanınca bir sonraki karede
        // yerçekimi hemen devreye girip anında düşmeye başlar.
        if ((moveFlags & CollisionFlags.Above) != 0 && velocity.y > 0f)
            velocity.y = 0f;

        if (launchVelocity.sqrMagnitude > 0.1f)
        {
            cc.Move(launchVelocity * Time.deltaTime);
            float drag = grounded ? 22f : 2f;
            launchVelocity = Vector3.Lerp(launchVelocity, Vector3.zero, Time.deltaTime * drag);
        }
        else if (launchVelocity != Vector3.zero)
        {
            launchVelocity = Vector3.zero;
            if (grounded) coyoteTimer = coyoteTime; // zıplama hakkını geri ver
        }

        if (SlideEnabled && slide.CanStart(Input.GetKeyDown(KeyBindings.Slide), grounded, wish))
        {
            slide.Start(wish, new Vector2(cc.velocity.x, cc.velocity.z).magnitude);
            sfx.Play(slideClip, slideVolume);
        }
    }

    // Yerçekimi odası hareketi: zıplama = FLIP (yerçekimi yönü ters döner, tavan zemin
    // olur). Basit tutuldu — wallrun/slide devre dışı. Zemin algısı cc.Move'un
    // CollisionFlags'inden (aşağı/yukarı çarpma). Kamera Look()'ta 180° döner.
    void MoveFlip()
    {
        int  gs       = flipped ? -1 : 1;   // yerçekimi yönü işareti (aşağı=+, yukarı=−)
        bool grounded = flipped ? (lastFlags & CollisionFlags.Above) != 0
                                : ((lastFlags & CollisionFlags.Below) != 0 || cc.isGrounded);

        bool jumped = false;
        if (Input.GetKeyDown(KeyBindings.Jump) && JumpEnabled)
        {
            flipped    = !flipped;              // yerçekimini ters çevir
            velocity.y = 0f;                    // yeni yüzeye taze düşüş
            flipRoll   = flipped ? 180f : 0f;
            ApplyCameraBase();                  // gözü yeni tarafa aynala (hemen)
            sfx.Play(jumpClip, jumpVolume);
            jumped = true;
        }

        if (!jumped)
        {
            velocity.y += gravity * gs * Time.deltaTime;   // yönlü yerçekimi
            velocity.y  = Mathf.Clamp(velocity.y, -14f, 14f);   // tünelleme önle (ince tavandan geçmesin)
            if (grounded) velocity.y = -gs * 2f;           // yüzeye hafif baskı (aşağı −2 / yukarı +2)
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        if (flipped) h = -h;   // 180° roll'da sağ-sol görsel olarak ters — A/D'yi eşle
        Vector3 wish = transform.right * h + transform.forward * v;
        if (wish.magnitude > 1f) wish.Normalize();

        Vector3 move = wish * (moveSpeed * SpeedMultiplier) + Vector3.up * velocity.y;
        lastFlags = cc.Move(move * Time.deltaTime);
    }

    void TickWallRun()
    {
        wallRun.Update(Input.GetKeyDown(KeyBindings.Jump) && JumpEnabled, out bool jumpedOff, out Vector3 jumpOutHorizontal);
        if (jumpedOff)
        {
            velocity.y = wallRunJumpUp * JumpMultiplier;
            Launch(jumpOutHorizontal);
            sfx.Play(jumpClip, jumpVolume);
        }
    }

    public Vector3 Velocity        => cc.velocity;
    public bool    IsGrounded      => cc.isGrounded;
    public bool    IsSliding       => slide.IsSliding;
    public bool    DisableGravity  { get; set; }
    public float   GravityScale    { get; set; } = 1f;   // yerçekimi bölgeleri (düşük-g) çarpanı
    public float   SpeedMultiplier { get; set; } = 1f;
    public bool    JumpEnabled     { get; set; } = true;
    // Zıplama gücü çarpanı — 1 = normal, 0.5 ≈ yaralı/bitkin (asansör kazası sonrası).
    // JumpEnabled'dan ayrı: o "hiç zıplayamaz", bu "zar zor zıplar".
    public float   JumpMultiplier  { get; set; } = 1f;
    public bool    SlideEnabled    { get; set; } = true;
    public bool    FlipEnabled     { get; private set; }   // yerçekimi flip modu aktif mi
    public CharacterController Controller => cc;

    // GravityFlipZone çağırır — odaya girince flip modu açılır, çıkınca normale döner
    public void SetFlipMode(bool on)
    {
        FlipEnabled = on;
        if (!on) { flipped = false; flipRoll = 0f; ApplyCameraBase(); }
    }

    // Yerçekimi bölgesi (anti-grav kolonu) yukarı itiş uygular — GravityZone çağırır
    public void AddUpdraft(float upSpeed)
    {
        if (velocity.y < upSpeed)
            velocity.y = Mathf.MoveTowards(velocity.y, upSpeed, 60f * Time.deltaTime);
    }

    public void Launch(Vector3 v)
    {
        launchVelocity = new Vector3(v.x, 0f, v.z);
        if (v.y > 0f) velocity.y = v.y;
    }

    public void StopMomentum() => launchVelocity = Vector3.zero;

    // Duvarda asılı kalırken kaymayı önler
    public void ZeroVerticalVelocity() => velocity.y = 0f;

    // Sinematik sonrası bakış açısını dışarıdan ayarlamak için (ör. asansör kazasından
    // sonra karanlık odada hafif aşağı bakarak uyanmak). pitch private ve Teleport() onu
    // SIFIRLAMIYOR — bu olmadan oyuncu kabine girerken hangi açıyla bakıyorsa öyle uyanır.
    public void SetLookPitch(float degrees) => pitch = Mathf.Clamp(degrees, -maxPitch, maxPitch);

    // Checkpoint ışınlaması — CharacterController'ı kapatıp aç (yoksa transform ezilir)
    public void Teleport(Vector3 pos, Quaternion rot)
    {
        cc.enabled = false;
        transform.SetPositionAndRotation(pos, rot);
        velocity       = Vector3.zero;
        launchVelocity = Vector3.zero;
        // Işınlama sonrası ters-dönük/eğik kalma — flip görselini sıfırla (FlipEnabled'e
        // dokunma; bölge içine ışınlandıysak flip modu açık kalmalı).
        flipped  = false;
        flipRoll = 0f;
        camRoll  = 0f;
        ApplyCameraBase();
        cc.enabled = true;
    }

}
}
