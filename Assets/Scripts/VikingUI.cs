using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Boîte à outils pour fabriquer des panneaux de style viking en VR (bois sombre, cadre doré, frise de runes).
/// Toutes les textures sont générées par le code : rien à importer.
/// </summary>
public static class VikingUI
{
    // Palette viking
    public static readonly Color Or = new Color(0.93f, 0.76f, 0.38f);
    public static readonly Color OrSombre = new Color(0.55f, 0.40f, 0.16f);
    public static readonly Color Parchemin = new Color(0.96f, 0.91f, 0.78f);
    public static readonly Color BoisFonce = new Color(0.16f, 0.10f, 0.06f);
    public static readonly Color BoisClair = new Color(0.33f, 0.21f, 0.12f);
    public static readonly Color RougeViking = new Color(0.62f, 0.12f, 0.08f);

    static Sprite cadre, bouton, frise, separateur;

    // ------------------------------------------------------------------ Canvas

    /// <summary>Crée un Canvas World Space cliquable avec les rayons des manettes VR (et la souris).</summary>
    public static Canvas CreerCanvas(string nom, Transform parent, Vector2 taille, float echelleMonde)
    {
        var go = new GameObject(nom, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI") >= 0 ? LayerMask.NameToLayer("UI") : 0;
        go.transform.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = taille;
        // échelle : on veut "echelleMonde" mètre par unité, quelle que soit l'échelle du parent
        float parentScale = parent != null ? parent.lossyScale.x : 1f;
        if (Mathf.Abs(parentScale) < 1e-5f) parentScale = 1f;
        go.transform.localScale = Vector3.one * (echelleMonde / parentScale);
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 3f;
        go.AddComponent<GraphicRaycaster>();
        go.AddComponent<TrackedDeviceGraphicRaycaster>();
        go.AddComponent<CanvasGroup>();
        AssurerEventSystem();
        return canvas;
    }

    public static void AssurerEventSystem()
    {
        var es = Object.FindAnyObjectByType<EventSystem>();
        if (es == null)
        {
            es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
        }
        if (es.GetComponent<XRUIInputModule>() == null && es.GetComponent<BaseInputModule>() == null)
            es.gameObject.AddComponent<XRUIInputModule>();
    }

    // ------------------------------------------------------------------ éléments

    public static Image Fond(Transform parent, bool avecFrise = true)
    {
        var img = CreerImage("Fond", parent, CadreSprite());
        img.type = UnityEngine.UI.Image.Type.Sliced;
        Etirer(img.rectTransform, 0, 0, 0, 0);
        if (avecFrise)
        {
            var haut = CreerImage("Frise_Haut", img.transform, FriseSprite());
            haut.type = UnityEngine.UI.Image.Type.Tiled;
            haut.color = Or;
            var r = haut.rectTransform;
            r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
            r.pivot = new Vector2(0.5f, 1);
            r.offsetMin = new Vector2(46, -78); r.offsetMax = new Vector2(-46, -40);
            var bas = Object.Instantiate(haut.gameObject, img.transform).GetComponent<Image>();
            bas.name = "Frise_Bas";
            r = bas.rectTransform;
            r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0);
            r.pivot = new Vector2(0.5f, 0);
            r.offsetMin = new Vector2(46, 40); r.offsetMax = new Vector2(-46, 78);
        }
        return img;
    }

