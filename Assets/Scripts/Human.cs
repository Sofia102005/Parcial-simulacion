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
    [Tooltip("Distancia entre BORDES de los colliders para poder golpear")]
    public float attackRange = 0.6f;
    public float attackCooldown = 1f;
    [Tooltip("Si la vida baja de este porcentaje, huye en vez de pelear")]
    [Range(0f, 1f)] public float fleeHealthThreshold = 0.4f;
    [Tooltip("Probabilidad de entrar en pánico y huir al ver un monstruo (aunque tenga vida)")]
    [Range(0f, 1f)] public float fleeOnSightChance = 0.35f;
    [Tooltip("Probabilidad por golpe de matar al monstruo al instante (baja: el monstruo es más fuerte)")]
    [Range(0f, 1f)] public float instantKillChance = 0.03f;

    [Header("Comida")]
    [Tooltip("Distancia entre BORDES para poder comerse un cofre (0 = tocándolo)")]
    public float eatRange = 0.2f;

    [Header("Human States")]
    public bool isAlive = true;
    public HumanState currentState = HumanState.Exploring;

    private Vector3 destination;
    private float attackTimer = 0f;
    private bool monsterInSight = false;

    // Decisión de pelear/huir: se toma una vez por encuentro
    private bool encounterDecided = false;
    private bool panicked = false;

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
        // 1. Monstruo a la vista: corre más rápido y decide si huye
        Monster monster = GetNearestMonster();
        monsterInSight = monster != null;

        if (monsterInSight)
        {
            // Al ver al monstruo por primera vez decide si le da miedo
            if (!encounterDecided)
            {
                encounterDecided = true;
                panicked = Random.value < fleeOnSightChance;
            }

            // Sobrevivir va primero: con miedo o poca vida huye, incluso si hay cofres
            bool healthy = (health / maxHealth) >= fleeHealthThreshold;
            if (!healthy || panicked)
            {
                currentState = HumanState.Fleeing;
                return;
            }
        }
        else
        {
            // Ya no ve monstruos: el próximo encuentro se decide de nuevo
            encounterDecided = false;
            panicked = false;
        }

        // 2. Monstruo pegado a él (lo está atacando): se defiende y contraataca
        if (monsterInSight && EdgeDistance(this, monster) <= attackRange)
        {
            currentState = HumanState.Fighting;
            return;
        }

        // 3 y 4. Cofre a la vista: si lo toca se lo come, si no va hacia él
        Chest nearChest = FindNearestChest();
        if (nearChest != null)
        {
            currentState = EdgeDistance(this, nearChest) <= eatRange
                ? HumanState.OpeningChest
                : HumanState.SearchingChest;
            return;
        }

        // 5. Sin cofres a la vista: si hay monstruo, va a pelearlo
        if (monsterInSight)
        {
            currentState = HumanState.Fighting;
            return;
        }

        // 6. Explorar
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
        Chest chest = FindNearestChest();
        if (chest != null && EdgeDistance(this, chest) <= eatRange)
        {
            chest.Open(this);
        }
        destination = transform.position;
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

        float dist = EdgeDistance(this, enemy);

        if (dist <= attackRange)
        {
            // Ya está al alcance: se planta y golpea (no sigue empujando el collider)
            destination = transform.position;
            TryAttack(enemy);
        }
        else
        {
            destination = enemy.transform.position;
        }
    }

    void Flee()
    {
        Monster enemy = GetNearestMonster();
        if (enemy == null) return;

        bool cornered;
        destination = GetFleePoint(transform.position, enemy.transform.position, visionRange, out cornered);

        // Sin salida: se defiende en vez de quedarse quieto
        if (cornered && EdgeDistance(this, enemy) <= attackRange)
        {
            TryAttack(enemy);
        }
    }

    void TryAttack(Monster enemy)
    {
        if (attackTimer > 0f) return;
        attackTimer = attackCooldown;

        // Pequeña probabilidad de matar al instante; si no, daño normal
        if (Random.value < instantKillChance)
        {
            enemy.Kill();
        }
        else
        {
            enemy.TakeDamage(attack);
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
            return Kill();
        }
        return false;
    }

    /// <summary>Muerte instantánea (golpe letal del monstruo).</summary>
    public bool Kill()
    {
        if (!isAlive) return false;

        isAlive = false;
        Destroy(gameObject);
        return true;
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

    // ---------- Utilidades ----------

    /// <summary>
    /// Distancia entre los BORDES de los colliders (0 si se tocan).
    /// Si alguno no tiene collider, usa la distancia entre centros.
    /// Evita el bug de "se quedan quietos": los colliders se bloquean entre sí
    /// y los centros nunca llegan a estar dentro de attackRange.
    /// Monster también la usa (Human.EdgeDistance).
    /// </summary>
    public static float EdgeDistance(Component a, Component b)
    {
        Collider2D ca = a.GetComponent<Collider2D>();
        Collider2D cb = b.GetComponent<Collider2D>();

        if (ca != null && cb != null)
        {
            return Mathf.Max(0f, ca.Distance(cb).distance);
        }

        return Vector2.Distance(a.transform.position, b.transform.position);
    }

    /// <summary>
    /// Punto seguro hacia donde huir de una amenaza. Prueba varias direcciones
    /// (alejándose de la amenaza) y elige la que tiene más espacio libre, así no
    /// se queda pegado a una pared. 'cornered' es true si casi no hay salida.
    /// Monster también la usa (Human.GetFleePoint).
    /// </summary>
    public static Vector3 GetFleePoint(Vector3 from, Vector3 threat, float range, out bool cornered)
    {
        Vector2 away = from - threat;
        if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitCircle;
        away.Normalize();

        int obstacleMask = LayerMask.GetMask("Obstacles");
        float bestScore = float.MinValue;
        float bestFree = 0f;
        Vector2 bestDir = away;

        for (int i = -3; i <= 3; i++)
        {
            Vector2 dir = (Vector2)(Quaternion.Euler(0f, 0f, i * 40f) * (Vector3)away);
            RaycastHit2D hit = Physics2D.Raycast(from, dir, range, obstacleMask);
            float free = hit.collider != null ? hit.distance : range;

            // Prefiere direcciones libres y que no se desvíen mucho de "alejarse"
            float score = free - Mathf.Abs(i) * 0.4f;
            if (score > bestScore)
            {
                bestScore = score;
                bestFree = free;
                bestDir = dir;
            }
        }

        cornered = bestFree < 0.8f;
        float travel = Mathf.Max(0f, bestFree - 0.4f);
        return from + (Vector3)(bestDir * travel);
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