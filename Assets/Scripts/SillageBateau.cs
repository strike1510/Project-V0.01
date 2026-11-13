using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Effets d'eau du drakkar, qui suivent sa vitesse :
/// - écume à la proue (l'avant fend l'eau),
/// - sillage d'écume qui s'élargit derrière le bateau,
/// - éclaboussures à l'avant quand il va vite,
/// - son d'eau (optionnel).
/// À l'arrêt, plus rien n'est émis et l'écume restante s'efface doucement.
///
/// Utilisation : sur Langskip_v2 (l'objet qui porte le script Ordre) → Add Component → Sillage Bateau.
/// Le matériau « Ecume_Mat » est rempli tout seul. Glisse Water_Plane dans « Eau » (sinon la hauteur 0.4 est utilisée).
/// </summary>
[DisallowMultipleComponent]
public class SillageBateau : MonoBehaviour
{
    [Header("Eau")]
    [Tooltip("L'objet Water_Plane : sa hauteur sert de niveau de l'eau. Vide = la valeur ci-dessous.")]
    public Transform eau;
    public float niveauEau = 0.4f;

    [Header("Apparence")]
    [Tooltip("Matériau de particules (Assets/Materials/Ecume_Mat).")]
    public Material materiauEcume;
    public Color couleurEcume = new Color(1f, 1f, 1f, 0.8f);
    [Tooltip("Vitesse (m/s) à laquelle les effets sont au maximum. Le drakkar va à environ 1,2 m/s.")]
    public float vitessePleine = 1.2f;
    [Tooltip("Multiplie la quantité d'écume.")]
    [Range(0f, 3f)] public float intensite = 1f;
    [Tooltip("Éclaboussures à l'avant quand le bateau va vite.")]
    public bool eclaboussures = true;

    [Header("Forme du bateau")]
    [Tooltip("Coché : l'avant, l'arrière et la largeur sont calculés depuis les colliders de la coque.")]
    public bool calculAuto = true;
    [Tooltip("Point avant, en coordonnées locales du bateau.")]
    public Vector3 proueLocale = new Vector3(0f, 0f, 8f);
    [Tooltip("Point arrière, en coordonnées locales du bateau.")]
    public Vector3 poupeLocale = new Vector3(0f, 0f, -8f);
    [Tooltip("Largeur de la coque en mètres.")]
    public float largeur = 3f;

    [Header("Son (optionnel)")]
    public AudioClip sonEau;
    [Range(0f, 1f)] public float volumeMax = 0.6f;

    ParticleSystem psProue, psSillage, psEclaboussures;
    AudioSource audioEau;
    Vector3 dernierePos;
    float vitesse;                 // m/s, positive en marche avant
    Vector3 avantLocal = Vector3.forward;

    // ------------------------------------------------------------------ mise en place

    void Start()
    {
        var ordre = GetComponent<Ordre>();
        if (ordre != null && ordre.directionAvant.sqrMagnitude > 0.0001f)
            avantLocal = ordre.directionAvant.normalized;

        if (calculAuto) CalculerForme();

        if (materiauEcume == null)
        {
            Debug.LogWarning("[SillageBateau] Aucun matériau : glisse Assets/Materials/Ecume_Mat dans « Materiau Ecume ».", this);
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader != null) materiauEcume = new Material(shader);
        }

        psProue = CreerSysteme("Ecume_Proue", 200, ParticleSystemRenderMode.HorizontalBillboard);
        ReglerProue(psProue);
        psSillage = CreerSysteme("Ecume_Sillage", 350, ParticleSystemRenderMode.HorizontalBillboard);
        ReglerSillage(psSillage);
        if (eclaboussures)
        {
            psEclaboussures = CreerSysteme("Eclaboussures", 80, ParticleSystemRenderMode.Billboard);
            ReglerEclaboussures(psEclaboussures);
        }

        if (sonEau != null)
        {
            audioEau = gameObject.AddComponent<AudioSource>();
            audioEau.clip = sonEau;
            audioEau.loop = true;
            audioEau.spatialBlend = 1f;
            audioEau.volume = 0f;
            audioEau.maxDistance = 40f;
            audioEau.Play();
        }

