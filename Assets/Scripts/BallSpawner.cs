// BallSpawner.cs — DEPRECATED
// Ball spawning is now handled directly inside BowlerController.
// This stub is kept to avoid missing-script errors on any old scene references.
using UnityEngine;

public class BallSpawner : MonoBehaviour
{
    [HideInInspector] public GameObject ballPrefab; // kept for serialisation compatibility

    /// <summary>No-op — BowlerController handles spawning directly.</summary>
    public void SpawnNow() { }
}