    public static Image CreerImage(string nom, Transform parent, Sprite sprite)
    {
        var go = new GameObject(nom, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    public static Image Separateur(Transform parent, float y, float marge = 120)
    {
        var img = CreerImage("Separateur", parent, SeparateurSprite());
        img.color = Or;
        var r = img.rectTransform;
        r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(marge, y - 9); r.offsetMax = new Vector2(-marge, y + 9);
        return img;
    }

    public static TextMeshProUGUI Texte(string nom, Transform parent, string texte, float taille, Color couleur,
                                        TextAlignmentOptions align = TextAlignmentOptions.Center, bool titre = false)
    {
        var go = new GameObject(nom, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = texte;
        t.fontSize = taille;
        t.color = couleur;
        t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        if (titre)
        {
            t.enableAutoSizing = true;
            t.fontSizeMax = taille;
            t.fontSizeMin = taille * 0.45f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            t.characterSpacing = 6;
            t.outlineWidth = 0.18f;
            t.outlineColor = new Color32(40, 22, 8, 255);
        }
        return t;
    }

    public static Button Bouton(string nom, Transform parent, string label, Vector2 pos, Vector2 taille,
                                UnityEngine.Events.UnityAction action, float tailleTexte = 34)
    {
        var img = CreerImage(nom, parent, BoutonSprite());
        img.type = UnityEngine.UI.Image.Type.Sliced;
        img.raycastTarget = true;
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.sizeDelta = taille;
        r.anchoredPosition = pos;
        var b = img.gameObject.AddComponent<Button>();
        var c = b.colors;
        c.normalColor = Color.white;
        c.highlightedColor = new Color(1f, 0.86f, 0.55f);
        c.pressedColor = new Color(0.85f, 0.55f, 0.25f);
        c.selectedColor = Color.white;
        c.fadeDuration = 0.08f;
        b.colors = c;
        if (action != null) b.onClick.AddListener(action);
        var t = Texte("Label", img.transform, label, tailleTexte, Parchemin, TextAlignmentOptions.Center, true);
        t.characterSpacing = 3;
        Etirer(t.rectTransform, 10, 6, 10, 6);
        return b;
    }

    public static void Etirer(RectTransform r, float gauche, float bas, float droite, float haut)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(gauche, bas); r.offsetMax = new Vector2(-droite, -haut);
    }

    // ------------------------------------------------------------------ sprites générés

    /// <summary>Panneau de bois sombre veiné, double cadre doré, clous aux coins (9-slice).</summary>
    public static Sprite CadreSprite()
    {
        if (cadre != null) return cadre;
        const int S = 256; const int B = 64;
        var tex = NouvelleTexture(S, S, "Viking_Cadre");
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                // veinage du bois (horizontal)
                float n = Mathf.PerlinNoise(x * 0.012f, y * 0.11f) * 0.6f + Mathf.PerlinNoise(x * 0.05f, y * 0.4f) * 0.4f;
                float veine = Mathf.Abs(Mathf.Sin((y * 0.22f + n * 9f)));
                Color c = Color.Lerp(BoisFonce, BoisClair, Mathf.Pow(veine, 3f) * 0.55f + n * 0.25f);
                c.a = 0.96f;
                int d = Mathf.Min(Mathf.Min(x, S - 1 - x), Mathf.Min(y, S - 1 - y));   // distance au bord
                if (d < 3) c = new Color(0.08f, 0.05f, 0.03f, 1f);
                else if (d < 9) c = Color.Lerp(OrSombre, Or, (d - 3) / 6f * 0.8f + 0.2f);
                else if (d < 13) c = Color.Lerp(c, Color.black, 0.45f);
                else if (d >= 18 && d < 21) c = Color.Lerp(c, Or, 0.85f);
                // clous aux 4 coins
                foreach (var coin in new[] { new Vector2(30, 30), new Vector2(S - 31, 30), new Vector2(30, S - 31), new Vector2(S - 31, S - 31) })
                {
                    float dist = Vector2.Distance(new Vector2(x, y), coin);
                    if (dist < 7f) c = Color.Lerp(Or, OrSombre, dist / 7f);
                    else if (dist < 8.5f) c = Color.Lerp(c, Color.black, 0.6f);
                }
                px[y * S + x] = c;
            }
        tex.SetPixels(px); tex.Apply(true);
        cadre = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(B, B, B, B));
        return cadre;
    }

