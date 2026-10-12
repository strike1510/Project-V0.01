using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit; // si pas utilisé, retire cette ligne

[DefaultExecutionOrder(100)] // passe APRÈS la boussole
public class CompassNeedle : MonoBehaviour
{
    public Transform target;        // PointBoussole
    public float angleOffset = 90f;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) return;

        float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(-90f, yaw + angleOffset, 0f);
    }
}