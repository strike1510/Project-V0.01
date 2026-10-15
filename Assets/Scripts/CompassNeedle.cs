using UnityEngine;

[DefaultExecutionOrder(100)]
public class CompassNeedle : MonoBehaviour
{
    public Transform target;        // le GameObject à viser
    public Transform pivot;         // point fixe au centre de la boussole
    public float angleOffset = 0f;  // ajuste si l'aiguille vise de travers

    void LateUpdate()
    {
        if (target == null || pivot == null) return;

        // origine de l'aiguille = le point central
        transform.position = pivot.position;

        // direction vers la cible, à plat (face dessus toujours vers le haut)
        Vector3 dir = target.position - pivot.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) return;

        float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(-90f, yaw + angleOffset, 0f);
    }
}