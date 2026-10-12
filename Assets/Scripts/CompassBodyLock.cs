using UnityEngine;

[DefaultExecutionOrder(0)] // passe AVANT l'aiguille
public class CompassBodyLock : MonoBehaviour
{
    Quaternion lockedRotation;

    void Start()
    {
        lockedRotation = transform.rotation;
    }

    void LateUpdate()
    {
        transform.rotation = lockedRotation;
    }
}