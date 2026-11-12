
using UnityEngine;

public class CycleJourNuit : MonoBehaviour
{
    [Header("Soleil")]
    public Light soleil;
    public float dureeJournee = 300f;
    [Range(0f, 24f)] public float heureDepart = 8f;
    public float inclinaisonNordSud = 20f;

    [Header("Intensité du soleil")]
    public float intensiteJour = 1.2f;
    public AnimationCurve courbeIntensite = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.1f, 0f),
        new Keyframe(0.35f, 1f),
        new Keyframe(0.65f, 1f),
        new Keyframe(0.9f, 0f),
        new Keyframe(1f, 0f));

    [Header("Couleur du soleil")]
    public Gradient couleurSoleil;

    [Header("Ambiance")]
    public Color ambianceJour = new Color(0.6f, 0.7f, 0.9f);
    public Color ambianceNuit = new Color(0.03f, 0.04f, 0.09f);

    [Header("Etoiles")]
    public Transform cielEtoiles;

    Renderer[] renderersEtoiles;
    Material[][] materiauxEtoiles;
    Color[][] couleursOriginales;
    int[][] proprietesCouleur;

    float heure;

    void Start()
    {
        heure = heureDepart;

        if (!soleil)
            soleil = RenderSettings.sun;

        // Récupération de toutes les étoiles et lignes
        if (cielEtoiles != null)
        {
            renderersEtoiles =
                cielEtoiles.GetComponentsInChildren<Renderer>(true);

            materiauxEtoiles = new Material[renderersEtoiles.Length][];
            couleursOriginales = new Color[renderersEtoiles.Length][];
            proprietesCouleur = new int[renderersEtoiles.Length][];

            for (int i = 0; i < renderersEtoiles.Length; i++)
            {
                materiauxEtoiles[i] = renderersEtoiles[i].materials;

                couleursOriginales[i] =
                    new Color[materiauxEtoiles[i].Length];

                proprietesCouleur[i] =
                    new int[materiauxEtoiles[i].Length];

                for (int j = 0; j < materiauxEtoiles[i].Length; j++)
                {
                    Material mat = materiauxEtoiles[i][j];

                    if (mat.HasProperty("_BaseColor"))
                        proprietesCouleur[i][j] =
                            Shader.PropertyToID("_BaseColor");
                    else if (mat.HasProperty("_Color"))
                        proprietesCouleur[i][j] =
                            Shader.PropertyToID("_Color");

                    if (proprietesCouleur[i][j] != 0)
                    {
                        couleursOriginales[i][j] =
                            mat.GetColor(proprietesCouleur[i][j]);
                    }
                }
            }
        }
    }

    void Update()
    {
        heure += (24f / dureeJournee) * Time.deltaTime;

        if (heure >= 24f)
            heure -= 24f;

        float t = heure / 24f;

        float angle = (t * 360f) - 90f;

        if (soleil != null)
        {
            soleil.transform.rotation =
                Quaternion.Euler(angle, 90f, 0f) *
                Quaternion.Euler(0f, 0f, inclinaisonNordSud);

            soleil.intensity =
                courbeIntensite.Evaluate(t) * intensiteJour;

            soleil.color = couleurSoleil.Evaluate(t);
        }

        float intensiteAmbiante = courbeIntensite.Evaluate(t);

        RenderSettings.ambientLight =
            Color.Lerp(ambianceNuit, ambianceJour, intensiteAmbiante);

        // Apparition progressive des étoiles
        float visibiliteEtoiles =
            1f - Mathf.SmoothStep(0.0f, 0.20f, intensiteAmbiante);

        ActualiserEtoiles(visibiliteEtoiles);
    }

    void ActualiserEtoiles(float visibilite)
    {
        if (renderersEtoiles == null)
            return;

        for (int i = 0; i < materiauxEtoiles.Length; i++)
        {
            for (int j = 0; j < materiauxEtoiles[i].Length; j++)
            {
                int propriete = proprietesCouleur[i][j];

                if (propriete == 0)
                    continue;

                Color couleur = couleursOriginales[i][j];

                couleur.a *= visibilite;

                materiauxEtoiles[i][j].SetColor(
                    propriete, couleur);
            }
        }
    }
}
