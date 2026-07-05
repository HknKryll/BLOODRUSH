using UnityEngine;

public class DeathEffect : MonoBehaviour
{
    [SerializeField] GameObject effectPrefab;
    [SerializeField] float scale = 1f;

    void Awake()
    {
        GetComponent<Health>().onDeath.AddListener(Spawn);
    }

    void Spawn()
    {
        if (effectPrefab == null) return;
        var go = Instantiate(effectPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
        go.transform.localScale = Vector3.one * scale;
    }
}
