using UnityEngine;

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

    bool    isSliding;
    float   slideTimer;
    Vector3 slideDir;
    float   normalHeight;
    Vector3 normalCenter;
    float   slideEndTime     = -1f;
    float   slideJumpDeadline;

    AudioSource audioSrc;
    float footstepTimer;
    bool  wasGrounded;

    bool    touchingWall;
    Vector3 wallNormal;
    float   wallJumpLockUntil;
    int      wallJumpsRemaining;
    Collider lastWallCollider;
    float    lastWallJumpTime = -999f;

    bool    wallRunning;
    Vector3 wallRunNormal;
    int     wallRunSide;    // +1 sağ duvar, -1 sol duvar
    float   wallRunTimer;
    static readonly int[] wallSides = { 1, -1 };

    void Awake()
    {
        cc           = GetComponent<CharacterController>();
        normalHeight = cc.height;
        normalCenter = cc.center;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        float saved = PlayerPrefs.GetFloat("Sensitivity", sensitivity);
        if (saved >= 0.5f) sensitivity = saved;

        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.spatialBlend = 0f;

        var cam = cameraHolder.GetComponentInChildren<Camera>();
        if (cam) cam.nearClipPlane = 0.05f;

        wallJumpsRemaining = maxWallJumpsPerWall;
    }

    void Update()
    {
        Look();
        Move();
        UpdateSlide();
    }

    void Look()
    {
        if (Time.timeScale == 0f) return;
        float mx = Input.GetAxisRaw("Mouse X") * sensitivity;
        float my = Input.GetAxisRaw("Mouse Y") * sensitivity;

        pitch -= my;
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

        float tilt = isSliding    ? -slideTilt
                   : wallRunning  ? wallRunSide * wallRunTilt
                   : 0f;
        cameraHolder.localRotation = Quaternion.Euler(pitch, 0f,
            Mathf.LerpAngle(cameraHolder.localEulerAngles.z, tilt, Time.deltaTime * 10f));
        transform.Rotate(Vector3.up * mx);
    }

    void CheckWall()
    {
        touchingWall = false;
        Vector3 origin = transform.position + cc.center;
        Vector3[] dirs = { transform.forward, -transform.forward, transform.right, -transform.right };

        foreach (var dir in dirs)
        {
            if (Physics.Raycast(origin, dir, out RaycastHit hit, wallJumpCheckDist + cc.radius,
                    wallJumpMask, QueryTriggerInteraction.Ignore))
            {
                touchingWall = true;
                wallNormal   = hit.normal;
                wallNormal.y = 0f;
                wallNormal.Normalize();

                // Farklı duvar ya da son zıplamadan bu yana yeterince zaman geçtiyse hakkı yenile
                if (hit.collider != lastWallCollider || Time.time - lastWallJumpTime >= wallJumpRefillTime)
                {
                    wallJumpsRemaining = maxWallJumpsPerWall;
                    lastWallCollider   = hit.collider;
                }
                break;
            }
        }
    }

    // Havadayken yan tarafta işaretli (wallRunMask) bir yüzey varsa wall-run başlat
    void TryStartWallRun()
    {
        if (wallRunMask == 0) return;                       // yapılandırılmamış
        if (Input.GetAxisRaw("Vertical") <= 0.1f) return;   // ileri gitmiyorsan başlama

        Vector3 origin = transform.position + cc.center;
        foreach (int side in wallSides)
        {
            if (Physics.Raycast(origin, transform.right * side, out RaycastHit hit,
                    wallRunCheckDist + cc.radius, wallRunMask, QueryTriggerInteraction.Ignore))
            {
                wallRunning   = true;
                wallRunNormal = hit.normal;
                wallRunSide   = side;
                wallRunTimer  = wallRunMaxTime;
                velocity.y    = 0f;
                return;
            }
        }
    }

    void UpdateWallRun()
    {
        wallRunTimer -= Time.deltaTime;

        Vector3 origin = transform.position + cc.center;
        bool onWall = Physics.Raycast(origin, transform.right * wallRunSide, out RaycastHit hit,
                        wallRunCheckDist + cc.radius + 0.3f, wallRunMask, QueryTriggerInteraction.Ignore);
        if (onWall) wallRunNormal = hit.normal;

        // Bitiş: duvar bitti / süre doldu / yere değdi
        if (!onWall || wallRunTimer <= 0f || cc.isGrounded)
        {
            wallRunning = false;
            return;
        }

        // Duvardan zıpla (dışa + yukarı)
        if (Input.GetButtonDown("Jump") && JumpEnabled)
        {
            velocity.y = wallRunJumpUp;
            Launch(wallRunNormal * wallRunJumpOut);
            wallRunning = false;
            Play(jumpClip, jumpVolume);
            return;
        }

        // Duvar boyunca yatay yön, oyuncunun baktığı yöne hizalı
        Vector3 along = Vector3.Cross(wallRunNormal, Vector3.up).normalized;
        if (Vector3.Dot(along, transform.forward) < 0f) along = -along;

        Vector3 move = along * wallRunSpeed
                     + (-wallRunNormal) * 2f          // duvara yapış
                     + Vector3.up * wallRunGravity;   // hafif aşağı kayma
        cc.Move(move * Time.deltaTime);
    }

    void Move()
    {
        bool grounded = cc.isGrounded;
        CheckWall();

        // İniş sesi
        if (grounded && !wasGrounded && velocity.y < -2f)
            Play(landClip, landVolume);
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
        if (wallRunning) { UpdateWallRun(); return; }
        if (!grounded && velocity.y < 2f) TryStartWallRun();
        if (wallRunning) { UpdateWallRun(); return; }

        if (!isSliding && Time.time >= wallJumpLockUntil)
        {
            float control = grounded ? 1f : airControl;
            cc.Move(wish * moveSpeed * SpeedMultiplier * control * Time.deltaTime);
        }

        // Adım sesi
        if (grounded && !isSliding && wish.magnitude > 0.1f)
        {
            footstepTimer -= Time.deltaTime;
            if (footstepTimer <= 0f)
            {
                Play(footstepClip, footstepVolume);
                footstepTimer = 0.32f;
            }
        }
        else
        {
            footstepTimer = 0f;
        }

        bool canWallJump = touchingWall && wallJumpsRemaining > 0;
        bool jumpAllowed = coyoteTimer > 0f || Time.time < slideJumpDeadline || canWallJump;
        if (Input.GetButtonDown("Jump") && jumpAllowed && JumpEnabled)
        {
            if (canWallJump)
            {
                Launch(wallNormal * wallJumpPushForce); // duvardan yatay itiş
                wallJumpsRemaining--;
                lastWallJumpTime  = Time.time;
                wallJumpLockUntil = Time.time + wallJumpLockDuration;
            }

            velocity.y        = jumpForce;
            coyoteTimer       = 0f;
            slideJumpDeadline = 0f;
            Play(jumpClip, jumpVolume);
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

        bool slideInput = SlideEnabled && (Input.GetKeyDown(slideKey) ||
                          (Input.GetKey(slideKey) && Time.time - slideEndTime < 0.15f));
        if (slideInput && grounded && !isSliding && wish.magnitude > 0.1f)
        {
            isSliding  = true;
            slideTimer = slideDuration;
            slideDir   = wish.normalized;
            cc.height  = normalHeight * 0.5f;
            cc.center  = new Vector3(0f, normalCenter.y * 0.5f, 0f);
            Play(slideClip, slideVolume);
        }
    }

    void UpdateSlide()
    {
        if (!isSliding) return;

        slideTimer -= Time.deltaTime;
        cc.Move(slideDir * slideSpeed * Time.deltaTime);

        bool jumpPressed = Input.GetButtonDown("Jump");

        if (slideTimer <= 0f || jumpPressed)
        {
            launchVelocity = slideDir * moveSpeed;
            StopSlide();
            if (jumpPressed)
                velocity.y = jumpForce;
            else
                slideJumpDeadline = Time.time + 0.3f;
        }
    }

    void StopSlide()
    {
        isSliding   = false;
        slideEndTime = Time.time;
        cc.height   = normalHeight;
        cc.center   = normalCenter;
    }

    public Vector3 Velocity        => cc.velocity;
    public bool    IsGrounded      => cc.isGrounded;
    public bool    IsSliding       => isSliding;
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

    void Play(AudioClip clip, float vol = 1f) { if (clip) audioSrc.PlayOneShot(clip, vol); }
}
