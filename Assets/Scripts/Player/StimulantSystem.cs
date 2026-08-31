using System.Collections;
using UnityEngine;
using Bloodrush.Shared;
using Bloodrush.FX;

namespace Bloodrush.Player
{
public class StimulantSystem : MonoBehaviour
{
    public static StimulantSystem Instance { get; private set; }

    [Header("Uyarıcı")]
    [SerializeField] float healthRestore  = 30f;
    [SerializeField] float speedBoost     = 1.5f;
    [SerializeField] float speedDuration  = 6f;
    [SerializeField] float damageBoost    = 1.6f;
    [SerializeField] float damageDuration = 6f;
    [SerializeField] float collapsePerUse = 0.2f;  // 5 kullanım = ölüm

    float collapseLevel = 1f;
    public float CollapseLevel => collapseLevel;

    Health         health;
    PlayerMovement movement;
    PlayerShoot    shoot;

    Coroutine speedRoutine;
    Coroutine damageRoutine;

    void Awake() => Instance = this;

    void Start()
    {
        health   = GetComponent<Health>();
        movement = GetComponent<PlayerMovement>();
        shoot    = GetComponent<PlayerShoot>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyBindings.Stimulant))
            UseStimulant();
    }

    void UseStimulant()
    {
        if (collapseLevel <= 0f) return;

        health?.Heal(healthRestore);

        collapseLevel = Mathf.Max(0f, collapseLevel - collapsePerUse);
        DamageVignette.SetCollapseLevel(collapseLevel);

        if (speedRoutine  != null) StopCoroutine(speedRoutine);
        if (damageRoutine != null) StopCoroutine(damageRoutine);
        speedRoutine  = StartCoroutine(SpeedBuff());
        damageRoutine = StartCoroutine(DamageBuff());

        if (collapseLevel <= 0f)
            health?.TakeDamage(9999f);
    }

    IEnumerator SpeedBuff()
    {
        if (movement) movement.SpeedMultiplier = speedBoost;
        yield return new WaitForSeconds(speedDuration);
        if (movement) movement.SpeedMultiplier = 1f;
        speedRoutine = null;
    }

    IEnumerator DamageBuff()
    {
        if (shoot) shoot.DamageMultiplier = damageBoost;
        yield return new WaitForSeconds(damageDuration);
        if (shoot) shoot.DamageMultiplier = 1f;
        damageRoutine = null;
    }
}
}
