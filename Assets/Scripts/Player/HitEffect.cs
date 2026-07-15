using System.Collections;
using UnityEngine;

namespace Bloodrush.Player
{
public class HitEffect : MonoBehaviour
{
    void Start()
    {
        int count = Random.Range(5, 9);
        for (int i = 0; i < count; i++)
            StartCoroutine(Spark());
        Destroy(gameObject, 0.25f);
    }

    IEnumerator Spark()
    {
        var go = new GameObject();
        go.transform.SetParent(transform, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace     = true;
        lr.positionCount     = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows    = false;
        lr.numCapVertices    = 0;
        lr.numCornerVertices = 0;

        float baseW = Random.Range(0.018f, 0.035f);
        lr.startWidth = baseW;
        lr.endWidth   = 0f;

        Color col = Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.35f, 0.05f), Random.value);
        lr.material = MakeTransparentMat(col);
        lr.startColor = col;
        lr.endColor   = new Color(col.r, col.g * 0.2f, 0f, 0f);

        Vector3 origin = transform.position;
        // transform.forward = hit.normal, kıvılcımlar oradan dışarı fışkırır
        Vector3 dir = (transform.forward * Random.Range(0.3f, 1f)
                     + Random.insideUnitSphere * 0.8f).normalized;

        float speed    = Random.Range(4f, 9f);
        float lifetime = Random.Range(0.07f, 0.18f);
        float elapsed  = 0f;
        Vector3 tip    = origin;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;

            dir.y -= 9.8f * Time.deltaTime * 0.4f;
            tip   += dir * speed * Time.deltaTime;
            speed  = Mathf.Max(0f, speed * (1f - Time.deltaTime * 7f));

            lr.startWidth = Mathf.Lerp(baseW, 0f, t);
            lr.startColor = new Color(col.r, col.g, col.b, 1f - t);
            lr.endColor   = new Color(col.r, col.g * 0.2f, 0f, 0f);

            var mpb = new MaterialPropertyBlock();
            var c = new Color(col.r, col.g, col.b, 1f - t);
            mpb.SetColor("_BaseColor",  c);
            mpb.SetColor("_UnlitColor", c);
            lr.GetComponent<Renderer>().SetPropertyBlock(mpb);

            lr.SetPosition(0, origin);
            lr.SetPosition(1, tip);
            yield return null;
        }

        Destroy(go);
    }

    static Material MakeTransparentMat(Color col)
    {
        var sh  = Shader.Find("HDRP/Unlit") ?? Shader.Find("Standard");
        var mat = new Material(sh);
        mat.SetFloat("_SurfaceType", 1f);
        mat.SetFloat("_BlendMode",   0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetColor("_BaseColor",  col);
        mat.SetColor("_UnlitColor", col);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return mat;
    }
}
}
