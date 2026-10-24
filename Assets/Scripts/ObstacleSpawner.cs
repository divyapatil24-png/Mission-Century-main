using UnityEngine;

// ObstacleSpawner — handles spawning spikes, boxes, and stumps
// Spawns ground obstacles that slide from right to left
public class ObstacleSpawner : MonoBehaviour
{
    [Header("Obstacle Prefabs")]
    public GameObject spikePrefab;
    public GameObject boxPrefab;
    [Tooltip("The stump/wicket obstacle prefab to solve the undefined error")]
    public GameObject stumpPrefab;

    float spawnTimer    = 0f;
    float spawnInterval = 5f;
    float camHalfW;

    void Start()
    {
        camHalfW     = Camera.main.orthographicSize * Camera.main.aspect;
        spawnTimer   = 3f; // First obstacle spawns after 3 seconds
    }

    void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.isPlaying) return;

        spawnTimer += Time.deltaTime;
        int score   = GameManager.Instance.runs;
        spawnInterval = Mathf.Max(2.5f, 5f - score * 0.02f);

        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            SpawnObstacle();
        }
    }

    void SpawnObstacle()
    {
        // Cycle randomly between spikes, boxes, and stumps
        GameObject prefab = null;
        float choice = Random.value;

        if (choice < 0.33f && spikePrefab != null)
        {
            prefab = spikePrefab;
        }
        else if (choice < 0.66f && boxPrefab != null)
        {
            prefab = boxPrefab;
        }
        else if (stumpPrefab != null)
        {
            prefab = stumpPrefab;
        }
        else
        {
            // Fallback selection to prevent null errors
            prefab = spikePrefab ?? boxPrefab;
        }

        if (prefab == null) return;

        float spawnX = camHalfW + 1f;
        float groundY = -2.35f; // Ground surface coordinate
        var go = Instantiate(prefab, new Vector3(spawnX, groundY, 0f), Quaternion.identity);

        var mover = go.GetComponent<ObstacleMover>();
        if (mover == null) mover = go.AddComponent<ObstacleMover>();
        mover.speed = 4f + GameManager.Instance.runs * 0.03f;
    }
}