using UnityEngine;

/// <summary>
/// Voile du drakkar en mouvement : la toile ondule en permanence et se gonfle quand le bateau avance.
/// Remplace le maillage de la voile par une grille plus fine (déformée chaque image) et lui donne
/// des rayures rouges et blanches typiques des voiles vikings.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class VoileAnimee : MonoBehaviour
{
    public Ordre ordre;
    [Tooltip("Subdivisions de la grille.")]
    public int resolution = 18;
    [Tooltip("Gonflement max (m) quand le drakkar avance à pleine vitesse.")]
    public float gonflementMax = 1.1f;
    [Tooltip("Gonflement au repos (m), léger vent.")]
    public float gonflementRepos = 0.3f;
    [Tooltip("Amplitude des ondulations de la toile (m).")]
    public float ondulation = 0.18f;
    public bool rayuresVikings = true;
    public Color rayure1 = new Color(0.62f, 0.10f, 0.07f);
    public Color rayure2 = new Color(0.93f, 0.88f, 0.76f);

    Mesh mesh;
    Vector3[] baseVerts, verts;
    Vector2[] uv;
    int axeU, axeV, axeN;         // axes locaux : largeur, hauteur, normale
    float signeN = 1f;
    bool vHautEnBas;
    float gonflement;
    int nbFace;
    float metreVersLocal = 1f;

    void Start()
    {
        if (ordre == null) ordre = GetComponentInParent<Ordre>();
        var mf = GetComponent<MeshFilter>();
        var src = mf.sharedMesh;
        if (src == null) { enabled = false; return; }

        Vector3[] sv = src.isReadable ? src.vertices : new Vector3[0];
        Bounds b = src.bounds;
        Vector3 s = b.size;
        axeN = s.x < s.y ? (s.x < s.z ? 0 : 2) : (s.y < s.z ? 1 : 2);
        axeU = (axeN + 1) % 3; axeV = (axeN + 2) % 3;
        // axeV = celui qui est le plus vertical dans le monde
        Vector3 du = transform.TransformDirection(Axe(axeU)), dv = transform.TransformDirection(Axe(axeV));
        if (Mathf.Abs(du.normalized.y) > Mathf.Abs(dv.normalized.y)) { int t = axeU; axeU = axeV; axeV = t; dv = du; }
        vHautEnBas = dv.y < 0f;     // v croît vers le bas ?
        // la toile doit se gonfler vers l'avant du bateau
        Vector3 avant = ordre ? ordre.Avant : Vector3.forward;
        signeN = Vector3.Dot(transform.TransformDirection(Axe(axeN)), avant) >= 0f ? 1f : -1f;
        float echelleN = transform.TransformVector(Axe(axeN)).magnitude;
        metreVersLocal = echelleN > 1e-6f ? 1f / echelleN : 1f;

        int n = Mathf.Clamp(resolution, 4, 40);
        nbFace = (n + 1) * (n + 1);
        baseVerts = new Vector3[nbFace * 2];
        verts = new Vector3[nbFace * 2];
        uv = new Vector2[nbFace * 2];
        for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n, v = j / (float)n;
                Vector3 p = Vector3.zero;
                p[axeU] = Mathf.Lerp(b.min[axeU], b.max[axeU], u);
                p[axeV] = Mathf.Lerp(b.min[axeV], b.max[axeV], v);
                p[axeN] = Interpoler(sv, p, b);
                int k = j * (n + 1) + i;
                baseVerts[k] = baseVerts[k + nbFace] = p;
                uv[k] = uv[k + nbFace] = new Vector2(u, v);
            }
        var tris = new int[n * n * 12];
        int q = 0;
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int a = j * (n + 1) + i, c = a + n + 1;
                // face avant
                tris[q++] = a; tris[q++] = c; tris[q++] = a + 1;
                tris[q++] = a + 1; tris[q++] = c; tris[q++] = c + 1;
                // face arrière (ordre inversé, sommets dupliqués pour avoir leurs propres normales)
                tris[q++] = a + nbFace; tris[q++] = a + 1 + nbFace; tris[q++] = c + nbFace;
                tris[q++] = a + 1 + nbFace; tris[q++] = c + 1 + nbFace; tris[q++] = c + nbFace;
            }
        mesh = new Mesh { name = "Voile_Animee" };
        mesh.MarkDynamic();
        mesh.vertices = baseVerts;
        mesh.uv = uv;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.bounds = new Bounds(b.center, b.size + Axe(axeN) * (gonflementMax * metreVersLocal * 4f));
        mf.sharedMesh = mesh;

        if (rayuresVikings) AppliquerRayures();
    }

    static Vector3 Axe(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

    float Interpoler(Vector3[] sv, Vector3 p, Bounds b)
    {
        // pondération inverse de la distance (dans le plan de la voile) sur les sommets d'origine
        float somme = 0f, poids = 0f;
        float echelle = Mathf.Max(b.size[axeU], b.size[axeV]) * 0.1f;
        foreach (var v in sv)
        {
            float du = (v[axeU] - p[axeU]) / echelle, dv = (v[axeV] - p[axeV]) / echelle;
            float d2 = du * du + dv * dv;
            float w = 1f / (d2 * d2 + 1e-4f);
            somme += v[axeN] * w; poids += w;
        }
        return poids > 0f ? somme / poids : b.center[axeN];   // maillage non lisible : voile plate au départ
    }

    void AppliquerRayures()
    {
        var rend = GetComponent<MeshRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (rend == null || shader == null) return;
        const int W = 256, H = 64;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "Voile_Rayures", wrapMode = TextureWrapMode.Clamp };
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int bande = Mathf.FloorToInt(x / (W / 7f));
                Color c = bande % 2 == 0 ? rayure1 : rayure2;
                float grain = Mathf.PerlinNoise(x * 0.3f, y * 0.9f) * 0.12f;
                c = Color.Lerp(c, c * 0.75f, grain);
                c.a = 1f;
                px[y * W + x] = c;
            }
        tex.SetPixels(px); tex.Apply(true);
        var mat = new Material(shader) { name = "Voile_Viking" };
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.15f);
        // les rayures doivent être verticales sur la voile : u = largeur
        rend.sharedMaterial = mat;
    }

    void Update()
    {
        if (mesh == null) return;
        float vitesse = ordre ? Mathf.Clamp01(Mathf.Abs(ordre.Vitesse)) : 0f;
        float cible = Mathf.Lerp(gonflementRepos, gonflementMax, vitesse);
        if (ordre && ordre.Vitesse < -0.05f) cible = gonflementRepos * 0.5f;   // en marche arrière la voile faseye
        gonflement = Mathf.Lerp(gonflement, cible, 1f - Mathf.Exp(-1.5f * Time.deltaTime));

        float t = Time.time;
        float faseyement = Mathf.Lerp(1f, 0.35f, vitesse);   // la toile flotte plus quand elle n'est pas tendue
        for (int k = 0; k < nbFace; k++)
        {
            Vector2 c = uv[k];
            float hauteur = vHautEnBas ? c.y : 1f - c.y;         // 0 = vergue (haut), 1 = bas de la voile
            float env = Mathf.Sin(Mathf.PI * c.x) * Mathf.Sin(Mathf.PI * (0.08f + 0.84f * hauteur));
            float vague = Mathf.Sin(c.x * 9f - t * 3.1f + hauteur * 4f) * 0.6f + Mathf.Sin(c.x * 4f + t * 1.7f + hauteur * 7f) * 0.4f;
            float dep = env * (gonflement + ondulation * faseyement * vague * hauteur) * signeN * metreVersLocal;
            Vector3 p = baseVerts[k];
            p[axeN] += dep;
            verts[k] = p;
            verts[k + nbFace] = p;
        }
        mesh.vertices = verts;
        mesh.RecalculateNormals();
    }
}
