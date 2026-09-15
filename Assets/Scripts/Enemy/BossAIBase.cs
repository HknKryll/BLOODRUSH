using UnityEngine;
using Bloodrush.UI;

namespace Bloodrush.Enemy
{
// BossAI ve ExperimentBossAI'nin ortak faz/can-bari iskeleti. Ikisi de ayni
// 3-fazli (%66/%33 esikli) can-yuzdesi modelini ve BossHealthUI entegrasyonunu
// birebir ayni sekilde kullaniyordu — burada birlestirildi.
public abstract class BossAIBase : EnemyAIBase
{
    [Header("Can Barı")]
    [SerializeField] protected string bossName     = "BOSS";
    [SerializeField] protected float  barShowRange = 35f;

    protected int  phase = 1;   // 1: >66%, 2: 66-33%, 3: <33%
    protected bool barShown;

    protected float HealthFraction => health.Max > 0f ? health.Current / health.Max : 1f;

    protected void CheckPhaseTransition()
    {
        float frac = HealthFraction;
        if (phase == 1 && frac <= 0.66f) { phase = 2; OnPhaseAdvanced(); }
        else if (phase == 2 && frac <= 0.33f) { phase = 3; OnPhaseAdvanced(); }
    }

    // Faz atlayinca cagrilir — alt sinif kendi gecis coroutine'ini (karanlik/
    // patlama) baslatir.
    protected abstract void OnPhaseAdvanced();

    protected void UpdateHealthBar(float dist)
    {
        if (!barShown && dist <= barShowRange)
        {
            BossHealthUI.ShowBoss(health, bossName);
            barShown = true;
        }
    }

    protected override void OnDeath()
    {
        StopAllCoroutines();
        OnBossDeathEffects();
        if (agent.enabled) agent.enabled = false;
        SetRenderersVisible(false);
        enabled = false;
        BossHealthUI.HideBoss();
        OnBossDeathFinish();
    }

    // Olum anindaki ozel efekt/temizlik (blackout kapatma, gostergeleri gizleme, vb.)
    protected abstract void OnBossDeathEffects();

    // Olum sonrasi son adim (silah dusurme+Destroy / onDefeated event'i, vb.)
    protected abstract void OnBossDeathFinish();

    public override bool IsLarge => true;
}
}
