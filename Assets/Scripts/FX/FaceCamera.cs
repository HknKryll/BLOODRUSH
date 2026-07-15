using UnityEngine;

namespace Bloodrush.FX
{
public class FaceCamera : MonoBehaviour
{
    Camera cam;

    void Start() => cam = Camera.main;

    void LateUpdate()
    {
        if (cam) transform.forward = cam.transform.forward;
    }
}
}
