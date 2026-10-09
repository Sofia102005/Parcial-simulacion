using UnityEngine;

/// <summary>
/// Ponlo en el prefab de la cueva. Define el área alrededor de la cual
/// los monstruos patrullan y defienden.
/// </summary>
public class Cave : MonoBehaviour
{
    [Tooltip("Radio de patrulla alrededor de la cueva")]
    public float radius = 4f;

    public Vector3 GetRandomPoint()
    {
        Vector2 offset = Random.insideUnitCircle * radius;
        return transform.position + (Vector3)offset;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}