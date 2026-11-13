using System;
using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

/// <summary>
/// Menu "Viking > Configurer la scène Hub" : installe tout dans la scène Hub, avec l'API d'Unity.
/// (16 rameurs assis avec leurs rames, cocotiers, herbe, collisions, téléportation, île du Nord,
///  panneau du pont, rune, boussole, cycle jour/nuit...). On peut le relancer sans risque : il refait proprement.
/// </summary>
[InitializeOnLoad]
public static class ConfigurationHub
{
    const string ScenePath = "Assets/Scenes/Hub.unity";
    const string VikingFbx = "Assets/Modeles/Viking/Viking_Rameur.fbx";
    const string CocotierFbx = "Assets/Modeles/Cocotier/Cocotier.fbx";
    const string RameBlend = "Assets/Blender/rame.blend";
    const string HerbeTex = "Assets/Textures/Herbe/Herbe_Touffe.png";
    const string PrefabViking = "Assets/Prefabs/Viking_Rameur.prefab";
    const string PrefabCocotier = "Assets/Prefabs/Cocotier.prefab";
    const string CleAuto = "ProjectV001_ConfigurationHub_v1";

    const float NiveauEau = 0.4f;
    const float EchelleViking = 0.95f;
    const float LongueurRame = 5.4f;
    const float RameInterieure = 1.0f;

    static readonly List<string> rapport = new List<string>();

