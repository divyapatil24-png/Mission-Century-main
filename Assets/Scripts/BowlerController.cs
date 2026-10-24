using UnityEngine;
using System.Collections;

// BowlerController — handles the full bowling sequence:
//   Runup (loop) → Bowl trigger → Deliver animation → ball release at ~60% of Deliver → return to Runup
[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public class BowlerController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────
    [Header("Ball")]
    public GameObject ballPrefab;
    [Tooltip("How far above/beside the bowler the ball appears (release point offset)")]
    public Vector2 releaseOffset = new Vector2(-0.8f, 1.2f);

    [Header("Bowling")]
    [Tooltip("Seconds between deliveries at the start of the game")]
    public float initialInterval = 3.0f;
    [Tooltip("Minimum interval even at max runs")]
    public float minInterval = 1.2f;
    [Tooltip("Fraction through the Deliver clip when the ball is released (0‑1)")]
    [Range(0f, 1f)] public float releaseNormalisedTime = 0.55f;

    [Header("Ball Physics")]
    public float initialBallSpeed  = 7f;
    public float maxBallSpeed      = 16f;

    [Header("Run-up")]
    [Tooltip("How far back (to the right) the bowler starts his run before each delivery")]
    public float runupDistance = 2.5f;
    [Tooltip("Seconds the bowler takes to run in from his mark to the crease")]
    public float runupDuration = 0.75f;
    [Tooltip("Seconds the bowler takes to jog back to his mark after delivering")]
    public float returnDuration = 0.6f;

    // ── Private ───────────────────────────────────────────────────────────
    Animator        anim;
    SpriteRenderer  sr;
    Collider2D      playerCol;          // cached batsman body collider (ball ignores it)
    Collider2D      groundCol;          // cached pitch collider (bounce is scripted, not physical)
    Collider2D      stumpsCol;          // cached stumps collider (OUT is positional, not physical)
    float           bowlTimer    = 0f;
    float           bowlInterval;
    bool            isBowling    = false;
    bool            ballReleased = false;

    Coroutine       bowlRoutine;

    Vector3         deliverPos;          // crease / release spot — kept fixed so the tuned ball physics are unchanged
    Vector3         markPos;             // start of the run-up, back behind the crease

    // Tracks the normalised time inside the "Deliver" state
    static readonly int HashBowl   = Animator.StringToHash("Bowl");
    static readonly int HashAppeal = Animator.StringToHash("Appeal");

    // ─────────────────────────────────────────────────────────────────────
    void Start()
    {
        anim = GetComponent<Animator>();
        sr   = GetComponent<SpriteRenderer>();
        bowlInterval = initialInterval;

        // The crease (current placement) is the tuned release spot; the bowler
        // waits at his mark, runupDistance further back, and runs in from there.
        deliverPos = transform.position;
        markPos    = deliverPos + new Vector3(runupDistance, 0f, 0f);
        transform.position = markPos;

        // Bowler starts in Runup
        anim.Play("Runup");
    }

    // ─────────────────────────────────────────────────────────────────────
    void Update()
    {
        bool playing = GameManager.Instance != null && GameManager.Instance.isPlaying;

        // Pause animation when game is not running
        anim.speed = playing ? 1f : 0f;
        if (!playing) return;

        // Speed up deliveries as the batsman scores more runs
        int runs     = GameManager.Instance.runs;
        bowlInterval = Mathf.Max(minInterval, initialInterval - runs * 0.018f);

        if (!isBowling)
        {
            bowlTimer += Time.deltaTime;
            if (bowlTimer >= bowlInterval)
            {
                bowlTimer = 0f;
                StartBowl();
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    void StartBowl()
    {
        if (bowlRoutine != null) StopCoroutine(bowlRoutine);
        bowlRoutine = StartCoroutine(BowlSequenceRoutine());
    }

    /// <summary>
    /// Robust timing sequence that respects game pausing and dynamic speed adjustment.
    /// </summary>
    IEnumerator BowlSequenceRoutine()
    {
        isBowling    = true;
        ballReleased = false;

        // ── Phase 1: run in from the mark to the crease ───────────────────
        anim.Play("Runup");
        yield return MoveOver(markPos, deliverPos, runupDuration);

        // ── Phase 2: deliver ──────────────────────────────────────────────
        anim.SetTrigger(HashBowl); // Transition: Runup → Deliver

        float deliverClipLength = GetDeliverClipLength();
        float elapsed = 0f;

        // Track Deliver animation playback safely while game is active
        while (elapsed < deliverClipLength)
        {
            if (GameManager.Instance != null && GameManager.Instance.isPlaying)
            {
                elapsed += Time.deltaTime;

                // Release ball at specified normal time threshold
                if (!ballReleased && (elapsed / deliverClipLength) >= releaseNormalisedTime)
                {
                    ballReleased = true;
                    ReleaseBall();
                }
            }
            yield return null;
        }

        // Safety fallback: Ensure a ball spawns if somehow missed by standard timeline
        if (!ballReleased)
        {
            ballReleased = true;
            ReleaseBall();
        }

        // ── Phase 3: jog back to the mark for the next delivery ───────────
        anim.Play("Runup");
        yield return MoveOver(deliverPos, markPos, returnDuration);

        isBowling    = false;
        ballReleased = false;
        bowlRoutine  = null;
    }

    // Lerps the bowler between two points over `duration`, only advancing while
    // the game is actually running so it respects pause/menu states.
    IEnumerator MoveOver(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f) { transform.position = to; yield break; }
        float t = 0f;
        while (t < duration)
        {
            if (GameManager.Instance != null && GameManager.Instance.isPlaying)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t / duration));
            }
            yield return null;
        }
        transform.position = to;
    }

    void ReleaseBall()
    {
        if (ballPrefab == null) return;

        // Spawn ball at the bowler's arm release point
        Vector3 spawnPos = transform.position + new Vector3(releaseOffset.x, releaseOffset.y, 0f);
        var go = Instantiate(ballPrefab, spawnPos, Quaternion.identity);
        go.SetActive(true);

        // The bat connects via an overlap-circle check in PlayerController, not a
        // physical collision, so let the ball pass THROUGH the batsman's body.
        // Otherwise a beaten ball ricochets off his torso and never reaches the
        // stumps, so a miss could never register as bowled/OUT.
        var ballCol = go.GetComponent<Collider2D>();
        if (ballCol != null)
        {
            if (playerCol == null)
            {
                var pl = GameObject.FindWithTag("Player");
                if (pl != null) playerCol = pl.GetComponent<Collider2D>();
            }
            if (playerCol != null) Physics2D.IgnoreCollision(ballCol, playerCol);

            // The pitch bounce is handled in CricketBallProjectile, so the ball
            // must not also collide physically with the Ground (that fights the
            // scripted bounce on the thin collider).
            if (groundCol == null)
            {
                var gnd = GameObject.Find("Ground");
                if (gnd != null) groundCol = gnd.GetComponent<Collider2D>();
            }
            if (groundCol != null) Physics2D.IgnoreCollision(ballCol, groundCol);

            // A beaten ball is registered OUT positionally when it reaches the
            // stump line, so it must pass THROUGH the stumps collider rather than
            // ricochet off it (which would stop it short and never trigger OUT).
            if (stumpsCol == null)
            {
                var st = GameObject.Find("Stumps");
                if (st != null) stumpsCol = st.GetComponent<Collider2D>();
            }
            if (stumpsCol != null) Physics2D.IgnoreCollision(ballCol, stumpsCol);
        }

        // Calculate speed; the ball is bowled on an arc so it dips down and
        // PITCHES (bounces once) on the wicket before reaching the batsman.
        float spd = Mathf.Min(initialBallSpeed + GameManager.Instance.runs * 0.04f, maxBallSpeed);

        // Upward toss controls the length: a higher toss makes the ball pitch
        // closer to the batsman (fuller), a lower toss pitches it shorter.
        // The range is tuned so the bounce always lands in front of the stumps.
        float upToss = Random.Range(0.8f, 1.8f);

        // Scale gravity with speed so a faster delivery still dips onto the pitch
        // at roughly the same length (otherwise a fast flat ball would reach the
        // batsman before it had time to fall and would never pitch).
        float grav = 0.45f * (spd / initialBallSpeed);
        go.GetComponent<CricketBallProjectile>()?.Launch(new Vector2(-spd, upToss), grav);
    }

    // ─────────────────────────────────────────────────────────────────────
    public void TriggerAppeal()
    {
        anim.SetTrigger(HashAppeal);
    }

    public void ResetBowler()
    {
        if (bowlRoutine != null)
        {
            StopCoroutine(bowlRoutine);
            bowlRoutine = null;
        }
        isBowling    = false;
        ballReleased = false;
        bowlTimer    = 0f;

        anim.ResetTrigger(HashBowl);
        anim.ResetTrigger(HashAppeal);
        if (markPos != Vector3.zero) transform.position = markPos;
        anim.Play("Runup");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Returns the Deliver clip length; falls back to 1.2 s. Matches "deliver"
    // first so the run-up clip (also containing "bowl") can't be picked instead.
    float GetDeliverClipLength()
    {
        var rc = anim.runtimeAnimatorController;
        if (rc != null)
        {
            foreach (var clip in rc.animationClips)
                if (clip.name.ToLower().Contains("deliver")) return clip.length;
            foreach (var clip in rc.animationClips)
                if (clip.name.ToLower().Contains("bowl")) return clip.length;
        }
        return 1.2f;
    }
}