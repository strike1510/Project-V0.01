using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cycle jour / nuit pendant le voyage.
/// - Le jour : soleil, ciel bleu (on navigue à la boussole).
/// - La nuit : il fait sombre, le ciel s'assombrit, les étoiles (Ciel_Etoiles) apparaissent :
///   on navigue à l'Étoile polaire, qui indique le Nord. Une faible lumière de lune permet de voir le pont.
/// </summary>
public class CycleJourNuit : MonoBehaviour
{
    [Header("Soleil")]
    public Light soleil;
    [Tooltip("Durée d'une journée complète (s).")]
    public float dureeJournee = 360f;
    [Range(0f, 24f)] public float heureDepart = 13f;
    public float inclinaisonNordSud = 20f;

    [Header("Intensité du soleil")]
    public float intensiteJour = 1.2f;
    public AnimationCurve courbeIntensite = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.22f, 0f),
        new Keyframe(0.32f, 1f),
        new Keyframe(0.72f, 1f),
        new Keyframe(0.82f, 0f),
        new Keyframe(1f, 0f));

    [Header("Couleur du soleil")]
    public Gradient couleurSoleil;

    [Header("Ambiance")]
    public Color ambianceJour = new Color(0.6f, 0.7f, 0.9f);
    public Color ambianceNuit = new Color(0.035f, 0.045f, 0.10f);
    [Tooltip("Teinte du ciel (SkyDome, nuages) la nuit.")]
    public Color cielNuit = new Color(0.03f, 0.04f, 0.10f);
    public Color cielCrepuscule = new Color(0.95f, 0.55f, 0.35f);

    [Header("Lune")]
    public float intensiteLune = 0.18f;
    public Color couleurLune = new Color(0.55f, 0.65f, 1f);

    [Header("Etoiles")]
    public Transform cielEtoiles;

    /// <summary>0 = plein jour, 1 = pleine nuit (lisible par les autres scripts).</summary>
    public static float Nuit { get; private set; }
    /// <summary>Heure actuelle (0..24).</summary>
    public static float Heure { get; private set; }

    class EtoileRendu
    {
        public Renderer r;
        public Material[] mats;
        public Color[] couleurs;
        public int[] proprietes;
        public LineRenderer ligne;
        public Color ligneDebut, ligneFin;
    }

    readonly List<EtoileRendu> etoiles = new List<EtoileRendu>();
    readonly List<Renderer> ciel = new List<Renderer>();
    MaterialPropertyBlock mpb;
    Material skybox;
    float expositionJour = 1f, reflexionJour = 1f;
    Color brouillardJour;
    Light lune;
    float heure;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    void Start()
    {
        heure = heureDepart;
        if (!soleil) soleil = RenderSettings.sun;
        if (couleurSoleil == null || couleurSoleil.colorKeys.Length < 2)
        {
            couleurSoleil = new Gradient();
            couleurSoleil.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.5f, 0.3f), 0.25f), new GradientColorKey(new Color(1f, 0.96f, 0.88f), 0.4f),
                        new GradientColorKey(new Color(1f, 0.96f, 0.88f), 0.65f), new GradientColorKey(new Color(1f, 0.45f, 0.25f), 0.8f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        }
        mpb = new MaterialPropertyBlock();

        if (cielEtoiles == null)
        {
            var go = GameObject.Find("Ciel_Etoiles");
            if (go) cielEtoiles = go.transform;
        }
        if (cielEtoiles != null)
        {
            foreach (var r in cielEtoiles.GetComponentsInChildren<Renderer>(true))
            {
                var e = new EtoileRendu { r = r };
                if (r is LineRenderer lr) { e.ligne = lr; e.ligneDebut = lr.startColor; e.ligneFin = lr.endColor; }
                e.mats = r.materials;   // instances : on peut les modifier sans toucher aux fichiers
                e.couleurs = new Color[e.mats.Length];
                e.proprietes = new int[e.mats.Length];
                for (int j = 0; j < e.mats.Length; j++)
                {
                    var m = e.mats[j];
                    if (m.HasProperty(BaseColor)) e.proprietes[j] = BaseColor;
                    else if (m.HasProperty(ColorId)) e.proprietes[j] = ColorId;
                    if (e.proprietes[j] != 0) e.couleurs[j] = m.GetColor(e.proprietes[j]);
                }
                etoiles.Add(e);
            }
        }

        // Le ciel (SkyDome + nuages) utilise un matériau "Unlit" qui ne réagit pas à la lumière : on le teinte nous-mêmes
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            var m = r.sharedMaterial;
            if (m == null) continue;
            string n = m.name + " " + r.name;
            if (n.Contains("SimpleSky") || n.Contains("SkyDome") || n.Contains("Cloud")) ciel.Add(r);
        }

        // L'ambiance doit être une couleur unie pour pouvoir assombrir la nuit
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        reflexionJour = RenderSettings.reflectionIntensity;
        if (RenderSettings.skybox != null)
        {
            skybox = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skybox;
            if (skybox.HasProperty("_Exposure")) expositionJour = skybox.GetFloat("_Exposure");
        }
        brouillardJour = RenderSettings.fogColor;
        Debug.Log($"[Drakkar] Cycle jour/nuit : {etoiles.Count} étoiles, {ciel.Count} éléments de ciel teintés, skybox = {(skybox ? skybox.shader.name : "aucune")}");

        // Lune : faible lumière bleutée la nuit
        var go2 = new GameObject("Lune");
        go2.transform.SetParent(transform, false);
        go2.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        lune = go2.AddComponent<Light>();
        lune.type = LightType.Directional;
        lune.color = couleurLune;
        lune.intensity = 0f;
        lune.shadows = LightShadows.None;
    }

    void Update()
    {
        heure += (24f / Mathf.Max(1f, dureeJournee)) * Time.deltaTime;
        if (heure >= 24f) heure -= 24f;
        Heure = heure;
        float t = heure / 24f;
        float angle = (t * 360f) - 90f;
        float jour = courbeIntensite.Evaluate(t);

        if (soleil != null)
        {
            soleil.transform.rotation = Quaternion.Euler(angle, 90f, 0f) * Quaternion.Euler(0f, 0f, inclinaisonNordSud);
            soleil.intensity = jour * intensiteJour;
            soleil.color = couleurSoleil.Evaluate(t);
            soleil.enabled = soleil.intensity > 0.001f;
        }

        RenderSettings.ambientLight = Color.Lerp(ambianceNuit, ambianceJour, jour);
        Nuit = 1f - Mathf.SmoothStep(0f, 0.35f, jour);
        if (lune) lune.intensity = intensiteLune * Nuit;

        // teinte du ciel : nuit -> crépuscule -> jour
        float crepuscule = Mathf.Clamp01(1f - Mathf.Abs(jour - 0.35f) / 0.35f) * 0.6f;
        Color teinte = Color.Lerp(cielNuit, Color.white, jour);
        teinte = Color.Lerp(teinte, teinte * cielCrepuscule * 1.4f, crepuscule);
        teinte.a = 1f;
        foreach (var r in ciel)
        {
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColor, teinte);
            mpb.SetColor(ColorId, teinte);
            r.SetPropertyBlock(mpb);
        }

        RenderSettings.reflectionIntensity = Mathf.Lerp(0.08f, reflexionJour, jour);
        if (skybox && skybox.HasProperty("_Exposure")) skybox.SetFloat("_Exposure", Mathf.Lerp(0.03f, expositionJour, jour));
        if (skybox && skybox.HasProperty("_Tint")) skybox.SetColor("_Tint", Color.Lerp(new Color(0.1f, 0.12f, 0.2f), new Color(0.5f, 0.5f, 0.5f), jour));
        if (RenderSettings.fog) RenderSettings.fogColor = Color.Lerp(cielNuit, brouillardJour, jour);

        ActualiserEtoiles(Nuit);
    }

    void ActualiserEtoiles(float visibilite)
    {
        bool visibles = visibilite > 0.02f;
        foreach (var e in etoiles)
        {
            if (e.r == null) continue;
            if (e.r.enabled != visibles) e.r.enabled = visibles;
            if (!visibles) continue;
            for (int j = 0; j < e.mats.Length; j++)
            {
                int p = e.proprietes[j];
                if (p == 0) continue;
                Color c = e.couleurs[j];
                // fonctionne pour les matériaux opaques (on assombrit) comme transparents (on rend invisible)
                Color v = Color.Lerp(Color.black, c, visibilite);
                v.a = c.a * visibilite;
                e.mats[j].SetColor(p, v);
            }
            if (e.ligne)
            {
                Color a = e.ligneDebut, b = e.ligneFin;
                a.a *= visibilite; b.a *= visibilite;
                e.ligne.startColor = a; e.ligne.endColor = b;
            }
        }
    }
}
