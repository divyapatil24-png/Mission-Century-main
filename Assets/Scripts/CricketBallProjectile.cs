using UnityEngine;

public class CricketBallProjectile : MonoBehaviour
{
    [HideInInspector] public bool wasHit = false;

    Rigidbody2D    rb;
    TrailRenderer  trail;
    float          camHalfW, camHalfH;

    Transform      stumps;
    float          stumpX   = -7.67f;   // fallback; overwritten if a "Stumps" object exists
    bool           resolved = false;    // true once this ball has scored OR caused an out

    // ── Pitch bounce ──────────────────────────────────────────────────────
    // The bounce is scripted (not left to physics) so it is reliable on the
    // thin ground collider: the vertical velocity is reflected at the pitch
    // surface while horizontal speed is preserved, so the ball always pitches
    // and then carries on to the batsman / stumps.
    float          bounceY   = -1.94f;  // transform.y where the ball's underside meets the pitch
    int            bounceCount = 0;
    const int      MAX_BOUNCES = 3;
    const float    RESTITUTION = 0.5f;  // how much vertical speed survives each bounce

    void Awake()
    {
        rb       = GetComponent<Rigidbody2D>();
        trail    = GetComponent<TrailRenderer>();
        camHalfW = Camera.main.orthographicSize * Camera.main.aspect;
        camHalfH = Camera.main.orthographicSize;

        // Locate the batsman's stumps so we know where "bowled" happens.
        var s = GameObject.Find("Stumps");
        if (s != null) { stumps = s.transform; stumpX = s.transform.position.x; }

        // Work out the y at which the ball's underside touches the pitch top,
        // from the Ground collider and this ball's circle collider.
        float groundTop = -1.9f;
        var g = GameObject.Find("Ground");
        if (g != null) { var gc = g.GetComponent<Collider2D>(); if (gc != null) groundTop = gc.bounds.max.y; }
        var cc = GetComponent<CircleCollider2D>();
        if (cc != null)
            bounceY = groundTop - (cc.offset.y - cc.radius) * transform.localScale.y;
    }

    void Update()
    {
        // Spin
        transform.Rotate(0, 0, -360f * Time.deltaTime);

        // Clamp inside camera — never leave the top of the screen
        var p = transform.position;
        if (p.y > camHalfH - 0.3f)
        {
            p.y = camHalfH - 0.3f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -Mathf.Abs(rb.linearVelocity.y));
            transform.position = p;
        }

        if (wasHit) return;

        // ── Pitch the ball: reflect vertical velocity at the pitch surface ──
        // Horizontal speed is preserved so the delivery always carries through
        // to the batsman after pitching. After a few bounces it just rolls.
        if (transform.position.y <= bounceY && rb.linearVelocity.y < 0f)
        {
            var bp = transform.position; bp.y = bounceY; transform.position = bp; rb.position = bp;
            float vy = (bounceCount < MAX_BOUNCES) ? Mathf.Abs(rb.linearVelocity.y) * RESTITUTION : 0f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, vy);
            bounceCount++;
        }

        // Un-hit ball reaches the stump line (behind the batsman) → OUT.
        // Positional check is immune to fast-ball tunnelling through a thin collider.
        if (transform.position.x <= stumpX)
        {
            RegisterOut();
            return;
        }

        // Safety net: ball left the field on the left without being hit = still a miss.
        if (transform.position.x < -camHalfW - 1f)
            RegisterOut();
    }

    public void Launch(Vector2 velocity, float gravity = 0.4f)
    {
        wasHit      = false;
        resolved    = false;
        bounceCount = 0;
        rb.gravityScale   = gravity;
        rb.linearVelocity = velocity;
        if (trail) { trail.Clear(); trail.enabled = true; }
    }

    // Called by PlayerController when the bat connects. `run` is the scored
    // value (1/2/3/4/6) — the bigger the shot, the higher and farther the ball
    // flies. A six is lofted; a four is driven flat and hard; singles/twos/threes
    // are gentle pushes into the gaps.
    public void OnHit(int run)
    {
        if (resolved) return;
        wasHit   = true;
        resolved = true;

        bool isSix  = run >= 6;
        bool isFour = run == 4;

        // Ball flies away after the hit, trajectory scaled by the shot value.
        float upVel   = isSix ? 9f : isFour ? 2.5f : 1f + run * 0.4f;
        float sideVel = isSix ? 7f : isFour ? 11f  : 4f + run * 0.8f;
        rb.gravityScale   = isSix ? 0.6f : isFour ? 0.5f : 0.4f;
        rb.linearVelocity = new Vector2(sideVel, upVel);
        if (trail)
        {
            trail.startColor = isSix  ? new Color(1f, 0.84f, 0.1f, 0.9f)   // gold for a six
                             : isFour ? new Color(1f, 0.45f, 0.1f, 0.9f)   // orange for a four
                                      : new Color(0.2f, 0.8f, 1f, 0.9f);   // blue for running runs
            trail.endColor   = Color.clear;
        }
        Destroy(gameObject, 2f);
    }

    // Ball beat the bat and reached the stumps → OUT (lose one life)
    void RegisterOut()
    {
        if (resolved) return;
        wasHit   = true;
        resolved = true;
        // GameManager handles the life loss, the MISS popup, the stump flash and the bowler appeal.
        GameManager.Instance?.OnBallMissed();
        Destroy(gameObject, 0.3f);
    }

    void OnBecameInvisible()
    {
        // Hit balls fly off-screen; clean them up shortly after they vanish.
        Destroy(gameObject, 0.1f);
    }
}
