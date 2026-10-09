using UnityEngine;

public class Human : MonoBehaviour
{
    [Header("Human Stats")]
    public float health = 10f;
    public float maxHealth = 10f;
    public float attack = 2f;
    public float defense = 0f;
    public float speed = 1.5f;
    [Tooltip("Multiplicador de velocidad al correr cuando ve un monstruo")]
    public float runSpeedMultiplier = 1.6f;
    public float visionRange = 6f;

    [Header("Combat")]
    public float attackRange = 0.6f;
    public float attackCooldown = 1f;
    [Tooltip("Si la vida baja de este porcentaje, huye en vez de pelear")]
    [Range(0f, 1f)] public float fleeHealthThreshold = 0.4f;

    [Header("Human States")]
    public bool isAlive = true;
    public HumanState currentState = HumanState.Exploring;

    private Vector3 destination;
    private float attackTimer = 0f;
    private bool monsterInSight = false;

    public float CurrentSpeed
    {
        get { return monsterInSight ? speed * runSpeedMultiplier : speed; }
    }

    private void Start()
    {
        destination = transform.position;

        SimulationManager manager = FindAnyObjectByType<SimulationManager>();
        if (manager != null)
        {
            manager.RegisterHuman(this);
        }
    }

    public void Simulate(float h)
    {
        if (!isAlive) return;

        attackTimer -= h;

        EvaluateState();

        switch (currentState)
        {
            case HumanState.Exploring:      Explore();      break;
            case HumanState.SearchingChest: SearchChest();  break;
            case HumanState.OpeningChest:   OpenChest();    break;
            case HumanState.Fighting:       Fight();        break;
            case HumanState.Fleeing:        Flee();         break;
        }

        // Movimiento continuo
        transform.position = Vector3.MoveTowards(transform.position, destination, CurrentSpeed * h);
    }

    void EvaluateState()
    {
        // 1. Monstruo cerca: corre más rápido y pelea o huye según la vida
        monsterInSight = GetNearestMonster() != null;
        if (monsterInSight)
        {
            bool healthy = (health / maxHealth) >= fleeHealthThreshold;
            currentState = healthy ? HumanState.Fighting : HumanState.Fleeing;
            return;
        }

        // 2. Pisando un cofre: abrirlo
        Collider2D chestHit = Physics2D.OverlapCircle(transform.position, 0.3f, LayerMask.GetMask("Chests"));
        if (chestHit != null && chestHit.GetComponent<Chest>() != null)
        {
            currentState = HumanState.OpeningChest;
            return;
        }

        // 3. Ve un cofre: ir por él
        if (FindNearestChest() != null)
        {
            currentState = HumanState.SearchingChest;
            return;
        }

        // 4. Explorar
        currentState = HumanState.Exploring;
    }

    void Explore()
    {
        if (Vector3.Distance(transform.position, destination) < 0.2f)
        {
            SelectNewDestination();
        }
    }

    void SearchChest()
    {
        Chest chest = FindNearestChest();
        if (chest == null)
        {
            currentState = HumanState.Exploring;
            return;
        }
        destination = chest.transform.position;
    }

    void OpenChest()
    {
        Collider2D chestHit = Physics2D.OverlapCircle(transform.position, 0.3f, LayerMask.GetMask("Chests"));
        if (chestHit != null)
        {
            Chest chest = chestHit.GetComponent<Chest>();
            if (chest != null)
            {
                chest.Open(this);
            }
        }
        currentState = HumanState.Exploring;
    }

    void Fight()
    {
        Monster enemy = GetNearestMonster();
        if (enemy == null)
        {
            currentState = HumanState.Exploring;
            return;
        }

        destination = enemy.transform.position;

        float dist = Vector2.Distance(transform.position, enemy.transform.position);
        if (dist <= attackRange && attackTimer <= 0f)
        {
            attackTimer = attackCooldown;
            enemy.TakeDamage(attack);
        }
    }

    void Flee()
    {
        Monster enemy = GetNearestMonster();
        if (enemy == null) return;

        Vector3 fleeDir = (transform.position - enemy.transform.position).normalized;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, fleeDir, visionRange, LayerMask.GetMask("Obstacles"));

        if (hit.collider != null)
        {
            float offset = transform.localScale.magnitude * 0.5f;
            destination = hit.point - (Vector2)fleeDir * offset;
        }
        else
        {
            destination = transform.position + fleeDir * visionRange;
        }
    }

    void SelectNewDestination()
    {
        Vector3 direction = new Vector3(
            Random.Range(-visionRange, visionRange),
            Random.Range(-visionRange, visionRange),
            0
        );

        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction.normalized, visionRange, LayerMask.GetMask("Obstacles"));

        if (hit.collider != null)
        {
            float offset = transform.localScale.magnitude * 0.5f;
            destination = hit.point - (Vector2)direction.normalized * offset;
        }
        else
        {
            destination = transform.position + direction;
        }
    }

    // ---------- Daño y mejoras ----------

    /// <summary>Devuelve true si el golpe lo mató.</summary>
    public bool TakeDamage(float amount)
    {
        if (!isAlive) return false;

        health -= Mathf.Max(0.1f, amount - defense);

        if (health <= 0f)
        {
            isAlive = false;
            Destroy(gameObject);
            return true;
        }
        return false;
    }

    public void ApplyUpgrade(ChestUpgrade type, float value)
    {
        switch (type)
        {
            case ChestUpgrade.Attack:  attack += value;                       break;
            case ChestUpgrade.Health:  maxHealth += value; health += value;   break;
            case ChestUpgrade.Defense: defense += value;                      break;
            case ChestUpgrade.Speed:   speed += value;                        break;
            case ChestUpgrade.Vision:  visionRange += value;                  break;
        }
    }

    // ---------- Búsquedas ----------

    Monster GetNearestMonster()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, visionRange, LayerMask.GetMask("Monsters"));
        Monster nearest = null;
        float minDist = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            Monster m = hit.GetComponent<Monster>();
            if (m != null && m.isAlive && !m.isHidden)
            {
                float dist = Vector2.Distance(transform.position, m.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = m;
                }
            }
        }
        return nearest;
    }

    Chest FindNearestChest()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, visionRange, LayerMask.GetMask("Chests"));
        Chest nearest = null;
        float minDist = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            Chest c = hit.GetComponent<Chest>();
            if (c != null)
            {
                float dist = Vector2.Distance(transform.position, c.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = c;
                }
            }
        }
        return nearest;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(destination, 0.2f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, destination);
    }
}