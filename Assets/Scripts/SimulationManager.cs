using System.Collections.Generic;
using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    [Header("Limits")]
    public int maxHumans = 20;

    [Header("Lists & Spawners")]
    public List<Human> humans = new List<Human>();
    public List<Monster> monsters = new List<Monster>();
    public ChestSpawner chestSpawner;

    void Start()
    {
        humans = new List<Human>(FindObjectsByType<Human>(FindObjectsSortMode.InstanceID));
        monsters = new List<Monster>(FindObjectsByType<Monster>(FindObjectsSortMode.InstanceID));
        chestSpawner = FindFirstObjectByType<ChestSpawner>();
    }

    void Update()
    {
        Simulate(Time.deltaTime);
    }

    void Simulate(float deltaTime)
    {
        // 1. Humanos
        humans.RemoveAll(x => x == null || !x.isAlive);
        for (int i = humans.Count - 1; i >= 0; i--)
        {
            if (humans[i] != null && humans[i].isAlive)
            {
                humans[i].Simulate(deltaTime);
            }
        }

        // 2. Monstruos (los mímicos nuevos se agregan durante la simulación, por eso iteramos con índice)
        monsters.RemoveAll(x => x == null || !x.isAlive);
        for (int i = monsters.Count - 1; i >= 0; i--)
        {
            if (i < monsters.Count && monsters[i] != null && monsters[i].isAlive)
            {
                monsters[i].Simulate(deltaTime);
            }
        }

        // 3. Cofres
        if (chestSpawner != null)
        {
            chestSpawner.Simulate(deltaTime);
        }
    }

    public bool RegisterHuman(Human newHuman)
    {
        humans.RemoveAll(x => x == null || !x.isAlive);

        if (humans.Count >= maxHumans)
        {
            if (newHuman != null && !humans.Contains(newHuman))
            {
                Destroy(newHuman.gameObject);
            }
            return false;
        }

        if (newHuman != null && !humans.Contains(newHuman))
        {
            humans.Add(newHuman);
            return true;
        }
        return false;
    }

    public void RegisterMonster(Monster newMonster)
    {
        if (newMonster != null && !monsters.Contains(newMonster))
        {
            monsters.Add(newMonster);
        }
    }
}
//Se uso Claude para generacion de codigo
