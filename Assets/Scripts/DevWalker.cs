using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// OUTIL DE TEST UNIQUEMENT : se promener dans la scène au clavier/souris, sans casque VR.
/// À mettre sur la Main Camera. Ne pas garder dans la version finale.
///
/// ZQSD ou WASD : avancer / reculer / côtés · Clic droit maintenu : regarder autour · Maj : courir
/// La caméra reste à hauteur des yeux au-dessus du sol (terrain, pontons...).
/// </summary>
public class DevWalker : MonoBehaviour
{
    public float walkSpeed = 3f;
    public float runSpeed = 8f;
    public float lookSensitivity = 0.15f;
    [Tooltip("Hauteur des yeux au-dessus du sol (m).")]
    public float eyeHeight = 1.7f;

    float yaw, pitch;

    void Start()
    {
        Vector3 e = transform.eulerAngles;
        yaw = e.y;
        pitch = e.x > 180f ? e.x - 360f : e.x;
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        if (mouse.rightButton.isPressed)
        {
            Vector2 d = mouse.delta.ReadValue() * lookSensitivity;
            yaw += d.x;
            pitch = Mathf.Clamp(pitch - d.y, -80f, 80f);
        }
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // Les deux dispositions de clavier (AZERTY et QWERTY) fonctionnent
        float fwd = (kb.wKey.isPressed || kb.zKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        float side = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.qKey.isPressed ? 1f : 0f);
        Vector3 flatFwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 flatRight = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
        Vector3 move = (flatFwd * fwd + flatRight * side);
        if (move.sqrMagnitude > 1f) move.Normalize();
        float speed = kb.leftShiftKey.isPressed ? runSpeed : walkSpeed;
        // Bloque devant les obstacles (menhir, caisses...) à hauteur de la taille
        Vector3 waist = transform.position + Vector3.down * (eyeHeight - 0.8f);
        if (move.sqrMagnitude > 0f && Physics.SphereCast(waist, 0.25f, move.normalized, out _, 0.35f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            move = Vector3.zero;
        Vector3 pos = transform.position + move * speed * Time.deltaTime;

        // Suit le sol
        if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 50f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            pos.y = Mathf.Lerp(pos.y, hit.point.y + eyeHeight, 1f - Mathf.Exp(-12f * Time.deltaTime));

        transform.position = pos;
    }
}
