using UnityEngine;

namespace Bloodrush.Player
{
public class BloodEffect : MonoBehaviour
{
    void Start()
    {
        int count = Random.Range(10, 18);
        for (int i = 0; i < count; i++)
            SpawnDrop();
        Destroy(gameObject, 3f);
    }

    void SpawnDrop()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.transform.position = transform.position;

        float s = Random.Range(0.07f, 0.18f);
        go.transform.localScale = Vector3.one * s;

        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", new Color(Random.Range(0.4f, 0.7f), 0f, 0f));
        go.GetComponent<Renderer>().SetPropertyBlock(mpb);

        var rb = go.AddComponent<Rigidbody>();
        Vector3 vel = Random.onUnitSphere * Random.Range(1.5f, 6f);
        vel.y = Mathf.Abs(vel.y) + Random.Range(0f, 2f);
        rb.velocity = vel;

        Destroy(go, 2.5f);
    }
}
}
