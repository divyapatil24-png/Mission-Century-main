// ParallaxBackground.cs — multi-layer parallax scrolling for stadium depth
using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    [System.Serializable]
    public struct Layer
    {
        public Transform tr;
        public float     speed;   // 0=static, 1=full scroll speed
        public float     resetX;  // when to reset
        public float     startX;
    }

    public Layer[] layers;
    public float   baseSpeed = 3f;

    void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.isPlaying) return;
        float spd = baseSpeed;

        foreach (var l in layers)
        {
            if (l.tr == null) continue;
            l.tr.Translate(Vector3.left * spd * l.speed * Time.deltaTime);
            if (l.tr.position.x < l.resetX)
            {
                var p = l.tr.position;
                p.x = l.startX;
                l.tr.position = p;
            }
        }
    }
}
