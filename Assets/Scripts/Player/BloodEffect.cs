using UnityEngine;
using Bloodrush.FX;

namespace Bloodrush.Player
{
public class BloodEffect : MonoBehaviour
{
    void Start()
    {
        var ps = gameObject.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop             = false;
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

        Destroy(gameObject, 2.5f);
    }
}
}
