using UnityEngine;

public class Monster : MonoBehaviour
{
    [Header("Monster Stats")]
    public float health = 15f;
    public float attack = 3f;
    public float energy = 10f;
    public float maxEnergy = 20f;
    public float energyDrainPerSecond = 0.3f;
    public float visionRange = 5f;

    [Header("Speed")]
    public float baseSpeed = 1f;

    [Header("Combat")]
    public float attackRange = 0.5f;
    public float attackCooldown = 1f;

    [Header("Cave / Territory")]
    [Tooltip("Si se deja vacío, busca la cueva más cercana al iniciar")]
    public Cave cave;
    [Tooltip("Radio de patrulla (se toma del radio de la cueva)")]
    public float patrolRadius = 4f;
    [Tooltip("Margen extra sobre el radio de patrulla hasta donde ataca y persigue. Defensor = pequeño, Patrullero = grande")]
    public float leashExtra = 3f;

    [Header("Ambush (esconderse detrás de cofres)")]
    [Tooltip("Probabilidad de emboscar cada vez que revisa (si ve un cofre)")]
    [Range(0f, 1f)] public float ambushChance = 0.3f;
    [Tooltip("Cada cuántos segundos revisa si se esconde")]
    public float ambushCheckInterval = 2f;
    [Tooltip("Distancia al cofre a la que debe llegar el humano para que salte")]
    public float ambushRange = 1.5f;
    [Tooltip("Qué tan detrás del cofre se esconde")]
    public float hideDistance = 0.6f;
    [Tooltip("Tiempo máximo esperando antes de rendirse")]
    public float maxAmbushTime = 12f;

    [Header("Mimic")]
    public bool isMimic = false;

    [Header("Monster States")]
    public bool isAlive = true;
    public bool isHidden = false;   // los humanos no ven a un monstruo escondido
    public MonsterState currentState = MonsterState.Patrolling;

    private Vector3 destination;
    private Vector3 home;
    private Human target;
    private float attackTimer = 0f;

    // Emboscada
    private Chest ambushChest;
    private Vector3 hidePos;
    private float ambushTimer = 0f;
    private float ambushCheckTimer = 0f;
    private bool surpriseReady = false;

    public float CurrentSpeed
    {
        get { return baseSpeed; }
    }

    private void Start()
    {
        if (isMimic)
        {
            // El mímico se queda donde estaba el cofre
            cave = null;
            patrolRadius = 1.5f;
            home = transform.position;
        }
        else
        {
            if (cave == null)
            {
                cave = FindNearestCave();
            }

            if (cave != null)
            {
                home = cave.transform.position;
                patrolRadius = cave.radius;
            }
            else
            {
                home = transform.position;
            }
        }

        destination = transform.position;

        SimulationManager manager = FindAnyObjectByType<SimulationManager>();
        if (manager != null)
        {
            manager.RegisterMonster(this);
        }
    }

    public void Simulate(float h)
    {
        if (!isAlive) return;

        attackTimer -= h;
        ambushCheckTimer -= h;

        EvaluateState();

        switch (currentState)
        {
            case MonsterState.Patrolling: Patrol();      break;
            case MonsterState.Chasing:    Chase();       break;
            case MonsterState.Attacking:  AttackHuman(); break;
            case MonsterState.Returning:  ReturnHome();  break;
            case MonsterState.Ambushing:  Ambush(h);     break;
        }

        transform.position = Vector3.MoveTowards(transform.position, destination, CurrentSpeed * h);

        if (!isMimic)
        {
            energy -= energyDrainPerSecond * h;
        }

        if (energy <= 0f)
        {
            Die();
        }
    }

    void EvaluateState()
    {
        target = FindNearestHuman();
        bool targetInTerritory = target != null && IsInsideTerritory(target.transform.position);

        // Emboscada en curso: seguir escondido hasta que el humano llegue al cofre
        if (currentState == MonsterState.Ambushing)
        {
            if (ambushChest == null || ambushTimer <= 0f)
            {
                // El cofre desapareció o se cansó de esperar
                StopAmbush();
            }
            else if (targetInTerritory &&
                     Vector2.Distance(target.transform.position, ambushChest.transform.position) <= ambushRange)
            {
                // ¡Sale a atacar con sorpresa!
                StopAmbush();
                surpriseReady = true;
                currentState = MonsterState.Chasing;
                return;
            }
            else
            {
                return; // sigue escondido
            }
        }

        // Humano dentro del territorio: perseguir / atacar
        if (targetInTerritory)
        {
            float dist = Vector2.Distance(transform.position, target.transform.position);
            currentState = dist <= attackRange ? MonsterState.Attacking : MonsterState.Chasing;
            return;
        }

        // Fuera de su territorio: volver
        if (!IsInsideTerritory(transform.position))
        {
            currentState = MonsterState.Returning;
            return;
        }

        // Sin humanos a la vista: a veces se esconde detrás de un cofre
        if (!isMimic && ambushCheckTimer <= 0f)
        {
            ambushCheckTimer = ambushCheckInterval;

            if (Random.value < ambushChance)
            {
                Chest chest = FindNearestFreeChest();
                if (chest != null)
                {
                    StartAmbush(chest);
                    return;
                }
            }
        }

        currentState = MonsterState.Patrolling;
    }

