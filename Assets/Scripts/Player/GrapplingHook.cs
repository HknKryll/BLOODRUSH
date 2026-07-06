using UnityEngine;

public class GrapplingHook : MonoBehaviour
{
    [Header("Kanca")]
    [SerializeField] KeyCode hookKey = KeyCode.E;
    [SerializeField] float maxRange = 30f;
    [SerializeField] float pullSpeed = 28f;
    [SerializeField] float arrivalDistance = 1.5f;
    [SerializeField] float pickupArrivalDistance = 0.6f;
    [SerializeField] float launchMultiplier = 0.5f;
    [SerializeField] LayerMask hookMask = ~0;

    [Header("Görsel")]
    [SerializeField] LineRenderer rope;
    [SerializeField] Transform hookOrigin;   // namlu/kamera ucu

    [Header("Referanslar")]
    [SerializeField] Camera playerCamera;

    [Header("Ses")]
    [SerializeField] AudioClip hookFireClip;
    [SerializeField] [Range(0f,1f)] float hookFireVolume = 0.9f;
    [SerializeField] AudioClip hookReleaseClip;
    [SerializeField] [Range(0f,1f)] float hookReleaseVolume = 0.7f;

    AudioSource audioSrc;
    PlayerMovement movement;
    Vector3 hookPoint;
    bool isHooked;
    bool hasLeftGround;
    bool releasedManually;  // true sadece oyuncu tuşu kasıtlı bıraktıysa
    EnemyAI hookedEnemy;
    bool pullingEnemy;
    ThrowableAnchor hookedAnchor;
    AmmoPickup hookedPickup;
    bool pullingPickup;
    PlayerShoot shoot;

    void Start()
    {
        movement = GetComponent<PlayerMovement>();
        shoot    = GetComponent<PlayerShoot>();
        if (playerCamera == null) playerCamera = Camera.main;
        if (rope != null) rope.gameObject.SetActive(false);
        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake  = false;
        audioSrc.spatialBlend = 0f;
    }

    void Update()
    {
        if (Input.GetKeyDown(hookKey)) TryGrapple();
        if (Input.GetKeyUp(hookKey))   { releasedManually = true; ReleaseGrapple(); }

        if (isHooked) Pull();
    }

    void TryGrapple()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRange, hookMask)) return;

        if (hookFireClip) audioSrc.PlayOneShot(hookFireClip, hookFireVolume);
        hookPoint         = hit.point;
        isHooked          = true;
        hasLeftGround     = false;
        releasedManually  = false;
        movement.StopMomentum();   // kanca başlarken önceki slide/launch momentumu temizle
        hookedEnemy  = hit.collider.GetComponent<EnemyAI>();
        hookedAnchor = hit.collider.GetComponent<ThrowableAnchor>();
        hookedPickup = hit.collider.GetComponent<AmmoPickup>();

        if (hookedPickup != null)
        {
            pullingPickup = true;
            movement.DisableGravity = false;
        }
        else if (hookedEnemy != null && !hookedEnemy.IsLarge)
        {
            pullingEnemy = true;
            movement.DisableGravity = false;
            hookedEnemy.StartBeingPulled();
        }
        else
        {
            pullingEnemy = false;
            movement.DisableGravity = true;
        }

        if (rope != null)
        {
            rope.gameObject.SetActive(true);
            Vector3 origin = hookOrigin != null ? hookOrigin.position : playerCamera.transform.position;
            rope.SetPosition(0, origin);
            rope.SetPosition(1, hookPoint);
        }
    }

    bool ActuallyGrounded()
    {
        Vector3 origin    = transform.position + Vector3.up * 0.1f;
        float   checkDist = movement.Controller.height * 0.5f + 0.45f;
        return Physics.Raycast(origin, Vector3.down, checkDist,
                               ~0, QueryTriggerInteraction.Ignore);
    }

    void Pull()
    {
        if (pullingPickup)
        {
            if (hookedPickup == null) { ReleaseGrapple(); return; }
            Vector3 toPlayer = transform.position - hookedPickup.transform.position;
            float dist = toPlayer.magnitude;
            if (dist <= pickupArrivalDistance) { hookedPickup.Collect(shoot); ReleaseGrapple(); return; }
            hookedPickup.transform.position += toPlayer.normalized * pullSpeed * Time.deltaTime;
            hookPoint = hookedPickup.transform.position;
        }
        else if (pullingEnemy && hookedEnemy != null)
        {
            Vector3 toPlayer = transform.position - hookedEnemy.transform.position;
            float dist = toPlayer.magnitude;
            if (dist <= arrivalDistance) { ReleaseGrapple(); return; }
            hookedEnemy.transform.position += toPlayer.normalized * pullSpeed * Time.deltaTime;
            hookPoint = hookedEnemy.transform.position;
        }
        else
        {
            // Yerden ayrıldıktan sonra yere değince bırak
            if (!ActuallyGrounded()) hasLeftGround = true;
            if (hasLeftGround && ActuallyGrounded()) { ReleaseGrapple(); return; }

            if (hookedEnemy != null)  hookPoint = hookedEnemy.transform.position;
            if (hookedAnchor != null)
                hookPoint = hookedAnchor.transform.position;
            else if (hookedAnchor is object)  // Unity fake-null: yok edildi
            {
                ReleaseGrapple(); return;
            }

            float dist = Vector3.Distance(transform.position, hookPoint);
            if (dist <= arrivalDistance) { ReleaseGrapple(); return; }

            Vector3 dir = (hookPoint - transform.position).normalized;
            movement.Controller.Move(dir * pullSpeed * Time.deltaTime);
        }

        if (rope != null)
        {
            Vector3 origin = hookOrigin != null ? hookOrigin.position : playerCamera.transform.position;
            rope.SetPosition(0, origin);
            rope.SetPosition(1, hookPoint);
        }
    }

    void ReleaseGrapple()
    {
        if (!isHooked) return;
        if (hookReleaseClip) audioSrc.PlayOneShot(hookReleaseClip, hookReleaseVolume);
        isHooked = false;
        movement.DisableGravity = false;

        if (pullingEnemy)
        {
            if (hookedEnemy != null)
            {
                Vector3 toPlayer  = transform.position - hookedEnemy.transform.position;
                toPlayer.y        = 0f;
                Vector3 momentum  = toPlayer.sqrMagnitude > 0.01f
                    ? toPlayer.normalized * Mathf.Min(pullSpeed, 14f)
                    : Vector3.zero;
                hookedEnemy.StopBeingPulled(momentum);
            }
        }
        else
        {
            if (releasedManually && !ActuallyGrounded())
            {
                Vector3 dir = (hookPoint - transform.position).normalized;
                movement.Launch(dir * pullSpeed * launchMultiplier);
            }
            else
            {
                movement.StopMomentum();  // duvara çarpma/yere inme: momentum sıfırla
            }
        }

        releasedManually = false;
        hookedEnemy   = null;
        hookedAnchor  = null;
        hookedPickup  = null;
        pullingEnemy  = false;
        pullingPickup = false;

        if (rope != null) rope.gameObject.SetActive(false);
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!isHooked) return;
        if (hit.normal.y > 0.6f) return;  // zemin çarpması, yoksay
        ReleaseGrapple();
    }

    public bool IsHooked => isHooked;
}
