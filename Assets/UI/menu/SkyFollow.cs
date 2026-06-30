using UnityEngine;
public class SkyFollow : MonoBehaviour
{
    Transform cam;
    void Start() { cam = Camera.main.transform; }
    void LateUpdate() { transform.position = cam.position; }
}