    void Patrol()
    {
        if (Vector3.Distance(transform.position, destination) < 0.2f)
        {
            SelectNewDestination();
        }
    }

    void Chase()
    {
        if (target != null)
        {
            destination = target.transform.position;
        }
    }

    void AttackHuman()
    {
        if (target == null) return;

        destination = target.transform.position;

        if (attackTimer <= 0f)
        {
            attackTimer = attackCooldown;

            if (surpriseReady)
            {
                surpriseReady = false;
                SurpriseAttack(target);
            }
            else
            {
                Hit(target);
            }
        }
    }

    void ReturnHome()
    {
        destination = home;
    }

    // ---------- Emboscada ----------

    void StartAmbush(Chest chest)
    {
        ambushChest = chest;
        chest.hasAmbusher = true;
        ambushTimer = maxAmbushTime;

        // Se esconde del lado de la cueva (el humano llega desde afuera)
        Vector3 toHome = home - chest.transform.position;
        Vector3 dir = toHome.sqrMagnitude > 0.01f ? toHome.normalized : Vector3.up;
        hidePos = chest.transform.position + dir * hideDistance;

        currentState = MonsterState.Ambushing;
    }

    void Ambush(float h)
    {
        destination = hidePos;
        ambushTimer -= h;

        // Solo queda oculto cuando ya llegó detrás del cofre
        isHidden = Vector3.Distance(transform.position, hidePos) < 0.2f;
    }

    void StopAmbush()
    {
        if (ambushChest != null)
        {
            ambushChest.hasAmbusher = false;
        }
        ambushChest = null;
        isHidden = false;
        currentState = MonsterState.Patrolling;
    }

    Chest FindNearestFreeChest()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, visionRange, LayerMask.GetMask("Chests"));
        Chest nearest = null;
        float minDist = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            Chest c = hit.GetComponent<Chest>();
            if (c == null || c.isMimic || c.hasAmbusher) continue;
            if (!IsInsideTerritory(c.transform.position)) continue;

            float dist = Vector2.Distance(transform.position, c.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = c;
            }
        }
        return nearest;
    }

    // ---------- Patrulla ----------

    void SelectNewDestination()
    {
        Vector3 candidate = (cave != null) ? cave.GetRandomPoint() : home + (Vector3)(Random.insideUnitCircle * patrolRadius);
        Vector3 direction = candidate - transform.position;

        if (direction.magnitude < 0.01f) return;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction.normalized, direction.magnitude, LayerMask.GetMask("Obstacles"));

        if (hit.collider != null)
        {
            float offset = transform.localScale.magnitude * 0.5f;
            destination = hit.point - (Vector2)direction.normalized * offset;
        }
        else
        {
            destination = candidate;
        }
    }

    // ---------- Combate ----------

    /// <summary>Golpea al humano; si lo mata, se lo come y recupera energía.</summary>
    void Hit(Human victim)
    {
        float victimValue = victim.maxHealth;
        if (victim.TakeDamage(attack))
        {
            energy = Mathf.Min(maxEnergy, energy + victimValue);
        }
    }

    /// <summary>Ataque sorpresa (mímico o emboscada): doble de daño.</summary>
    public void SurpriseAttack(Human victim)
    {
        if (victim == null) return;

        float victimValue = victim.maxHealth;
        if (victim.TakeDamage(attack * 2f))
        {
            energy = Mathf.Min(maxEnergy, energy + victimValue);
        }
    }

    public void TakeDamage(float amount)
    {
        if (!isAlive) return;

        health -= amount;
        if (health <= 0f)
        {
            Die();
        }
    }

    void Die()
    {
        isAlive = false;
        if (ambushChest != null) ambushChest.hasAmbusher = false;
        Destroy(gameObject);
    }

    // ---------- Utilidades ----------

    bool IsInsideTerritory(Vector3 pos)
    {
        return Vector2.Distance(pos, home) <= patrolRadius + leashExtra;
    }

    Cave FindNearestCave()
    {
        Cave[] caves = FindObjectsByType<Cave>(FindObjectsSortMode.None);
        Cave nearest = null;
        float minDist = Mathf.Infinity;

        foreach (Cave c in caves)
        {
            float dist = Vector2.Distance(transform.position, c.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = c;
            }
        }
        return nearest;
    }

    Human FindNearestHuman()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, visionRange, LayerMask.GetMask("Humans"));
        Human nearest = null;
        float minDist = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            Human hu = hit.GetComponent<Human>();
            if (hu != null && hu.isAlive)
            {
                float dist = Vector2.Distance(transform.position, hu.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = hu;
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

        if (ambushChest != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(ambushChest.transform.position, ambushRange);
        }
    }
}