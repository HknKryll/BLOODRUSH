using UnityEngine;

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
    [SerializeField] float recoilKick     = 0.07f;
    [SerializeField] float recoilYKick    = 0.05f;
    [SerializeField] float recoilTilt     = 6f;
    [SerializeField] float recoilReturn   = 10f;

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
    float      recoilZ;
    float      recoilPosY;
    float      recoilTiltCurrent;
    Vector3    swayPos;

    void Start()
    {
        movement   = GetComponentInParent<PlayerMovement>();
        hook       = GetComponentInParent<GrapplingHook>();
        cam        = GetComponentInParent<Camera>();
        if (cam == null) cam = Camera.main;
        if (weaponHolder == null) weaponHolder = transform;
        defaultPos = weaponHolder.localPosition;
        defaultRot = weaponHolder.localRotation;
        currentPos = defaultPos;
    }

    void Update()
    {
        Vector3 vel    = movement.Velocity;
        float   speed  = new Vector2(vel.x, vel.z).magnitude;
        bool    ground = movement.IsGrounded;
        bool    slide  = movement.IsSliding;
        bool    grapple = hook != null && hook.IsHooked;

        // ── Bob ──────────────────────────────
        Vector3 target = defaultPos;

        if (speed > 1f && ground && !slide)
        {
            bobTimer += Time.deltaTime * bobFrequency;
            target += new Vector3(
                Mathf.Sin(bobTimer)           * bobAmplitudeX,
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

        // ── Recoil dönüş ─────────────────────
        recoilZ           = Mathf.Lerp(recoilZ,           0f, Time.deltaTime * recoilReturn);
        recoilPosY        = Mathf.Lerp(recoilPosY,        0f, Time.deltaTime * recoilReturn);
        recoilTiltCurrent = Mathf.Lerp(recoilTiltCurrent, 0f, Time.deltaTime * recoilReturn);

        // ── Duvar Koruması ───────────────────
        float wallPush = 0f;
        if (cam != null && Physics.Raycast(weaponHolder.position, cam.transform.forward,
                out RaycastHit wallHit, wallCheckDist, wallMask, QueryTriggerInteraction.Ignore))
        {
            float t = 1f - (wallHit.distance / wallCheckDist);
            wallPush = wallRetractDist * t;
        }

        // ── Uygula ───────────────────────────
        currentPos = Vector3.Lerp(currentPos, target, Time.deltaTime * bobSmoothing);
        weaponHolder.localPosition = currentPos + Vector3.back * (recoilZ + wallPush) + Vector3.up * recoilPosY + swayPos;
        weaponHolder.localRotation = defaultRot * Quaternion.Euler(recoilTiltCurrent, 0f, slideRoll);
    }

    public void ApplyRecoil()
    {
        recoilZ           += recoilKick;
        recoilPosY        += recoilYKick;
        recoilTiltCurrent -= recoilTilt;
    }
}
