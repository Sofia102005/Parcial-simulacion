using UnityEngine;

public class ChestSpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    public GameObject chestPrefab;
    [Tooltip("Prefab del monstruo que sale cuando el cofre es mímico")]
    public GameObject mimicPrefab;
    public float spawnInterval = 4f;
    public int maxChests = 15;

    [Header("Mimic")]
    [Range(0f, 1f)] public float mimicChance = 0.08f;

    [Header("Spawn cerca de las cuevas")]
    [Tooltip("Distancia extra más allá del radio de la cueva donde pueden aparecer cofres")]
    public float extraMargin = 2f;

    private float time = 0f;

    public void Simulate(float h)
    {
        time += h;

        if (time >= spawnInterval)
        {
            time = 0f;

            if (CountChests() < maxChests)
            {
                SpawnChest();
            }
        }
    }

    void SpawnChest()
    {
        Cave[] caves = FindObjectsByType<Cave>(FindObjectsSortMode.None);
        if (caves.Length == 0) return;

        int obstacleLayer = LayerMask.GetMask("Obstacles");

        // Reintenta para no aparecer dentro de una pared
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Cave cave = caves[Random.Range(0, caves.Length)];
            Vector2 offset = Random.insideUnitCircle * (cave.radius + extraMargin);
            Vector2 spawnPos = (Vector2)cave.transform.position + offset;

            if (Physics2D.OverlapCircle(spawnPos, 0.3f, obstacleLayer) != null) continue;

            GameObject go = Instantiate(chestPrefab, spawnPos, Quaternion.identity);
            Chest chest = go.GetComponent<Chest>();
            if (chest != null)
            {
                chest.isMimic = Random.value < mimicChance;
                chest.mimicPrefab = mimicPrefab;
            }
            return;
        }
    }

    int CountChests()
    {
        return FindObjectsByType<Chest>(FindObjectsSortMode.None).Length;
    }
}