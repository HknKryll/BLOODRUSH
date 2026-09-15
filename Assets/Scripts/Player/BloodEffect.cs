using UnityEngine;
using Bloodrush.FX;
using Bloodrush.Shared.Pooling;

namespace Bloodrush.Player
{
public class BloodEffect : MonoBehaviour, IPoolable
{
    const float lifetime = 2.5f;

    // Havuzdan her alınışta sıfırdan kurulur (bkz. IPoolable) — eskiden Start()'taydı,
    // Start() havuzlanan bir nesnede sadece İLK üreyiminde bir kez çalışır.
    // ParticleSystem ilk seferde eklenir, sonraki her OnSpawned()'da AYNI bileşen
    // yeniden yapılandırılır (bir GameObject'e ikinci bir ParticleSystem eklenemez).
    public void OnSpawned()
    {
        var ps = GetComponent<ParticleSystem>();
        if (ps == null) ps = gameObject.AddComponent<ParticleSystem>();
        // AddComponent playOnAwake ile hemen oynamaya başlar — süre/emisyon gibi
        // ayarları değiştirmeden önce durdurup, konfigürasyon bitince elle başlatıyoruz.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop             = false;
        main.playOnAwake      = false;
        main.duration          = 0.5f;
        main.startLifetime     = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed        = new ParticleSystem.MinMaxCurve(1.5f, 5f);
        main.startSize         = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startColor        = new ParticleSystem.MinMaxGradient(
            new Color(0.5f, 0f, 0f), new Color(0.75f, 0.05f, 0.05f));
        main.gravityModifier   = 1.2f;
        main.simulationSpace   = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Random.Range(10, 18)) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle     = 35f;
        shape.radius    = 0.05f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material   = SoftDotVFX.CreateMaterial(new Color(0.6f, 0f, 0f));

        ps.Play();
        Invoke(nameof(ReleaseSelf), lifetime);
    }

    public void OnDespawned() => CancelInvoke();

    void ReleaseSelf() => PoolManager.Release(gameObject);
}
}
