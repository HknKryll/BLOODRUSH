using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class SpeedEffect : MonoBehaviour
{
    [SerializeField] Camera     playerCamera;
    [SerializeField] float      baseFov        = 70f;
    [SerializeField] float      maxFovBoost    = 25f;
    [SerializeField] float      speedThreshold = 8f;
    [SerializeField] float      maxSpeed       = 35f;
    [SerializeField] float      lerpSpeed      = 6f;

    CharacterController  cc;
    ChromaticAberration  ca;

    void Awake()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player) cc = player.GetComponent<CharacterController>();
        if (playerCamera == null) playerCamera = Camera.main;

        var vol = gameObject.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 2;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.profile = profile;

        ca         = profile.Add<ChromaticAberration>();
        ca.active  = true;
        ca.intensity.Override(0f);
    }

    void Update()
    {
        if (cc == null || playerCamera == null) return;

        float speed = cc.velocity.magnitude;
        float t     = Mathf.Clamp01((speed - speedThreshold) / (maxSpeed - speedThreshold));

        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            baseFov + maxFovBoost * t,
            Time.deltaTime * lerpSpeed);

        ca.intensity.value = Mathf.Lerp(ca.intensity.value, t, Time.deltaTime * lerpSpeed);
    }
}
