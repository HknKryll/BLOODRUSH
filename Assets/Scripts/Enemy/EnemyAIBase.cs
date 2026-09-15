using UnityEngine;
using UnityEngine.AI;
using Bloodrush.Shared;
using Bloodrush.Shared.Audio;
using Bloodrush.Player;

namespace Bloodrush.Enemy
{
// EnemyAI/BossAI/ExperimentBossAI'nin ortak taban sinifi — agent/health/oyuncu
// referanslarinin kurulumu, FacePlayer, renderer onbellekleme ve olum bildirim
// zincirini tek yerde toplar (bkz. BLOODRUSH_YENIDEN_YAPILANDIRMA_PLANI.md, Faz 4).
//
// State machine'ler BILEREK burada BIRLESTIRILMEDI: uc sinifin State enum'lari
// ve Update() akislari birbirinden yeterince farkli (kucuk dusman patrol/chase/
// menzilli hibrit; bosslar faz-tabanli farkli saldiri setleri) — zorla tek bir
// FSM'e sikistirmak davranis riskini artirirdi. Ortak taban SADECE gercekten
// birebir ayni kurulum/yardimci kodu topluyor (bkz. mimari kurallar "Acik Kalan
// Kararlar" — enum+switch genel kural olarak kaldi, ayri state siniflarina
// bolunmedi).
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public abstract class EnemyAIBase : MonoBehaviour, IParryable
{
    protected NavMeshAgent   agent;
    protected Health         health;
    protected Transform      player;
    protected Health         playerHealth;
    protected PlayerMovement playerMovement;
    protected SfxPlayer      sfx;
    protected Renderer[]     renderers;

    protected bool dead;

    protected virtual void Awake()
    {
        agent  = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();

        var pgo = GameObject.FindGameObjectWithTag("Player");
        if (pgo != null)
        {
            player         = pgo.transform;
            playerHealth   = pgo.GetComponent<Health>();
            playerMovement = pgo.GetComponent<PlayerMovement>();
        }

        sfx = SfxPlayer.Create(gameObject, spatialBlend: 1f);

        // Olcek buyutulunce (buyuk dusman/boss) skinned mesh sinirlari bozulup
        // yanlis frustum-culling ile gorunmez olabiliyor — her zaman guncelle.
        renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            smr.updateWhenOffscreen = true;

        health.onDeath.AddListener(HandleDeath);
    }

    protected void FacePlayer()
    {
        Vector3 dir = player.position - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
    }

    protected void SetRenderersVisible(bool visible)
    {
        foreach (var r in renderers) if (r != null) r.enabled = visible;
    }

    void HandleDeath()
    {
        if (dead) return;
        dead = true;
        OnDeath();
    }

    protected abstract void OnDeath();

    public abstract bool IsParryable { get; }
    public abstract void Parry(float stunDuration);

    // Buyuk dusmanlar/bosslar kanca/yumruk tarafindan boyle isaretlenir (varsayilan: degil).
    public virtual bool IsLarge => false;
}
}
