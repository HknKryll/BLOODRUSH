using UnityEngine;

namespace Bloodrush.Player
{
// Duvardan zıplama tespiti + wallrun state machine. PlayerMovement tarafından
// composition ile kullanılır; dikey hız (velocity.y) ve ses çalma kararları
// çağıran tarafta kalır, bu sınıf sadece duvar tespiti/hareketini yönetir.
public class PlayerWallRun
{
    readonly CharacterController cc;
    readonly Transform transform;

    readonly LayerMask wallJumpMask;
    readonly float wallJumpCheckDist;
    readonly float wallJumpRefillTime;
    readonly int   maxWallJumpsPerWall;
    readonly float wallJumpLockDuration;

    readonly LayerMask wallRunMask;
    readonly float wallRunCheckDist;
    readonly float wallRunSpeed;
    readonly float wallRunMaxTime;
    readonly float wallRunGravity;
    readonly float wallRunJumpOut;

    static readonly int[] wallSides = { 1, -1 };

    Collider lastWallCollider;
    float    lastWallJumpTime = -999f;
    Vector3  wallRunNormal;
    float    wallRunTimer;

    public bool    TouchingWall       { get; private set; }
    public Vector3 WallNormal         { get; private set; }
    public int     WallJumpsRemaining { get; private set; }
    public float   WallJumpLockUntil  { get; private set; }
    public bool    Running            { get; private set; }
    public int     Side               { get; private set; }
    public bool    CanWallJump => TouchingWall && WallJumpsRemaining > 0;

    public PlayerWallRun(CharacterController cc, Transform transform,
        LayerMask wallJumpMask, float wallJumpCheckDist, float wallJumpRefillTime,
        int maxWallJumpsPerWall, float wallJumpLockDuration,
        LayerMask wallRunMask, float wallRunCheckDist, float wallRunSpeed,
        float wallRunMaxTime, float wallRunGravity, float wallRunJumpOut)
    {
        this.cc = cc;
        this.transform = transform;
        this.wallJumpMask = wallJumpMask;
        this.wallJumpCheckDist = wallJumpCheckDist;
        this.wallJumpRefillTime = wallJumpRefillTime;
        this.maxWallJumpsPerWall = maxWallJumpsPerWall;
        this.wallJumpLockDuration = wallJumpLockDuration;
        this.wallRunMask = wallRunMask;
        this.wallRunCheckDist = wallRunCheckDist;
        this.wallRunSpeed = wallRunSpeed;
        this.wallRunMaxTime = wallRunMaxTime;
        this.wallRunGravity = wallRunGravity;
        this.wallRunJumpOut = wallRunJumpOut;

        WallJumpsRemaining = maxWallJumpsPerWall;
    }

    public void CheckWall()
    {
        TouchingWall = false;
        Vector3 origin = transform.position + cc.center;
        Vector3[] dirs = { transform.forward, -transform.forward, transform.right, -transform.right };

        foreach (var dir in dirs)
        {
            if (Physics.Raycast(origin, dir, out RaycastHit hit, wallJumpCheckDist + cc.radius,
                    wallJumpMask, QueryTriggerInteraction.Ignore))
            {
                TouchingWall = true;
                Vector3 normal = hit.normal;
                normal.y = 0f;
                normal.Normalize();
                WallNormal = normal;

                // Farklı duvar ya da son zıplamadan bu yana yeterince zaman geçtiyse hakkı yenile
                if (hit.collider != lastWallCollider || Time.time - lastWallJumpTime >= wallJumpRefillTime)
                {
                    WallJumpsRemaining = maxWallJumpsPerWall;
                    lastWallCollider   = hit.collider;
                }
                break;
            }
        }
    }

    // Çağıran taraf CanWallJump==true iken zıplama girdisini işlerken çağırır;
    // yatay itiş yönünü döndürür, dikey hız/ses çalma karar çağıranda kalır.
    public Vector3 ConsumeWallJump()
    {
        WallJumpsRemaining--;
        lastWallJumpTime  = Time.time;
        WallJumpLockUntil = Time.time + wallJumpLockDuration;
        return WallNormal;
    }

    // Havadayken yan tarafta işaretli (wallRunMask) bir yüzey varsa wall-run başlat
    public void TryStart(bool wantsForward)
    {
        if (wallRunMask == 0) return;   // yapılandırılmamış
        if (!wantsForward) return;      // ileri gitmiyorsan başlama

        Vector3 origin = transform.position + cc.center;
        foreach (int side in wallSides)
        {
            if (Physics.Raycast(origin, transform.right * side, out RaycastHit hit,
                    wallRunCheckDist + cc.radius, wallRunMask, QueryTriggerInteraction.Ignore))
            {
                Running       = true;
                wallRunNormal = hit.normal;
                Side          = side;
                wallRunTimer  = wallRunMaxTime;
                return;
            }
        }
    }

    // Her frame çağrılır; wall-run boyunca hareketi doğrudan uygular (cc.Move).
    // Duvardan zıplanırsa jumpedOff=true döner ve yatay itiş jumpOutHorizontal'da gelir
    // (dikey hız ve ses çalma karar çağıranda kalır).
    public void Update(bool jumpPressed, out bool jumpedOff, out Vector3 jumpOutHorizontal)
    {
        jumpedOff = false;
        jumpOutHorizontal = Vector3.zero;

        wallRunTimer -= Time.deltaTime;

        Vector3 origin = transform.position + cc.center;
        bool onWall = Physics.Raycast(origin, transform.right * Side, out RaycastHit hit,
                        wallRunCheckDist + cc.radius + 0.3f, wallRunMask, QueryTriggerInteraction.Ignore);
        if (onWall) wallRunNormal = hit.normal;

        // Bitiş: duvar bitti / süre doldu / yere değdi
        if (!onWall || wallRunTimer <= 0f || cc.isGrounded)
        {
            Running = false;
            return;
        }

        if (jumpPressed)
        {
            jumpedOff = true;
            jumpOutHorizontal = wallRunNormal * wallRunJumpOut;
            Running = false;
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
}
}
