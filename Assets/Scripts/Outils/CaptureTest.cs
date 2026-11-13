#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// OUTIL DE TEST (éditeur uniquement) : menu Viking > Test : captures automatiques.
/// Lance le jeu, donne des ordres au drakkar et enregistre des images dans le dossier "Captures" du projet.
/// </summary>
public class CaptureTest : MonoBehaviour
{
    static string Flag => Path.Combine(Application.dataPath, "../Temp/viking_capture.txt");
    static string Dossier => Path.Combine(Application.dataPath, "../Captures");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Demarrer()
    {
        if (!File.Exists(Flag)) return;
        File.Delete(Flag);
        new GameObject("CaptureTest").AddComponent<CaptureTest>();
    }

    Camera cam;
    RenderTexture rt;

    System.Text.StringBuilder journal = new System.Text.StringBuilder();
    void OnEnable() { Application.logMessageReceived += Noter; }
    void OnDisable() { Application.logMessageReceived -= Noter; }
    void Noter(string msg, string pile, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert || msg.StartsWith("[Test]") || msg.StartsWith("[Drakkar]"))
            journal.AppendLine($"[{type}] {msg}\n{(type == LogType.Log ? "" : pile)}");
    }

    IEnumerator Start()
    {
        Directory.CreateDirectory(Dossier);
        foreach (var f in Directory.GetFiles(Dossier, "*.png")) File.Delete(f);
        rt = new RenderTexture(1280, 720, 24);
        cam = new GameObject("CamTest").AddComponent<Camera>();
        cam.enabled = false;
        cam.fieldOfView = 60f;
        cam.farClipPlane = 3000f;
        yield return new WaitForSeconds(2f);

        var ordre = FindAnyObjectByType<Ordre>();
        var rune = FindAnyObjectByType<RuneInfo>();
        var origin = FindAnyObjectByType<XROrigin>();
        Transform b = ordre.transform;
        Vector3 av = ordre.Avant, dr = Vector3.Cross(Vector3.up, av).normalized;

        Capture("01_bateau_haut", b.position + Vector3.up * 13f - av * 16f + dr * 7f, b.position + Vector3.up * 1f);
        Capture("02_rameurs", b.position + dr * 6.5f + Vector3.up * 3.2f - av * 2f, b.position + Vector3.up * 1.6f - dr * 0.5f);
        Capture("02b_rameur_proche", b.position + dr * 3.2f + Vector3.up * 2.6f - av * 3.4f, b.position + dr * 1.3f + Vector3.up * 1.6f - av * 2.6f);

        if (rune)
        {
            rune.Toggle();
            yield return new WaitForSeconds(1f);
            Vector3 vers = origin ? origin.transform.position - rune.transform.position : Vector3.back;
            vers.y = 0; vers = vers.sqrMagnitude > 0.01f ? vers.normalized : Vector3.back;
            Vector3 c = rune.transform.position + vers * 3.2f + Vector3.up * 1.3f;
            Capture("03_rune_panneau", c, rune.transform.position + Vector3.up * rune.hauteurPanneau);
        }

        // le joueur monte sur le pont
        if (origin)
        {
            var cc = origin.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            origin.transform.position = b.position + Vector3.up * 1.4f - av * 6f;
            origin.transform.rotation = Quaternion.LookRotation(av);
            if (cc) cc.enabled = true;
        }
        yield return new WaitForSeconds(1f);
        var pont = FindAnyObjectByType<PanneauPont>();
        if (pont) Capture("04_panneau_pont", pont.transform.position - av * 3.5f + Vector3.up * 0.2f, pont.transform.position);

        ordre.BasculerMenu();
        yield return new WaitForSeconds(0.5f);
        var menu = GameObject.Find("Menu_Drakkar");
        if (menu) Capture("05_menu", menu.transform.position - menu.transform.forward * 1.3f + Vector3.up * 0.1f, menu.transform.position);

        Vector3 p0 = b.position;
        ordre.Avancer();
        yield return new WaitForSeconds(2.5f);
        Debug.Log($"[Test] apres Avancer 2.5s : deplacement {(b.position - p0).magnitude:0.00} m, manoeuvre {ordre.manoeuvre}, vitesse {ordre.Vitesse:0.00}");
        Capture("06_avancer_a", b.position + dr * 6.5f + Vector3.up * 3.2f - av * 2f, b.position + Vector3.up * 1.6f);
        yield return new WaitForSeconds(0.9f);
        Capture("06_avancer_b", b.position + dr * 6.5f + Vector3.up * 3.2f - av * 2f, b.position + Vector3.up * 1.6f);
        for (int k = 0; k < 4; k++)
        {
            Vector3 avk = ordre.Avant, drk = Vector3.Cross(Vector3.up, avk).normalized;
            Capture("06c_rameur_phase" + k, b.position + drk * 3.4f + Vector3.up * 2.4f + avk * 1.5f, b.position + drk * 1.3f + Vector3.up * 1.5f + avk * 3.2f);
            yield return new WaitForSeconds(0.5f);
        }
        Capture("06_voile", b.position + av * 14f + dr * 4f + Vector3.up * 6f, b.position + Vector3.up * 5f);
        ordre.TournerDroite();
        yield return new WaitForSeconds(2f);
        Capture("07_tribord", b.position + Vector3.up * 13f - ordre.Avant * 14f, b.position + Vector3.up * 1f);
        Debug.Log($"[Test] apres virage : deplacement total {(b.position - p0).magnitude:0.00} m, cap {ordre.Avant}");
        ordre.Stop();
        yield return new WaitForSeconds(3f);
        Capture("08_stop", b.position + Vector3.Cross(Vector3.up, ordre.Avant) * 6.5f + Vector3.up * 3.2f, b.position + Vector3.up * 1.6f);

        // Mouette
        var m = GameObject.Find("Mouette 1");
        if (m)
        {
            Capture("09_mouette_a", m.transform.position - m.transform.forward * 1.5f + m.transform.right * 2.5f + Vector3.up * 0.6f, m.transform.position);
            yield return new WaitForSeconds(0.12f);
            Capture("09_mouette_b", m.transform.position - m.transform.forward * 1.5f + m.transform.right * 2.5f + Vector3.up * 0.6f, m.transform.position);
        }

        // Boussole
        var bous = FindAnyObjectByType<CompassBodyLock>();
        if (bous) Capture("10_boussole", bous.transform.position + Vector3.up * 1.2f - Vector3.forward * 0.4f, bous.transform.position);

        // Ile + herbe + cocotiers (vue du joueur au départ)
        var ile = GameObject.Find("Island");
        if (ile)
        {
            Vector3 p = new Vector3(0.1f, 3.6f, 2.3f);
            Capture("11_ile", p, p + new Vector3(0.6f, -0.25f, 1f));
            Capture("11b_ile_haut", new Vector3(0, 60, -70), new Vector3(0, 0, 0));
        }

        // Nuit
        var cycle = FindAnyObjectByType<CycleJourNuit>();
        if (cycle)
        {
            typeof(CycleJourNuit).GetField("heure", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(cycle, 23.5f);
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[Test] nuit = {CycleJourNuit.Nuit:0.00}, heure = {CycleJourNuit.Heure:0.0}, ambiance = {RenderSettings.ambientLight}");
            Vector3 p = b.position + Vector3.up * 2.5f;
            Capture("12_nuit_nord", p, p + new Vector3(0, 0.75f, 1f));
            Capture("12b_nuit_bateau", b.position + Vector3.up * 8f - ordre.Avant * 12f, b.position + Vector3.up * 2f);
        }

        var terrain = Terrain.activeTerrain;
        if (terrain) Debug.Log($"[Test] terrain {terrain.name} details: proto={terrain.terrainData.detailPrototypes.Length} res={terrain.terrainData.detailResolution} dist={terrain.detailObjectDistance} draw={terrain.drawTreesAndFoliage}");
        File.WriteAllText(Path.Combine(Dossier, "journal.txt"), journal.ToString());
        Debug.Log("[CaptureTest] Captures enregistrées dans " + Path.GetFullPath(Dossier));
        UnityEditor.EditorApplication.isPlaying = false;
    }

    void Capture(string nom, Vector3 pos, Vector3 cible)
    {
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation(cible - pos, Vector3.up);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(Dossier, nom + ".png"), tex.EncodeToPNG());
        Destroy(tex);
    }
}
#endif
