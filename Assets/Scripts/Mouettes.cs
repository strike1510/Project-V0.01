using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fait voler quelques mouettes au hasard dans une zone.
/// La zone est la boîte cyan visible dans la vue Scene : déplace et redimensionne l'objet pour la placer
/// au-dessus du port. Chaque mouette choisit un point au hasard dans la boîte, y vole en virant doucement
/// (en s'inclinant dans les virages), puis en choisit un autre.
///
/// Utilisation : objet vide → Add Component → Mouettes → glisse ton prefab de mouette dans « Bird Prefab ».
/// </summary>
public class Mouettes : MonoBehaviour
{
    [Header("Oiseaux")]
    [Tooltip("Le prefab de la mouette (doit regarder vers l'axe Z bleu).")]
    public GameObject birdPrefab;
    [Range(1, 30)] public int count = 8;
    [Tooltip("Multiplie la taille du prefab. Augmente si elles sont trop petites.")]
    public float sizeMultiplier = 1f;

    [Header("Zone de vol (boîte cyan dans la vue Scene)")]
    [Tooltip("Largeur, hauteur, profondeur de la zone en mètres. Centrée sur cet objet.")]
    public Vector3 zoneSize = new Vector3(60f, 12f, 60f);

    [Header("Vol")]
    public float minSpeed = 4f;
    public float maxSpeed = 7f;
    [Tooltip("Vitesse de virage (degrés par seconde).")]
    public float turnSpeed = 45f;
    [Tooltip("Inclinaison max dans les virages (degrés).")]
    public float maxBank = 30f;
    [Tooltip("Temps max avant de changer d'idée et de choisir une nouvelle direction (s).")]
    public float maxTimeToTarget = 8f;

    class Bird
    {
        public Transform t;
        public Vector3 target;
        public float speed;
        public float timer;
        public float yaw, pitch, bank;
    }

    readonly List<Bird> birds = new List<Bird>();

    void Start()
    {
        if (birdPrefab == null)
        {
            Debug.LogError("[Mouettes] Glisse ton prefab de mouette dans le champ « Bird Prefab ».", this);
            return;
        }

        Vector3 baseScale = birdPrefab.transform.localScale * sizeMultiplier;
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(birdPrefab, RandomPointInZone(), Quaternion.identity, transform);
            go.name = "Mouette " + (i + 1);
            // Le parent peut être mis à l'échelle : on garde la taille voulue dans le monde
            go.transform.localScale = Vector3.Scale(baseScale, InverseScale(transform.lossyScale));
            foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;

            var b = new Bird { t = go.transform, yaw = Random.Range(0f, 360f) };
            NewTarget(b);
            birds.Add(b);
        }
        Debug.Log($"[Mouettes] {birds.Count} mouettes créées autour de {transform.position}.", this);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        foreach (var b in birds)
        {
            b.timer -= dt;
            Vector3 toTarget = b.target - b.t.position;
            if (toTarget.magnitude < 4f || b.timer <= 0f)
            {
                NewTarget(b);
                toTarget = b.target - b.t.position;
            }

            // Direction voulue → cap (yaw) et assiette (pitch) voulus
            float wantYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            float flat = new Vector2(toTarget.x, toTarget.z).magnitude;
            float wantPitch = Mathf.Clamp(-Mathf.Atan2(toTarget.y, flat) * Mathf.Rad2Deg, -20f, 20f);

            float yawDelta = Mathf.DeltaAngle(b.yaw, wantYaw);
            float yawStep = Mathf.Clamp(yawDelta, -turnSpeed * dt, turnSpeed * dt);
            b.yaw += yawStep;
            b.pitch = Mathf.MoveTowards(b.pitch, wantPitch, turnSpeed * 0.5f * dt);

            // S'incline dans les virages, comme un vrai oiseau qui plane
            float wantBank = -Mathf.Clamp(yawDelta / 45f, -1f, 1f) * maxBank;
            b.bank = Mathf.Lerp(b.bank, wantBank, 1f - Mathf.Exp(-3f * dt));

            b.t.rotation = Quaternion.Euler(b.pitch, b.yaw, b.bank);
            b.t.position += Quaternion.Euler(b.pitch, b.yaw, 0f) * Vector3.forward * (b.speed * dt);
        }
    }

    void NewTarget(Bird b)
    {
        b.target = RandomPointInZone();
        b.speed = Random.Range(minSpeed, maxSpeed);
        b.timer = Random.Range(maxTimeToTarget * 0.4f, maxTimeToTarget);
    }

    Vector3 RandomPointInZone()
    {
        Vector3 half = zoneSize * 0.5f;
        return transform.position + new Vector3(
            Random.Range(-half.x, half.x),
            Random.Range(-half.y, half.y),
            Random.Range(-half.z, half.z));
    }

    static Vector3 InverseScale(Vector3 s) =>
        new Vector3(s.x != 0f ? 1f / s.x : 1f, s.y != 0f ? 1f / s.y : 1f, s.z != 0f ? 1f / s.z : 1f);

    // Boîte cyan dans la vue Scene : c'est là que les mouettes voleront
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireCube(transform.position, zoneSize);
        Gizmos.color = new Color(0f, 0.9f, 1f, 0.08f);
        Gizmos.DrawCube(transform.position, zoneSize);
    }
}
