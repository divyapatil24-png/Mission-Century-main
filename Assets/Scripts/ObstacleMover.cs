// ObstacleMover.cs — moves obstacle left, destroys when off screen
using UnityEngine;

public class ObstacleMover : MonoBehaviour
{
    public float speed = 4f;
    float camHalfW;

    void Start() => camHalfW = Camera.main.orthographicSize * Camera.main.aspect;

    void Update()
    {
        transform.Translate(Vector3.left * speed * Time.deltaTime);
        if (transform.position.x < -camHalfW - 2f) Destroy(gameObject);
    }
}