    /// <summary>Planche de bouton : bois rouge-brun, liseré doré.</summary>
    public static Sprite BoutonSprite()
    {
        if (bouton != null) return bouton;
        const int W = 128, H = 64, B = 18;
        var tex = NouvelleTexture(W, H, "Viking_Bouton");
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.04f, y * 0.25f);
                Color c = Color.Lerp(new Color(0.30f, 0.12f, 0.07f), new Color(0.45f, 0.20f, 0.10f), n);
                c = Color.Lerp(c, Color.black, (1f - y / (float)H) * 0.25f);
                int d = Mathf.Min(Mathf.Min(x, W - 1 - x), Mathf.Min(y, H - 1 - y));
                if (d < 2) c = new Color(0.1f, 0.05f, 0.02f);
                else if (d < 6) c = Color.Lerp(OrSombre, Or, (d - 2) / 4f);
                c.a = 1f;
                px[y * W + x] = c;
            }
        tex.SetPixels(px); tex.Apply(true);
        bouton = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(B, B, B, B));
        return bouton;
    }

    /// <summary>Frise de runes (Futhark) dessinées au trait, à répéter horizontalement.</summary>
    public static Sprite FriseSprite()
    {
        if (frise != null) return frise;
        string[] ordre = { "F", "U", "TH", "A", "R", "K", "G", "W", "H", "N", "I", "J", "T", "B", "E", "M", "L" };
        const int cell = 40, H = 48;
        int W = cell * ordre.Length;
        var tex = NouvelleTexture(W, H, "Viking_Frise");
        var px = new Color[W * H];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(1, 1, 1, 0);
        // fines lignes haut / bas
        for (int x = 0; x < W; x++) { Pixel(px, W, H, x, 2, 1f); Pixel(px, W, H, x, 3, 1f); Pixel(px, W, H, x, H - 3, 1f); Pixel(px, W, H, x, H - 4, 1f); }
        for (int i = 0; i < ordre.Length; i++)
        {
            var traits = Runes[ordre[i]];
            foreach (var poly in traits)
                for (int k = 0; k < poly.Length - 1; k++)
                {
                    Vector2 a = new Vector2(i * cell + 8 + poly[k].x * (cell - 16), 9 + poly[k].y * (H - 18));
                    Vector2 b = new Vector2(i * cell + 8 + poly[k + 1].x * (cell - 16), 9 + poly[k + 1].y * (H - 18));
                    Ligne(px, W, H, a, b, 1.7f);
                }
        }
        tex.SetPixels(px); tex.Apply(true);
        tex.wrapMode = TextureWrapMode.Repeat;
        frise = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100);
        return frise;
    }

    /// <summary>Séparateur doré : trait avec losange central.</summary>
    public static Sprite SeparateurSprite()
    {
        if (separateur != null) return separateur;
        const int W = 512, H = 24;
        var tex = NouvelleTexture(W, H, "Viking_Separateur");
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float a = 0f;
                float fondu = Mathf.Clamp01(Mathf.Min(x, W - x) / 80f);
                if (Mathf.Abs(y - H / 2f) < 1.6f) a = fondu;
                float dx = Mathf.Abs(x - W / 2f), dy = Mathf.Abs(y - H / 2f);
                if (dx / 18f + dy / 10f < 1f) a = 1f;
                if (dx / 12f + dy / 6f < 1f) a = 0.35f;
                foreach (int cx in new[] { W / 2 - 40, W / 2 + 40 })
                    if (Vector2.Distance(new Vector2(x, y), new Vector2(cx, H / 2f)) < 3.5f) a = 1f;
                px[y * W + x] = new Color(1, 1, 1, a);
            }
        tex.SetPixels(px); tex.Apply(true);
        separateur = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100);
        return separateur;
    }

    static Texture2D NouvelleTexture(int w, int h, string nom)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, true)
        {
            name = nom, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4
        };
    }

    static void Pixel(Color[] px, int W, int H, int x, int y, float a)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        var c = px[y * W + x];
        c.a = Mathf.Max(c.a, a);
        px[y * W + x] = c;
    }

    static void Ligne(Color[] px, int W, int H, Vector2 a, Vector2 b, float epaisseur)
    {
        int minX = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - epaisseur - 1), maxX = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + epaisseur + 1);
        int minY = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - epaisseur - 1), maxY = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + epaisseur + 1);
        Vector2 ab = b - a; float l2 = Mathf.Max(ab.sqrMagnitude, 1e-4f);
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 p = new Vector2(x, y);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                float d = Vector2.Distance(p, a + ab * t);
                Pixel(px, W, H, x, y, Mathf.Clamp01(epaisseur + 0.5f - d));
            }
    }

    // Tracés des runes du Futhark ancien dans une case 0..1 (polylignes)
    static readonly Dictionary<string, Vector2[][]> Runes = new Dictionary<string, Vector2[][]>
    {
        { "F",  new[] { P(0.3f,0, 0.3f,1), P(0.3f,0.55f, 0.8f,0.8f), P(0.3f,0.8f, 0.75f,1f) } },
        { "U",  new[] { P(0.25f,0, 0.25f,1, 0.75f,0.7f, 0.75f,0) } },
        { "TH", new[] { P(0.3f,0, 0.3f,1), P(0.3f,0.72f, 0.72f,0.5f, 0.3f,0.28f) } },
        { "A",  new[] { P(0.3f,0, 0.3f,1), P(0.3f,1, 0.72f,0.78f), P(0.3f,0.72f, 0.72f,0.5f) } },
        { "R",  new[] { P(0.3f,0, 0.3f,1, 0.72f,0.78f, 0.3f,0.55f, 0.72f,0) } },
        { "K",  new[] { P(0.7f,0.85f, 0.3f,0.5f, 0.7f,0.15f) } },
        { "G",  new[] { P(0.2f,0, 0.8f,1), P(0.2f,1, 0.8f,0) } },
        { "W",  new[] { P(0.3f,0, 0.3f,1, 0.72f,0.8f, 0.3f,0.6f) } },
        { "H",  new[] { P(0.28f,0, 0.28f,1), P(0.72f,0, 0.72f,1), P(0.28f,0.62f, 0.72f,0.38f) } },
        { "N",  new[] { P(0.5f,0, 0.5f,1), P(0.28f,0.62f, 0.72f,0.38f) } },
        { "I",  new[] { P(0.5f,0, 0.5f,1) } },
        { "J",  new[] { P(0.48f,0.85f, 0.25f,0.62f, 0.48f,0.4f), P(0.52f,0.6f, 0.75f,0.38f, 0.52f,0.15f) } },
        { "T",  new[] { P(0.5f,0, 0.5f,1), P(0.22f,0.72f, 0.5f,1, 0.78f,0.72f) } },
        { "B",  new[] { P(0.3f,0, 0.3f,1, 0.72f,0.76f, 0.3f,0.5f, 0.72f,0.24f, 0.3f,0) } },
        { "E",  new[] { P(0.25f,0, 0.25f,1, 0.5f,0.7f, 0.75f,1, 0.75f,0) } },
        { "M",  new[] { P(0.25f,0, 0.25f,1, 0.75f,0.6f), P(0.75f,0, 0.75f,1, 0.25f,0.6f) } },
        { "L",  new[] { P(0.35f,0, 0.35f,1, 0.75f,0.75f) } },
    };

    static Vector2[] P(params float[] v)
    {
        var r = new Vector2[v.Length / 2];
        for (int i = 0; i < r.Length; i++) r[i] = new Vector2(v[i * 2], v[i * 2 + 1]);
        return r;
    }

    /// <summary>Fait pivoter un panneau autour de l'axe vertical pour qu'il regarde le joueur.</summary>
    public static void FaireFaceAuJoueur(Transform panneau, Transform tete, float vitesse = 6f, bool instantane = false)
    {
        if (panneau == null || tete == null) return;
        Vector3 look = panneau.position - tete.position;
        look.y = 0f;
        if (look.sqrMagnitude < 0.001f) return;
        var cible = Quaternion.LookRotation(look, Vector3.up);
        panneau.rotation = instantane ? cible : Quaternion.Slerp(panneau.rotation, cible, 1f - Mathf.Exp(-vitesse * Time.deltaTime));
    }
}
