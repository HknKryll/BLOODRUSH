using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class DamageVignette : MonoBehaviour
{
    [SerializeField] Health playerHealth;
    [SerializeField] float  flashIntensity = 0.55f;
    [SerializeField] float  fadeSpeed      = 4f;

    Vignette vignette;
    float    target;

    void Awake()
    {
        var vol     = gameObject.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 1;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.profile = profile;

        vignette = profile.Add<Vignette>();
        vignette.active = true;
        vignette.color.Override(Color.red);
        vignette.intensity.Override(0f);

        if (playerHealth == null)
            playerHealth = GameObject.FindGameObjectWithTag("Player")?.GetComponent<Health>();

        if (playerHealth)
            playerHealth.onHealthChanged.AddListener(OnHealthChanged);
    }

    void OnHealthChanged(float normalized)
    {
        target = flashIntensity;
    }

    void Update()
    {
        if (vignette == null) return;
        if (target > 0f)
        {
            vignette.intensity.value = target;
            target = 0f;
        }
        vignette.intensity.value = Mathf.Lerp(vignette.intensity.value, 0f, Time.deltaTime * fadeSpeed);
    }
}
