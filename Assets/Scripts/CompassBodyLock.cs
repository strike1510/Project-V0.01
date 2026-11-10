using UnityEngine;

[DefaultExecutionOrder(0)]
public class CompassBodyLock : MonoBehaviour
{
    void LateUpdate()
    {
        transform.eulerAngles = new Vector3(-90f, 0f, 0f);
    }
}