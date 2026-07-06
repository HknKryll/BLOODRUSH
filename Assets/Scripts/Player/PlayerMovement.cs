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

    void Start()
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

        float tilt = isSliding ? -slideTilt : 0f;
        cameraHolder.localRotation = Quaternion.Euler(pitch, 0f,
            Mathf.LerpAngle(cameraHolder.localEulerAngles.z, tilt, Time.deltaTime * 10f));
        transform.Rotate(Vector3.up * mx);
    }

    void Move()
    {
        bool grounded = cc.isGrounded;

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

        if (!isSliding)
        {
            float control = grounded ? 1f : airControl;
            cc.Move(wish * moveSpeed * control * Time.deltaTime);
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

        bool jumpAllowed = coyoteTimer > 0f || Time.time < slideJumpDeadline;
        if (Input.GetButtonDown("Jump") && jumpAllowed)
        {
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

        bool slideInput = Input.GetKeyDown(slideKey) ||
                          (Input.GetKey(slideKey) && Time.time - slideEndTime < 0.15f);
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
    public CharacterController Controller => cc;

    public void Launch(Vector3 v)
    {
        launchVelocity = new Vector3(v.x, 0f, v.z);
        if (v.y > 0f) velocity.y = v.y;
    }

    public void StopMomentum() => launchVelocity = Vector3.zero;

    void Play(AudioClip clip, float vol = 1f) { if (clip) audioSrc.PlayOneShot(clip, vol); }
}