    // Proposé une seule fois automatiquement, après la compilation des scripts
    static ConfigurationHub()
    {
        // Modifier un script pendant le mode Play provoquait des centaines d'erreurs (XR Toolkit, rune...) :
        // Unity attend maintenant la fin de la partie pour recompiler.
        if (EditorPrefs.GetInt("ScriptCompilationDuringPlay", 0) != 1) EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1);

        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool(CleAuto + Application.dataPath, false)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(VikingFbx) == null) return;
            EditorPrefs.SetBool(CleAuto + Application.dataPath, true);
            if (EditorUtility.DisplayDialog("Drakkar viking",
                "Installer maintenant les nouveautés dans la scène Hub ?\n\n(16 rameurs, cocotiers, herbe, collisions, île du Nord, boussole, rune...)\n\n" +
                "Tu pourras le relancer plus tard avec le menu  Viking > Configurer la scène Hub.", "Oui, installer", "Plus tard"))
                ToutConfigurer();
        };
    }

    [MenuItem("Viking/Configurer la scène Hub (tout)", priority = 0)]
    public static void ToutConfigurer()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Drakkar viking", "Quitte d'abord le mode Play.", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        rapport.Clear();
        Random.InitState(1234);

        Etape("Réglages d'import", ReglerImports);
        Material matViking = null, matCoco = null, matRame = null;
        Etape("Matériaux", () => { matViking = MatViking(); matCoco = MatCocotier(); matRame = MatRame(); });
        GameObject prefabViking = null, prefabCoco = null;
        Etape("Prefab viking", () => prefabViking = CreerPrefabViking(matViking));
        Etape("Prefab cocotier", () => prefabCoco = CreerPrefabCocotier(matCoco));
        Etape("Île du Nord", DeplacerIleNord);
        Etape("Collisions du décor", AjouterColliders);
        Etape("Drakkar : rameurs, rames, panneau", () => ConfigurerDrakkar(prefabViking, matRame));
        Etape("Herbe", AjouterHerbe);
        Etape("Cocotiers", () => PlanterCocotiers(prefabCoco));
        Etape("Téléportation", AjouterTeleportation);
        Etape("Rune", ConfigurerRune);
        Etape("Boussole", ConfigurerBoussole);
        Etape("Jour / nuit", ConfigurerCiel);
        Etape("Joueur XR", ConfigurerJoueur);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        string msg = string.Join("\n", rapport);
        Debug.Log("[Viking] Configuration terminée :\n" + msg);
        EditorUtility.DisplayDialog("Drakkar viking", "Configuration terminée !\n\n" + msg, "OK");
    }

    [MenuItem("Viking/Test : captures automatiques", priority = 50)]
    static void CapturesTest()
    {
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Temp/viking_capture.txt"), "1");
        EditorApplication.isPlaying = true;
    }

    static void Etape(string nom, Action a)
    {
        try { a(); }
        catch (Exception e)
        {
            rapport.Add("⚠ " + nom + " : " + e.Message);
            Debug.LogException(e);
        }
    }

    // ------------------------------------------------------------------ imports & matériaux

    static void ReglerImports()
    {
        foreach (var n in new[] { "Assets/Modeles/Viking/Viking_Normal.png", "Assets/Modeles/Cocotier/Cocotier_Normal.png" })
        {
            var ti = AssetImporter.GetAtPath(n) as TextureImporter;
            if (ti && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
        }
        foreach (var n in new[] { "Assets/Modeles/Viking/Viking_MetalSmooth.png", "Assets/Modeles/Cocotier/Cocotier_MetalSmooth.png" })
        {
            var ti = AssetImporter.GetAtPath(n) as TextureImporter;
            if (ti && ti.sRGBTexture) { ti.sRGBTexture = false; ti.SaveAndReimport(); }
        }
        var herbe = AssetImporter.GetAtPath(HerbeTex) as TextureImporter;
        if (herbe && (!herbe.alphaIsTransparency || herbe.wrapMode != TextureWrapMode.Clamp))
        {
            herbe.alphaIsTransparency = true; herbe.wrapMode = TextureWrapMode.Clamp; herbe.SaveAndReimport();
        }
        var mi = AssetImporter.GetAtPath(VikingFbx) as ModelImporter;
        if (mi && (mi.animationType != ModelImporterAnimationType.Generic || mi.importAnimation || mi.materialImportMode != ModelImporterMaterialImportMode.None))
        {
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.SaveAndReimport();
        }
        // maillages déformés par le code (voile, ailes des mouettes) : lecture autorisée
        foreach (var chemin in new[] { "Assets/Blender/Bateau/Langskip_v2.fbx", "Assets/#NVJOB Boids/Simple Boids/Mesh/Bird Mesh.fbx" })
        {
            var mr = AssetImporter.GetAtPath(chemin) as ModelImporter;
            if (mr && !mr.isReadable) { mr.isReadable = true; mr.SaveAndReimport(); }
        }
        var mc = AssetImporter.GetAtPath(CocotierFbx) as ModelImporter;
        if (mc && (mc.materialImportMode != ModelImporterMaterialImportMode.None || mc.animationType != ModelImporterAnimationType.None))
        {
            mc.animationType = ModelImporterAnimationType.None;
            mc.importAnimation = false;
            mc.materialImportMode = ModelImporterMaterialImportMode.None;
            mc.SaveAndReimport();
        }
    }

    static void Dossier(string chemin)
    {
        if (AssetDatabase.IsValidFolder(chemin)) return;
        int i = chemin.LastIndexOf('/');
        Dossier(chemin.Substring(0, i));
        AssetDatabase.CreateFolder(chemin.Substring(0, i), chemin.Substring(i + 1));
    }

    static Material Mat(string chemin)
    {
        Dossier(chemin.Substring(0, chemin.LastIndexOf('/')));
        var m = AssetDatabase.LoadAssetAtPath<Material>(chemin);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, chemin);
        }
        return m;
    }

    static Material MatPBR(string chemin, string dossierTex, string nom)
    {
        var m = Mat(chemin);
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dossierTex}/{nom}_Diffuse.png");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dossierTex}/{nom}_Normal.png");
        var ms = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dossierTex}/{nom}_MetalSmooth.png");
        m.SetColor("_BaseColor", Color.white);
        if (albedo) m.SetTexture("_BaseMap", albedo);
        if (normal) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
        if (ms) { m.SetTexture("_MetallicGlossMap", ms); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", 0.8f); }
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material MatViking() => MatPBR("Assets/Materials/Viking/Viking_Mat.mat", "Assets/Modeles/Viking", "Viking");
    static Material MatCocotier() => MatPBR("Assets/Materials/Cocotier/Cocotier_Mat.mat", "Assets/Modeles/Cocotier", "Cocotier");

    static Material MatRame()
    {
        var m = Mat("Assets/Materials/Viking/Rame_Bois.mat");
        m.SetColor("_BaseColor", new Color(0.46f, 0.31f, 0.18f));
        m.SetFloat("_Smoothness", 0.25f);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------ prefabs

    static GameObject CreerPrefabViking(Material mat)
    {
        var modele = AssetDatabase.LoadAssetAtPath<GameObject>(VikingFbx);
        if (modele == null) throw new Exception("modèle introuvable : " + VikingFbx);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(modele);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        inst.name = "Viking_Rameur";
        inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
            if (r is SkinnedMeshRenderer smr) { smr.updateWhenOffscreen = false; smr.quality = SkinQuality.Bone2; }
        }
        var an = inst.GetComponent<Animator>();
        if (an) Object.DestroyImmediate(an);

        // collider (on ne traverse pas les rameurs)
        var b = new Bounds(inst.transform.position, Vector3.zero);
        foreach (var r in inst.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        var cap = inst.GetComponent<CapsuleCollider>();
        if (cap == null) cap = inst.AddComponent<CapsuleCollider>();
        Vector3 c = inst.transform.InverseTransformPoint(b.center);
        cap.center = new Vector3(0f, c.y, 0f);
        cap.height = b.size.y;
        cap.radius = 0.28f;
        cap.direction = 1;

        if (inst.GetComponent<VikingRameur>() == null) inst.AddComponent<VikingRameur>();
        Dossier("Assets/Prefabs");
        var prefab = PrefabUtility.SaveAsPrefabAsset(inst, PrefabViking);
        Object.DestroyImmediate(inst);
        return prefab;
    }

    static GameObject CreerPrefabCocotier(Material mat)
    {
        var modele = AssetDatabase.LoadAssetAtPath<GameObject>(CocotierFbx);
        if (modele == null) throw new Exception("modèle introuvable : " + CocotierFbx);
        // Racine vide bien droite (Y = haut) ; le modèle garde sa rotation d'import en enfant
        var racine = new GameObject("Cocotier");
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(modele);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        inst.name = "Modele";
        inst.transform.SetParent(racine.transform, false);
        foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();

        // Si le modèle est couché (plus long à l'horizontale qu'à la verticale), on le redresse
        var mf = inst.GetComponentInChildren<MeshFilter>();
        Vector3[] Sommets() => mf.sharedMesh.vertices.Select(v => racine.transform.InverseTransformPoint(mf.transform.TransformPoint(v))).ToArray();
        if (mf && mf.sharedMesh)
        {
            var vs = Sommets();
            Vector3 mn = new Vector3(vs.Min(v => v.x), vs.Min(v => v.y), vs.Min(v => v.z));
            Vector3 mx = new Vector3(vs.Max(v => v.x), vs.Max(v => v.y), vs.Max(v => v.z));
            Vector3 taille = mx - mn;
            if (taille.z > taille.y * 1.2f && taille.z >= taille.x)
            {
                // axe Z vers le haut : quel bout est le pied ? (le tronc est plus fin que les palmes)
                float largeurBas = vs.Where(v => v.z < mn.z + taille.z * 0.15f).Select(v => new Vector2(v.x, v.y)).DefaultIfEmpty().Max(v => v.magnitude);
                float largeurHaut = vs.Where(v => v.z > mx.z - taille.z * 0.15f).Select(v => new Vector2(v.x, v.y)).DefaultIfEmpty().Max(v => v.magnitude);
                inst.transform.rotation = Quaternion.FromToRotation(largeurBas < largeurHaut ? Vector3.forward : Vector3.back, Vector3.up) * inst.transform.rotation;
            }
            else if (taille.x > taille.y * 1.2f)
                inst.transform.rotation = Quaternion.FromToRotation(Vector3.right, Vector3.up) * inst.transform.rotation;
            // pose le pied du tronc à l'origine
            vs = Sommets();
            float minY = vs.Min(v => v.y), maxY = vs.Max(v => v.y);
            var bas = vs.Where(v => v.y < minY + (maxY - minY) * 0.12f).ToArray();
            Vector3 pied = new Vector3(bas.Average(v => v.x), minY, bas.Average(v => v.z));
            inst.transform.position -= pied;
        }
        float hauteur = 1.9f;
        if (mf && mf.sharedMesh)
        {
            var vs = Sommets();
            hauteur = vs.Max(v => v.y) - vs.Min(v => v.y);
        }
        var cap = racine.AddComponent<CapsuleCollider>();
        cap.center = Vector3.up * hauteur * 0.3f;
        cap.height = hauteur * 0.6f;
        cap.radius = hauteur * 0.05f;
        Dossier("Assets/Prefabs");
        var prefab = PrefabUtility.SaveAsPrefabAsset(racine, PrefabCocotier);
        Object.DestroyImmediate(racine);
        return prefab;
    }

    // ------------------------------------------------------------------ île du Nord

    static GameObject Trouver(string nom)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == nom && t.parent == null) return t.gameObject;
        return null;
    }

    static void DeplacerIleNord()
    {
        var ile = Trouver("Island");
        var ile2 = Trouver("Island (2)");
        var port1 = Trouver("Port (1)");
        if (ile == null || ile2 == null) throw new Exception("Island / Island (2) introuvables");
        Vector3 p = ile2.transform.position;
        if (p.x < 500f) { rapport.Add("Île du Nord déjà au nord (z=" + p.z.ToString("0") + ")"); return; }
        Vector3 cible = new Vector3(ile.transform.position.x, p.y, 880f);
        Vector3 delta = cible - p;
        Undo.RecordObject(ile2.transform, "Ile nord");
        ile2.transform.position += delta;
        if (port1) { Undo.RecordObject(port1.transform, "Port nord"); port1.transform.position += delta; }
        rapport.Add($"Île du Nord + Port (1) déplacés plein Nord (z ≈ {cible.z:0} m)");
    }

    // ------------------------------------------------------------------ collisions

    static void AjouterColliders()
    {
        int n = 0;
        var racines = new List<GameObject>();
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            string nom = go.name;
            if (nom.StartsWith("Port") || nom.StartsWith("Village") || nom.StartsWith("Rock"))
                racines.Add(go);
        }
        foreach (var racine in racines)
            foreach (var mf in racine.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var go = mf.gameObject;
                if (go.GetComponent<Collider>() != null) continue;
                if (go.name == "Plane" || go.name.StartsWith("Cylinder.001")) continue;   // voile / vergue : gérés par Ordre
                if (go.name.ToLower().Contains("rope")) continue;                          // cordages : on passe à travers
                var r = go.GetComponent<Renderer>();
                if (r == null || r.bounds.size.magnitude < 0.35f) continue;               // petits objets (bouteilles...)
                go.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                n++;
            }
        rapport.Add($"Collisions : {n} colliders ajoutés au décor (ports, village, rochers)");
    }

    // ------------------------------------------------------------------ drakkar

    static void ConfigurerDrakkar(GameObject prefabViking, Material matRame)
    {
        var ordre = Object.FindObjectsByType<Ordre>(FindObjectsInactive.Include).FirstOrDefault();
        if (ordre == null) throw new Exception("Drakkar (script Ordre) introuvable");
        if (prefabViking == null) throw new Exception("prefab viking manquant");
        var bateau = ordre.transform;

        // réglages du pilotage
        Undo.RecordObject(ordre, "Ordre");
        ordre.vitesseMax = 7f;
        ordre.vitesseRotation = 7f;
        ordre.acceleration = 0.35f;
        ordre.accelerationRotation = 1.2f;
        ordre.niveauEau = NiveauEau;
        var port1 = Trouver("Port (1)");
        if (port1) ordre.destination = port1.transform;
        var origin = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Exclude).FirstOrDefault();
        if (origin) { ordre.xrOrigin = origin.transform; if (origin.Camera) ordre.tete = origin.Camera.transform; }
        EditorUtility.SetDirty(ordre);
        PrefabUtility.RecordPrefabInstancePropertyModifications(ordre);

        // on repart de zéro
        var ancien = bateau.Find("Equipage");
        if (ancien) Object.DestroyImmediate(ancien.gameObject);
        var equipage = new GameObject("Equipage").transform;
        equipage.SetParent(bateau, false);
        equipage.localScale = Vector3.one / Mathf.Max(0.0001f, bateau.lossyScale.x);   // enfants à l'échelle 1 du monde

        Physics.SyncTransforms();
        var coque = bateau.GetComponentsInChildren<MeshCollider>(true).FirstOrDefault(c => c.name == "Cube");
        Vector3 avant = bateau.TransformDirection(ordre.directionAvant).normalized;

        // Bancs (coordonnées locales du drakkar, mesurées sur le modèle Langskip_v2)
        float[] zBanc = { 2.34f, 4.50f, 6.60f, 8.68f };
        float[] xBanc = { 2.75f, 2.75f, 2.675f, 2.555f };
        float[] xBord = { 4.26f, 4.26f, 4.17f, 3.98f };
        const float yBanc = 3.05f, yBord = 3.92f;

        Mesh rameMesh = AssetDatabase.LoadAllAssetsAtPath(RameBlend).OfType<Mesh>().OrderByDescending(m => m.vertexCount).FirstOrDefault();
        if (rameMesh == null) rameMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");

        int idx = 0;
        foreach (float cote in new[] { 1f, -1f })
            foreach (float sensZ in new[] { 1f, -1f })
                for (int i = 0; i < 4; i++)
                {
                    idx++;
                    Vector3 local = new Vector3(cote * xBanc[i], yBanc, sensZ * zBanc[i]);
                    Vector3 siege = bateau.TransformPoint(local);
                    if (coque && coque.Raycast(new Ray(siege + Vector3.up * 1.5f, Vector3.down), out RaycastHit hit, 3f)) siege = hit.point;

                    // --- viking
                    var v = (GameObject)PrefabUtility.InstantiatePrefab(prefabViking, equipage);
                    v.name = $"Rameur_{idx:00}";
                    v.transform.localScale = Vector3.one * EchelleViking;
                    v.transform.position = siege + Vector3.down * 0.01f;
                    v.transform.rotation = Quaternion.identity;
                    Vector3 face = Regard(v.transform);
                    v.transform.rotation = Quaternion.LookRotation(-avant, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(face, Vector3.up));

                    // --- tolet (pivot de la rame), sur le bord, un peu devant le rameur
                    var tolet = new GameObject($"Tolet_{idx:00}").transform;
                    tolet.SetParent(equipage, false);
                    tolet.position = bateau.TransformPoint(new Vector3(cote * xBord[i], yBord, sensZ * zBanc[i])) - avant * 0.35f;
                    Vector3 dehors = bateau.TransformDirection(new Vector3(cote, 0f, 0f));
                    dehors.y = 0f;
                    tolet.rotation = Quaternion.LookRotation(dehors.normalized, Vector3.up);
                    CreerRame(tolet, rameMesh, matRame);

                    var vr = v.GetComponent<VikingRameur>();
                    vr.ordre = ordre;
                    vr.tolet = tolet;
                    vr.longueurInterieure = RameInterieure;
                    vr.longueurExterieure = LongueurRame - RameInterieure;
                    vr.balayage = 22f;
                    vr.decalagePhase = Random.Range(-0.025f, 0.025f);
                }

        // Panneau du pont : "Cap au Nord"
        var mat = bateau.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Cylinder");
        Vector3 basMat = mat ? new Vector3(mat.position.x, bateau.TransformPoint(0, 2.34f, 0).y, mat.position.z) : bateau.TransformPoint(0, 2.34f, 0);
        var panneau = new GameObject("Panneau_Pont_Cap_Nord").transform;
        panneau.SetParent(equipage, false);
        panneau.position = basMat + Vector3.up * 1.75f - avant * 1.4f;
        panneau.rotation = Quaternion.LookRotation(avant, Vector3.up);
        panneau.gameObject.AddComponent<PanneauPont>();

        rapport.Add($"Drakkar : {idx} vikings assis sur les bancs avec leur rame, panneau « Cap au Nord » sur le pont, vitesse max 7 m/s");
    }

    /// <summary>Direction du regard du viking assis (des hanches vers les genoux), à plat.</summary>
    static Vector3 Regard(Transform viking)
    {
        Transform hips = null, shinL = null, shinR = null;
        foreach (var t in viking.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Hips") hips = t;
            else if (t.name == "Shin.L") shinL = t;
            else if (t.name == "Shin.R") shinR = t;
        }
        if (hips && shinL && shinR)
        {
            Vector3 d = (shinL.position + shinR.position) * 0.5f - hips.position;
            d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) return d.normalized;
        }
        return viking.forward;
    }

    /// <summary>Place la rame dans le tolet : pale vers l'extérieur (axe Z du tolet), poignée à l'intérieur.</summary>
    static void CreerRame(Transform tolet, Mesh mesh, Material mat)
    {
        var go = new GameObject("Rame");
        go.transform.SetParent(tolet, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;

        var vs = mesh.vertices;
        var b = mesh.bounds;
        int ax = b.size.x > b.size.y ? (b.size.x > b.size.z ? 0 : 2) : (b.size.y > b.size.z ? 1 : 2);
        int o1 = (ax + 1) % 3, o2 = (ax + 2) % 3;
        float min = b.min[ax], max = b.max[ax], len = Mathf.Max(1e-4f, max - min);
        float Etalement(Func<Vector3, bool> zone, int axe)
        {
            var sel = vs.Where(zone).ToArray();
            return sel.Length == 0 ? 0f : sel.Max(v => v[axe]) - sel.Min(v => v[axe]);
        }
        bool zoneMin(Vector3 v) => v[ax] < min + len * 0.15f;
        bool zoneMax(Vector3 v) => v[ax] > max - len * 0.15f;
        float sMin = Mathf.Max(Etalement(zoneMin, o1), Etalement(zoneMin, o2));
        float sMax = Mathf.Max(Etalement(zoneMax, o1), Etalement(zoneMax, o2));
        bool paleAuMax = sMax > sMin;
        Func<Vector3, bool> zonePale = paleAuMax ? (Func<Vector3, bool>)zoneMax : zoneMin;
        int axeLarge = Etalement(zonePale, o1) >= Etalement(zonePale, o2) ? o1 : o2;

        Vector3 A = Vector3.zero; A[ax] = paleAuMax ? 1f : -1f;
        Vector3 L = Vector3.zero; L[axeLarge] = 1f;
        var poignee = vs.Where(v => paleAuMax ? v[ax] < min + len * 0.02f : v[ax] > max - len * 0.02f).ToArray();
        Vector3 bout = poignee.Length > 0 ? new Vector3(poignee.Average(v => v.x), poignee.Average(v => v.y), poignee.Average(v => v.z)) : b.center;
        bout[ax] = paleAuMax ? min : max;

        float s = LongueurRame / len;
        Quaternion R = Quaternion.Inverse(Quaternion.LookRotation(A, L));
        go.transform.localScale = Vector3.one * s;
        go.transform.localRotation = R;
        go.transform.localPosition = new Vector3(0f, 0f, -RameInterieure) - R * (bout * s);
    }

    // ------------------------------------------------------------------ herbe & cocotiers

    static IEnumerable<Terrain> Iles()
    {
        foreach (var nom in new[] { "Island", "Island (2)", "Island (1)" })
        {
            var go = Trouver(nom);
            var t = go ? go.GetComponent<Terrain>() : null;
            if (t) yield return t;
        }
    }

    static void AjouterHerbe()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(HerbeTex);
        var terrain = Iles().FirstOrDefault();
        if (tex == null || terrain == null) throw new Exception("texture d'herbe ou terrain introuvable");
        var td = terrain.terrainData;
        Undo.RecordObject(td, "Herbe");
        var protos = td.detailPrototypes.ToList();
        int index = protos.FindIndex(p => p.prototypeTexture == tex);
        if (index < 0)
        {
            protos.Add(new DetailPrototype
            {
                prototypeTexture = tex,
                usePrototypeMesh = false,
                renderMode = DetailRenderMode.Grass,
                healthyColor = new Color(0.55f, 0.85f, 0.35f),
                dryColor = new Color(0.78f, 0.75f, 0.42f),
                minWidth = 0.5f, maxWidth = 0.9f,
                minHeight = 0.25f, maxHeight = 0.5f,
                noiseSpread = 0.35f,
            });
            td.detailPrototypes = protos.ToArray();
            index = protos.Count - 1;
        }
        else
        {
            protos[index].minWidth = 0.5f; protos[index].maxWidth = 0.9f;
            protos[index].minHeight = 0.25f; protos[index].maxHeight = 0.5f;
            td.detailPrototypes = protos.ToArray();
        }
        // mode "nombre d'instances" : chaque case contient un nombre précis de touffes
        td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
        int res = Mathf.Clamp(Mathf.RoundToInt(td.size.x / 0.5f), 128, 512);   // cases d'environ 50 cm
        td.SetDetailResolution(res, 16);
        var carte = new int[res, res];
        float baseY = terrain.transform.position.y;
        float aireCase = (td.size.x / res) * (td.size.z / res);
        const float touffesParM2 = 0.9f;   // herbe clairsemée, légère pour un casque autonome (Quest)
        int cellules = 0;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float nx = (x + 0.5f) / res, ny = (y + 0.5f) / res;
                float h = baseY + td.GetInterpolatedHeight(nx, ny);
                float pente = td.GetSteepness(nx, ny);
                if (h < NiveauEau + 1.3f || pente > 32f) continue;   // pas d'herbe sur la plage ni les falaises
                float bruit = Mathf.PerlinNoise(x * 0.06f + 13.1f, y * 0.06f + 7.7f);
                if (bruit < 0.42f) continue;                           // des zones sans herbe
                float voulu = touffesParM2 * aireCase * Mathf.Lerp(0.4f, 1.6f, (bruit - 0.42f) / 0.58f);
                int n = Mathf.FloorToInt(voulu);
                if (Random.value < voulu - n) n++;
                carte[y, x] = n;
                if (n > 0) cellules++;
            }
        td.SetDetailLayer(0, 0, index, carte);
        // l'herbe ondule à peine (vent très léger)
        td.wavingGrassAmount = 0.04f;
        td.wavingGrassStrength = 0.08f;
        td.wavingGrassSpeed = 0.15f;
        EditorUtility.SetDirty(td);
        foreach (var t in Iles())
        {
            Undo.RecordObject(t, "Herbe");
            t.drawTreesAndFoliage = true;
            t.detailObjectDistance = 35f;
            t.detailObjectDensity = 0.8f;
            EditorUtility.SetDirty(t);
        }
        rapport.Add($"Herbe : touffes d'herbe peintes sur {cellules} cellules du terrain (les 3 îles partagent le même terrain)");
    }

    static void PlanterCocotiers(GameObject prefab)
    {
        if (prefab == null) throw new Exception("prefab cocotier manquant");
        int total = 0;
        var depart = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Exclude).FirstOrDefault();
        var ordre = Object.FindAnyObjectByType<Ordre>();
        Physics.SyncTransforms();
        foreach (var terrain in Iles())
        {
            if (terrain.name == "Island (1)") continue;
            var ancien = terrain.transform.Find("Cocotiers");
            if (ancien) Object.DestroyImmediate(ancien.gameObject);
            var parent = new GameObject("Cocotiers").transform;
            parent.SetParent(terrain.transform, false);

            var td = terrain.terrainData;
            Vector3 o = terrain.transform.position;
            var places = new List<Vector3>();
            int voulu = terrain.name == "Island" ? 24 : 20;
            for (int essai = 0; essai < 6000 && places.Count < voulu; essai++)
            {
                float nx = Random.Range(0.03f, 0.97f), nz = Random.Range(0.03f, 0.97f);
                float h = o.y + td.GetInterpolatedHeight(nx, nz);
                if (h < NiveauEau + 0.7f || h > NiveauEau + 9f) continue;
                if (td.GetSteepness(nx, nz) > 24f) continue;
                Vector3 p = new Vector3(o.x + nx * td.size.x, h, o.z + nz * td.size.z);
                if (places.Any(q => (q - p).sqrMagnitude < 8f * 8f)) continue;
                if (depart && Plat(depart.transform.position - p) < 13f) continue;
                if (ordre && Plat(ordre.transform.position - p) < 16f) continue;
                // rien d'autre autour (port, rochers, rune...)
                bool libre = true;
                foreach (var c in Physics.OverlapSphere(p + Vector3.up * 1.5f, 3.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    if (!(c is TerrainCollider)) { libre = false; break; }
                if (!libre) continue;
                places.Add(p);
            }
            foreach (var p in places)
            {
                var c = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                c.transform.position = p - Vector3.up * 0.1f;
                c.transform.rotation = Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(0f, 360f), Random.Range(-3f, 3f));
                c.transform.localScale = Vector3.one * Random.Range(2.6f, 3.8f);
                total++;
            }
        }
        rapport.Add($"Cocotiers : {total} plantés (île de départ + île du Nord)");
    }

    static float Plat(Vector3 v) { v.y = 0f; return v.magnitude; }

    // ------------------------------------------------------------------ téléportation

    static void AjouterTeleportation()
    {
        int n = 0;
        int masque = InteractionLayerMask.GetMask("Teleport");
        void Zone(Collider col)
        {
            if (col == null) return;
            var ta = col.GetComponent<TeleportationArea>();
            if (ta == null) { ta = col.gameObject.AddComponent<TeleportationArea>(); n++; }
            if (masque != 0) ta.interactionLayers = masque;
            if (!ta.colliders.Contains(col)) ta.colliders.Add(col);
            EditorUtility.SetDirty(ta);
        }
        foreach (var t in Iles()) Zone(t.GetComponent<TerrainCollider>());
        var ordre = Object.FindAnyObjectByType<Ordre>();
        if (ordre) Zone(ordre.GetComponentsInChildren<MeshCollider>(true).FirstOrDefault(c => c.name == "Cube"));
        foreach (var nom in new[] { "Port", "Port (1)" })
        {
            var p = Trouver(nom);
            if (p == null) continue;
            foreach (var col in p.GetComponentsInChildren<MeshCollider>(true))
                if (col.name.ToLower().Contains("dock") || (col.transform.parent != null && col.transform.parent.name.ToLower().Contains("dock"))) Zone(col);
        }
        rapport.Add($"Téléportation : {n} zones ajoutées (îles, pont du drakkar, pontons)");
    }

    // ------------------------------------------------------------------ rune, boussole, ciel, joueur

    static void ConfigurerRune()
    {
        var rune = Object.FindObjectsByType<RuneInfo>(FindObjectsInactive.Include).FirstOrDefault();
        if (rune == null) throw new Exception("RuneInfo introuvable");
        Undo.RecordObject(rune, "Rune");
        var ancien = rune.panel;
        rune.pages = RuneInfo.DefaultPages();
        rune.styleViking = true;
        rune.panel = null; rune.titleText = null; rune.bodyText = null; rune.pageNumberText = null;
        rune.hideDistance = Mathf.Max(rune.hideDistance, 6f);
        // panneau au-dessus de la pierre
        var pierre = rune.transform.parent;
        if (pierre)
        {
            var b = new Bounds(pierre.position, Vector3.zero);
            foreach (var r in pierre.GetComponentsInChildren<Renderer>()) if (r.transform != rune.transform) b.Encapsulate(r.bounds);
            rune.hauteurPanneau = Mathf.Max(1.0f, b.max.y + 0.95f - rune.transform.position.y);
        }
        EditorUtility.SetDirty(rune);
        PrefabUtility.RecordPrefabInstancePropertyModifications(rune);
        if (ancien) { Undo.RecordObject(ancien, "Ancien panneau"); ancien.SetActive(false); }
        // la texture de la rune doit être lisible pour que les runes s'illuminent
        var rr = rune.GetComponent<Renderer>();
        var texRune = rr && rr.sharedMaterial && rr.sharedMaterial.HasProperty("_BaseMap") ? rr.sharedMaterial.GetTexture("_BaseMap") : null;
        var tiRune = texRune ? AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texRune)) as TextureImporter : null;
        if (tiRune && !tiRune.isReadable) { tiRune.isReadable = true; tiRune.SaveAndReimport(); }
        var p2 = Trouver("Panneau_Rune");
        if (p2) p2.SetActive(false);
        rapport.Add("Rune : panneau viking + 8 pages (histoire du drakkar et navigation) — texte modifiable en cliquant sur l'objet Rune");
    }

    static void ConfigurerBoussole()
    {
        var aiguille = Object.FindObjectsByType<CompassNeedle>(FindObjectsInactive.Include).FirstOrDefault();
        if (aiguille)
        {
            Undo.RecordObject(aiguille, "Aiguille");
            aiguille.angleOffset = 0f;
            aiguille.target = null;
            aiguille.axePointe = new Vector3(-1f, 0f, 0f);
            aiguille.axeCadran = new Vector3(0f, 0f, 1f);
            EditorUtility.SetDirty(aiguille);
        }
        var corps = Object.FindObjectsByType<CompassBodyLock>(FindObjectsInactive.Include).FirstOrDefault();
        if (corps)
        {
            corps.niveauEau = NiveauEau;
            EditorUtility.SetDirty(corps);
            var grab = corps.GetComponent<XRGrabInteractable>();
            if (grab)
            {
                // l'aiguille n'est PAS le visuel de l'objet attrapé (c'était la cause du bug)
                var so = new SerializedObject(grab);
                var p = so.FindProperty("m_PredictedVisualsTransform");
                if (p != null) { p.objectReferenceValue = null; so.ApplyModifiedProperties(); }
            }
        }
        rapport.Add("Boussole : l'aiguille tourne dans le cadran et montre le Nord, prise en main libre, retour automatique si perdue");
    }

    static void ConfigurerCiel()
    {
        var cycle = Object.FindAnyObjectByType<CycleJourNuit>();
        if (cycle == null) throw new Exception("CycleJourNuit introuvable");
        Undo.RecordObject(cycle, "Cycle");
        cycle.dureeJournee = 360f;
        cycle.heureDepart = 13f;
        cycle.courbeIntensite = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.22f, 0f), new Keyframe(0.32f, 1f),
            new Keyframe(0.72f, 1f), new Keyframe(0.82f, 0f), new Keyframe(1f, 0f));
        var ciel = Trouver("Ciel_Etoiles");
        if (ciel) cycle.cielEtoiles = ciel.transform;
        EditorUtility.SetDirty(cycle);
        rapport.Add("Jour/nuit : départ à 13 h, journée de 6 min : la nuit tombe pendant la traversée (étoiles + Étoile polaire)");
    }

    static void ConfigurerJoueur()
    {
        var origin = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Exclude).FirstOrDefault();
        if (origin == null) throw new Exception("XR Origin actif introuvable");
        var go = origin.gameObject;
        var cc = go.GetComponent<CharacterController>();
        if (cc == null)
        {
            cc = go.AddComponent<CharacterController>();
            cc.height = 1.7f; cc.radius = 0.2f; cc.center = new Vector3(0, 0.85f, 0); cc.stepOffset = 0.3f;
        }
        if (go.GetComponent<TeteAntiTraversee>() == null) go.AddComponent<TeteAntiTraversee>();
        // les déplacements passent par le CharacterController (collisions)
        foreach (var comp in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (comp == null) continue;
            var so = new SerializedObject(comp);
            var p = so.FindProperty("m_UseCharacterControllerIfExists");
            if (p != null && !p.boolValue) { p.boolValue = true; so.ApplyModifiedProperties(); }
        }
        if (origin.Camera)
        {
            Undo.RecordObject(origin.Camera, "Camera");
            origin.Camera.farClipPlane = Mathf.Max(origin.Camera.farClipPlane, 1500f);
            EditorUtility.SetDirty(origin.Camera);
        }
        EditorUtility.SetDirty(go);
        rapport.Add("Joueur : CharacterController + anti-traversée de la tête + sécurité chute à l'eau, vision à 1500 m");
    }
}
