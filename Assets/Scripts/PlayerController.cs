using UnityEngine;

// PlayerController — batsman: stands at the crease and swings the bat.
// Movement (run/jump) was removed — the batsman is stationary and the only
// action is timing the swing. The swing hits the ball if it overlaps the bat
// zone; a connect triggers a Four or Six. A ball that beats the bat and
// reaches the stumps is registered as OUT inside CricketBallProjectile.
[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────
    [Header("Batting")]
    [Tooltip("Radius of the overlap circle used to detect balls at swing time")]
    public float hitZone = 2.2f;
    [Tooltip("Horizontal offset of the bat zone in front of the batsman (positive = facing-right direction)")]
    public float batOffsetX = 0.9f;
    [Tooltip("Vertical offset of the bat zone centre (above pivot)")]
    public float batOffsetY = 0.6f;

    // ── Private ───────────────────────────────────────────────────────────
    Rigidbody2D    rb;
    Animator       anim;
    SpriteRenderer sr;

    bool  swinging   = false;
    float swingTimer = 0f;
    const float SWING_DURATION = 0.6f;   // seconds before another swing is allowed

    static readonly int HashSwing     = Animator.StringToHash("Swing");
    static readonly int HashMiss      = Animator.StringToHash("Miss");
    static readonly int HashCelebrate = Animator.StringToHash("Celebrate");
    static readonly int HashIsSix     = Animator.StringToHash("isSix");

    // ─────────────────────────────────────────────────────────────────────
    void Awake()
    {
        rb   = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr   = GetComponent<SpriteRenderer>();

        // Keep the batsman planted: freeze horizontal drift and rotation.
        rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
    }

    void Update()
    {
        bool playing = GameManager.Instance != null && GameManager.Instance.isPlaying;
        anim.speed = playing ? 1f : 0f;

        // The batsman never moves horizontally — kill any residual velocity so
        // physics nudges (e.g. a ball collision) can't push him off the crease.
        if (rb.linearVelocity.x != 0f)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (!playing) return;

        // ── Swing cooldown ────────────────────────────────────────────────
        if (swinging)
        {
            swingTimer += Time.deltaTime;
            if (swingTimer >= SWING_DURATION) { swinging = false; swingTimer = 0f; }
        }

        // ── Swing input (keyboard / touch) ────────────────────────────────
        if (Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(0)) TrySwing();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Collision / Trigger detection
    // ─────────────────────────────────────────────────────────────────────
    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Obstacle")) GameManager.Instance?.OnPlayerHit();
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Obstacle")) GameManager.Instance?.OnPlayerHit();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Public API — kept as no-ops so any existing UI/button wiring that still
    //  references movement does not throw. The batsman is intentionally fixed.
    // ─────────────────────────────────────────────────────────────────────
    public void SetMove(float d) { }
    public void StopMove()       { }
    public void TryJump()        { }

    public void TrySwing()
    {
        bool playing = GameManager.Instance != null && GameManager.Instance.isPlaying;
        if (swinging || !playing) return;
        swinging   = true;
        swingTimer = 0f;

        // ── Check whether a ball is inside the bat zone ───────────────────
        float   sideSign = sr.flipX ? -1f : 1f;
        Vector3 zonePos  = transform.position + new Vector3(sideSign * batOffsetX, batOffsetY, 0f);

        var hits      = Physics2D.OverlapCircleAll(zonePos, hitZone);
        bool connected = false;

        foreach (var h in hits)
        {
            if (!h.CompareTag("Ball")) continue;
            var ball = h.GetComponent<CricketBallProjectile>();
            if (ball == null || ball.wasHit) continue;

            // Any clean connection scores a random cricket value (1/2/3/4/6).
            int[] vals = { 1, 2, 3, 4, 6 };
            int   run   = vals[Random.Range(0, vals.Length)];
            bool  isSix = run >= 6;

            anim.SetBool(HashIsSix, isSix);
            anim.SetTrigger(HashSwing);
            ball.OnHit(run);
            GameManager.Instance?.AddRuns(run);
            connected = true;
            break;
        }

        if (!connected)
        {
            // Swing and miss — just play the miss animation. No life is lost here:
            // OUT is registered only when a ball actually reaches the stumps
            // (handled in CricketBallProjectile). This prevents a single delivery
            // from costing two lives, and stops swinging at thin air being punished.
            anim.SetBool(HashIsSix, false);
            anim.SetTrigger(HashMiss);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    public void PlayCelebrate() => anim.SetTrigger(HashCelebrate);
    public void PlayMiss()      { /* handled internally */ }

    public void ResetPlayer(Vector3 pos)
    {
        transform.position = pos;
        rb.linearVelocity  = Vector2.zero;
        swinging   = false;
        sr.flipX   = false;
        anim.SetBool(HashIsSix, false);
        anim.ResetTrigger(HashSwing);
        anim.ResetTrigger(HashMiss);
        anim.ResetTrigger(HashCelebrate);
        anim.Play("Idle");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        float sideSign = (sr != null && sr.flipX) ? -1f : 1f;
        Vector3 zonePos = transform.position + new Vector3(sideSign * batOffsetX, batOffsetY, 0f);
        Gizmos.DrawWireSphere(zonePos, hitZone);
    }
}
