using UnityEngine;

public enum ChestUpgrade
{
    Attack,
    Health,
    Defense,
    Speed,
    Vision
}

public class Chest : MonoBehaviour
{
    [Header("Chest Settings")]
    public bool isMimic = false;

    [Tooltip("Prefab del monstruo (con script Monster) que aparece si el cofre es un mímico")]
    public GameObject mimicPrefab;

    [Header("Upgrade")]
    public ChestUpgrade upgrade;
    public float upgradeValue;

    [Header("Comida: bonus garantizado")]
    public float attackBoost = 0.5f;
    public float speedBoost = 0.15f;

    [HideInInspector] public bool hasAmbusher = false; // un monstruo está escondido detrás

    private bool opened = false;

    private void Awake()
    {
        upgrade = (ChestUpgrade)Random.Range(0, 5);

        switch (upgrade)
        {
            case ChestUpgrade.Attack:  upgradeValue = Random.Range(0.5f, 1.5f); break;
            case ChestUpgrade.Health:  upgradeValue = Random.Range(2f, 5f);     break;
            case ChestUpgrade.Defense: upgradeValue = Random.Range(0.2f, 0.8f); break;
            case ChestUpgrade.Speed:   upgradeValue = Random.Range(0.1f, 0.4f); break;
            case ChestUpgrade.Vision:  upgradeValue = Random.Range(0.5f, 1.5f); break;
        }
    }

    /// <summary>
    /// Un humano se come el cofre: recibe mejora o es atacado por un mímico.
    /// </summary>
    public void Open(Human human)
    {
        if (opened || human == null) return;
        opened = true;

        if (isMimic && mimicPrefab != null)
        {
            GameObject go = Instantiate(mimicPrefab, transform.position, Quaternion.identity);
            Monster mimic = go.GetComponent<Monster>();
            if (mimic != null)
            {
                // Se marca antes de que corra su Start()
                mimic.isMimic = true;
                mimic.SurpriseAttack(human);
            }
        }
        else
        {
            // Siempre sube fuerza y velocidad
            human.ApplyUpgrade(ChestUpgrade.Attack, attackBoost);
            human.ApplyUpgrade(ChestUpgrade.Speed, speedBoost);

            // Más el bonus aleatorio que ya tenías (borra esta línea si no lo quieres)
            human.ApplyUpgrade(upgrade, upgradeValue);
        }

        Destroy(gameObject);
    }
}