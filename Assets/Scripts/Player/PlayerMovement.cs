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
    [SerializeField] float sensitivity = 2f;
    [SerializeField] float maxPitch    = 85f;

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

    void Start()
    {
        cc           = GetComponent<CharacterController>();
        normalHeight = cc.height;
        normalCenter = cc.center;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        float saved = PlayerPrefs.GetFloat("Sensitivity", sensitivity);
        if (saved >= 0.5f) sensitivity = saved;
    }

    void Update()
    {
        Look();
        Move();
        UpdateSlide();
    }

    void Look()
    {
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

        bool jumpAllowed = coyoteTimer > 0f || Time.time < slideJumpDeadline;
        if (Input.GetButtonDown("Jump") && jumpAllowed)
        {
            velocity.y        = jumpForce;
            coyoteTimer       = 0f;
            slideJumpDeadline = 0f;
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
}
