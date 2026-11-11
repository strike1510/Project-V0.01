using UnityEngine;

public class CycleJourNuit : MonoBehaviour
{
    [Header("Soleil")]
    public Light soleil;                       // la Directional Light
    public float dureeJournee = 300f;          // secondes pour un tour complet (5 min)
    [Range(0f, 24f)] public float heureDepart = 8f;
    public float inclinaisonNordSud = 20f;     // légère inclinaison de la trajectoire

    [Header("Intensité du soleil")]
    public float intensiteJour = 1.2f;
    public AnimationCurve courbeIntensite = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.1f, 0f),
        new Keyframe(0.35f, 1f), new Keyframe(0.65f, 1f),
        new Keyframe(0.9f, 0f), new Keyframe(1f, 0f));

    [Header("Couleur du soleil")]
    public Gradient couleurSoleil;

    [Header("Ambiance (optionnel)")]
    public Color ambianceJour = new Color(0.6f, 0.7f, 0.9f);
    public Color ambianceNuit = new Color(0.03f, 0.04f, 0.09f);

    float heure;

    void Reset()
    {
        // Dégradé par défaut : nuit -> aube orangée -> jour blanc -> crépuscule -> nuit
        couleurSoleil = new Gradient();
        couleurSoleil.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.1f, 0.1f, 0.3f), 0.00f),
                new GradientColorKey(new Color(1f, 0.55f, 0.3f), 0.25f),
                new GradientColorKey(Color.white, 0.50f),
                new GradientColorKey(new Color(1f, 0.5f, 0.25f), 0.75f),
                new GradientColorKey(new Color(0.1f, 0.1f, 0.3f), 1.00f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
    }

    void Start()
    {
        heure = heureDepart;
        if (!soleil) soleil = RenderSettings.sun;
    }

    void Update()
    {
        heure += (24f / dureeJournee) * Time.deltaTime;
        if (heure >= 24f) heure -= 24f;

        float t = heure / 24f;   // 0 à 1

        // Angle : 6h = lever (0°), 12h = zénith (90°), 18h = coucher (180°), 0h = sous la map
        float angle = (t * 360f) - 90f;

        // Rotation autour de l'axe Z -> le soleil traverse le ciel de l'est vers l'ouest
        soleil.transform.rotation = Quaternion.Euler(angle, 90f, 0f) * Quaternion.Euler(0f, 0f, inclinaisonNordSud);

        soleil.intensity = courbeIntensite.Evaluate(t) * intensiteJour;
        soleil.color = couleurSoleil.Evaluate(t);

        // Lumière ambiante qui suit la journée
        RenderSettings.ambientLight = Color.Lerp(ambianceNuit, ambianceJour, courbeIntensite.Evaluate(t));
    }
}