        dernierePos = transform.position;
    }

    /// <summary>Trouve l'avant, l'arrière et la largeur de la coque à partir de ses colliders (ou de ses maillages).</summary>
    void CalculerForme()
    {
        var points = new List<Vector3>();
        foreach (var c in GetComponentsInChildren<Collider>())
            if (!c.isTrigger && c.enabled) AjouterCoins(c.bounds, points);
        if (points.Count == 0)
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
                if (r.GetComponentInParent<Canvas>() == null) AjouterCoins(r.bounds, points);
        if (points.Count == 0) return;

        Vector3 cote = Vector3.Cross(Vector3.up, avantLocal).normalized;
        float minA = float.MaxValue, maxA = float.MinValue, minC = float.MaxValue, maxC = float.MinValue;
        Vector3 somme = Vector3.zero;
        foreach (var pw in points)
        {
            Vector3 p = transform.InverseTransformPoint(pw);
            float a = Vector3.Dot(p, avantLocal), s = Vector3.Dot(p, cote);
            minA = Mathf.Min(minA, a); maxA = Mathf.Max(maxA, a);
            minC = Mathf.Min(minC, s); maxC = Mathf.Max(maxC, s);
            somme += p;
        }
        float milieuC = (minC + maxC) * 0.5f;
        proueLocale = avantLocal * maxA + cote * milieuC;
        poupeLocale = avantLocal * minA + cote * milieuC;
        // largeur en mètres réels (le bateau peut être mis à l'échelle)
        largeur = Mathf.Max(0.5f, (maxC - minC) * transform.lossyScale.x);
    }

    static void AjouterCoins(Bounds b, List<Vector3> pts)
    {
        Vector3 c = b.center, e = b.extents;
        for (int i = 0; i < 8; i++)
            pts.Add(c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z));
    }

    ParticleSystem CreerSysteme(string nom, int max, ParticleSystemRenderMode mode)
    {
        var go = new GameObject(nom);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;   // l'écume reste sur l'eau quand le bateau avance
        main.scalingMode = ParticleSystemScalingMode.Local;          // ignore l'échelle 0.5 du bateau
        main.maxParticles = max;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = couleurEcume;

        var em = ps.emission;
        em.rateOverTime = 0f;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = mode;
        r.sharedMaterial = materiauEcume;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.sortingFudge = -20f;   // dessinée par-dessus l'eau

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ps.Play();
        return ps;
    }

    void ReglerProue(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(largeur * 0.35f, 0.01f, 0.6f);

        // L'écume part sur les côtés : forme un « V » derrière la proue
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-1.1f, 1.1f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0f);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2f));
    }

    void ReglerSillage(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 7f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(largeur * 0.6f, 0.01f, 0.8f);

        // Le sillage s'élargit lentement
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 3.2f));
    }

    void ReglerEclaboussures(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.gravityModifier = 1f;

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 40f;
        sh.radius = Mathf.Max(0.2f, largeur * 0.15f);
        sh.rotation = new Vector3(-60f, 0f, 0f);   // vers le haut et vers l'avant

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));
    }

    // ------------------------------------------------------------------ chaque image

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Vitesse réelle du bateau (le script Ordre le déplace dans Update)
        Vector3 deplacement = transform.position - dernierePos;
        dernierePos = transform.position;
        Vector3 avantMonde = transform.TransformDirection(avantLocal);
        avantMonde.y = 0f;
        if (avantMonde.sqrMagnitude < 0.0001f) return;
        avantMonde.Normalize();
        float v = Vector3.Dot(deplacement, avantMonde) / dt;
        if (deplacement.sqrMagnitude > 25f) v = 0f;   // téléportation : on ignore
        vitesse = Mathf.Lerp(vitesse, v, 1f - Mathf.Exp(-6f * dt));

        float f = Mathf.Clamp01(Mathf.Abs(vitesse) / Mathf.Max(0.05f, vitessePleine));
        if (f < 0.03f) f = 0f;
        bool enArriere = vitesse < 0f;

        // En marche arrière, l'« avant » qui fend l'eau devient la poupe
        Vector3 devant = transform.TransformPoint(enArriere ? poupeLocale : proueLocale);
        Vector3 derriere = transform.TransformPoint(enArriere ? proueLocale : poupeLocale);
        Vector3 sens = enArriere ? -avantMonde : avantMonde;
        Quaternion rot = Quaternion.LookRotation(sens, Vector3.up);
        float y = (eau != null ? eau.position.y : niveauEau) + 0.03f;

        Placer(psProue, devant, y, rot);
        Placer(psSillage, derriere, y, rot);
        if (psEclaboussures) Placer(psEclaboussures, devant, y, rot);

        Debit(psProue, 45f * f * intensite);
        Debit(psSillage, 30f * f * intensite);
        if (psEclaboussures) Debit(psEclaboussures, 40f * Mathf.Clamp01((f - 0.5f) / 0.5f) * intensite);

        if (audioEau != null)
        {
            audioEau.volume = Mathf.Lerp(audioEau.volume, volumeMax * f, 1f - Mathf.Exp(-3f * dt));
            audioEau.pitch = 0.85f + 0.3f * f;
        }
    }

    static void Placer(ParticleSystem ps, Vector3 p, float y, Quaternion rot)
    {
        if (ps == null) return;
        p.y = y;
        ps.transform.SetPositionAndRotation(p, rot);
    }

    static void Debit(ParticleSystem ps, float parSeconde)
    {
        if (ps == null) return;
        var em = ps.emission;
        em.rateOverTime = parSeconde;
    }

    // Petits repères dans la vue Scene : avant (vert), arrière (rouge)
    void OnDrawGizmosSelected()
    {
        float y = eau != null ? eau.position.y : niveauEau;
        Vector3 a = transform.TransformPoint(proueLocale); a.y = y;
        Vector3 b = transform.TransformPoint(poupeLocale); b.y = y;
        Gizmos.color = Color.green; Gizmos.DrawWireSphere(a, 0.4f);
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(b, 0.4f);
        Gizmos.color = Color.cyan; Gizmos.DrawLine(a, b);
    }
}
