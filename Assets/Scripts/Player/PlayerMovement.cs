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
    [SerializeField] KeyCode slideKey      = KeyCode.LeftControl;
    [SerializeField] float   slideSpeed    = 22f;
    [SerializeField] float   slideDuration = 0.65f;
    [SerializeField] float   slideTilt     = 8f;

    [Header("Camera")]
    [SerializeField] Transform cameraHolder;
    [SerializeField] float sensitivityField = 2f;
    public float sensitivity { get => sensitivityField; set => sensitivityField = value; }
    [SerializeField] float maxPitch    = 85f;

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
        slide = new PlayerSlide(cc, slideSpeed, slideDuration, moveSpeed);

        var cam = cameraHolder.GetComponentInChildren<Camera>();
        if (cam) cam.nearClipPlane = 0.05f;
    }

    void Update()
    {
        Look();
        Move();

        slide.Tick(Input.GetButtonDown("Jump"), out bool slideEnded, out bool slideJumpedOut, out Vector3 slideLaunch);
        if (slideEnded)
        {
            launchVelocity = slideLaunch;
            if (slideJumpedOut) velocity.y = jumpForce;
        }
    }

    void Look()
    {
        if (Time.timeScale == 0f) return;
        float mx = Input.GetAxisRaw("Mouse X") * sensitivity;
        float my = Input.GetAxisRaw("Mouse Y") * sensitivity;

        pitch -= my;
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

        float tilt = slide.IsSliding  ? -slideTilt
                   : wallRun.Running  ? wallRun.Side * wallRunTilt
                   : 0f;
        cameraHolder.localRotation = Quaternion.Euler(pitch, 0f,
            Mathf.LerpAngle(cameraHolder.localEulerAngles.z, tilt, Time.deltaTime * 10f));
        transform.Rotate(Vector3.up * mx);
    }

    void Move()
    {
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
        if (Input.GetButtonDown("Jump") && jumpAllowed && JumpEnabled)
        {
            if (wallRun.CanWallJump)
                Launch(wallRun.ConsumeWallJump() * wallJumpPushForce); // duvardan yatay itiş

            velocity.y  = jumpForce;
            coyoteTimer = 0f;
            slide.ConsumeJumpDeadline();
            sfx.Play(jumpClip, jumpVolume);
        }

        if (!DisableGravity)
            velocity.y += gravity * Time.deltaTime;

        cc.Move(velocity * Time.deltaTime);

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

        bool slideKeyDown = Input.GetKeyDown(slideKey);
        bool slideKeyHeld = Input.GetKey(slideKey);
        if (SlideEnabled && slide.CanStart(slideKeyDown, slideKeyHeld, grounded, wish))
        {
            slide.Start(wish);
            sfx.Play(slideClip, slideVolume);
        }
    }

    void TickWallRun()
    {
        wallRun.Update(Input.GetButtonDown("Jump") && JumpEnabled, out bool jumpedOff, out Vector3 jumpOutHorizontal);
        if (jumpedOff)
        {
            velocity.y = wallRunJumpUp;
            Launch(jumpOutHorizontal);
            sfx.Play(jumpClip, jumpVolume);
        }
    }

    public Vector3 Velocity        => cc.velocity;
    public bool    IsGrounded      => cc.isGrounded;
    public bool    IsSliding       => slide.IsSliding;
    public bool    DisableGravity  { get; set; }
    public float   SpeedMultiplier { get; set; } = 1f;
    public bool    JumpEnabled     { get; set; } = true;
    public bool    SlideEnabled    { get; set; } = true;
    public CharacterController Controller => cc;

    public void Launch(Vector3 v)
    {
        launchVelocity = new Vector3(v.x, 0f, v.z);
        if (v.y > 0f) velocity.y = v.y;
    }

    public void StopMomentum() => launchVelocity = Vector3.zero;

    // Duvarda asılı kalırken kaymayı önler
    public void ZeroVerticalVelocity() => velocity.y = 0f;

    // Checkpoint ışınlaması — CharacterController'ı kapatıp aç (yoksa transform ezilir)
    public void Teleport(Vector3 pos, Quaternion rot)
    {
        cc.enabled = false;
        transform.SetPositionAndRotation(pos, rot);
        velocity       = Vector3.zero;
        launchVelocity = Vector3.zero;
        cc.enabled = true;
    }

}
}
