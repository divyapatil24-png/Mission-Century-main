using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// StumpsReactor — when the batsman is bowled, the stump sprite is split into
// three vertical pieces that fly apart to separate places (with spin), then
// reassemble for the next delivery. The pieces are created at runtime by
// slicing the single StumpsSet sprite into three columns, so no extra art is
// needed. The sprite uses a bottom-centre pivot.
public class StumpsReactor : MonoBehaviour
{
    [Tooltip("Seconds for the stumps to fly apart")]
    public float fallTime = 0.22f;
    [Tooltip("Seconds the stumps stay scattered")]
    public float holdTime = 0.8f;
    [Tooltip("Seconds for the stumps to reassemble")]
    public float riseTime = 0.3f;

    SpriteRenderer parentSR;
    readonly List<Transform>  pieces  = new List<Transform>();
    readonly List<Vector3>    homePos = new List<Vector3>();
    Vector3[] scatterPos;   // local target positions when shattered
    float[]   scatterRot;   // local target z-rotation when shattered
    Coroutine routine;
    bool built;

    void Awake() { Build(); }

    void Build()
    {
        if (built) return;
        parentSR = GetComponent<SpriteRenderer>();
        if (parentSR == null || parentSR.sprite == null) return;

        var sp  = parentSR.sprite;
        var tex = sp.texture;
        float P = sp.pixelsPerUnit;
        Vector2 piv = sp.pivot;   // pivot in pixels within the sprite rect
        Rect r = sp.rect;         // sprite rect in the texture (pixels)

        const int N = 3;
        float colW = r.width / N;
        for (int i = 0; i < N; i++)
        {
            float rx0 = r.x + i * colW;
            var sub = Sprite.Create(tex, new Rect(rx0, r.y, colW, r.height), new Vector2(0f, 0f), P);
            var go  = new GameObject("StumpPiece" + i);
            go.transform.SetParent(transform, false);
            var csr = go.AddComponent<SpriteRenderer>();
            csr.sprite         = sub;
            csr.sortingOrder   = parentSR.sortingOrder;
            csr.sortingLayerID = parentSR.sortingLayerID;
            // bottom-left of this column relative to the parent's pivot (unscaled local units)
            go.transform.localPosition = new Vector3((i * colW - piv.x) / P, (0f - piv.y) / P, 0f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = Vector3.one;
            pieces.Add(go.transform);
            homePos.Add(go.transform.localPosition);
        }
        parentSR.enabled = false;   // show the three pieces instead of the whole sprite

        // Scatter mostly in -x (the ball's direction) and outward, with spin.
        scatterPos = new Vector3[] {
            homePos[0] + new Vector3(-3.0f, 0.6f, 0f),
            homePos[1] + new Vector3(-0.9f, 1.3f, 0f),
            homePos[2] + new Vector3( 2.6f, 0.4f, 0f),
        };
        scatterRot = new float[] { 115f, 50f, -125f };
        built = true;
    }

    // Called when the ball beats the bat and hits the stumps.
    public void Topple()
    {
        if (!gameObject.activeInHierarchy) return;
        if (!built) Build();
        if (pieces.Count == 0) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShatterRoutine());
    }

    public void ResetStumps()
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
        for (int i = 0; i < pieces.Count; i++)
        {
            pieces[i].localPosition = homePos[i];
            pieces[i].localRotation = Quaternion.identity;
        }
    }

    IEnumerator ShatterRoutine()
    {
        var startPos = new Vector3[pieces.Count];
        for (int i = 0; i < pieces.Count; i++) startPos[i] = pieces[i].localPosition;

        // fly apart
        float t = 0f;
        while (t < fallTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fallTime);
            for (int i = 0; i < pieces.Count; i++)
            {
                pieces[i].localPosition = Vector3.Lerp(startPos[i], scatterPos[i], k);
                pieces[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, scatterRot[i], k));
            }
            yield return null;
        }

        yield return new WaitForSeconds(holdTime);

        // reassemble
        t = 0f;
        while (t < riseTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / riseTime);
            for (int i = 0; i < pieces.Count; i++)
            {
                pieces[i].localPosition = Vector3.Lerp(scatterPos[i], homePos[i], k);
                pieces[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(scatterRot[i], 0f, k));
            }
            yield return null;
        }
        ResetStumps();
        routine = null;
    }
}
