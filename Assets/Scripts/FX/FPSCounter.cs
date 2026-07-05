using UnityEngine;
using TMPro;

public class FPSCounter : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI label;

    float timer;
    int   frames;
    float fps;

    void Update()
    {
        frames++;
        timer += Time.unscaledDeltaTime;
        if (timer >= 0.5f)
        {
            fps    = frames / timer;
            frames = 0;
            timer  = 0f;
            label.text  = $"FPS: {fps:0}";
            label.color = fps >= 60f ? Color.green
                        : fps >= 30f ? Color.yellow
                        :              Color.red;
        }
    }
}
