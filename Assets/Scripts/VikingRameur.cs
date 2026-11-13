using UnityEngine;

/// <summary>
/// Un viking assis sur un banc du drakkar, qui tient sa rame.
/// - La rame pivote sur son tolet (au bord de la coque) : coup de rame, dénage, ou rame levée à l'arrêt.
/// - Le buste se penche avec le mouvement, les mains suivent la poignée de la rame (IK à 2 os).
/// L'animation dépend de l'ordre donné au drakkar (script Ordre).
/// Os attendus (modèle Viking_Rameur) : Hips, Spine, Chest, Neck, Head, UpperArm.L/R, ForeArm.L/R, Hand.L/R.
/// </summary>
[DefaultExecutionOrder(50)]
public class VikingRameur : MonoBehaviour
{
    [Header("Liens")]
    public Ordre ordre;
    [Tooltip("Pivot de la rame (tolet). Son axe Z bleu pointe vers l'extérieur du bateau, Y vers le haut.")]
    public Transform tolet;
    [Tooltip("Longueur de la rame à l'intérieur du bateau, entre la poignée et le tolet (m).")]
    public float longueurInterieure = 1.0f;
    [Tooltip("Longueur de la rame à l'extérieur, du tolet au bout de la pale (m).")]
    public float longueurExterieure = 4.2f;
    [Tooltip("Décalage de phase de ce rameur (0 = parfaitement synchronisé).")]
    public float decalagePhase = 0f;

    [Header("Mouvement de rame")]
    public float balayage = 28f;         // amplitude du coup de rame (degrés, de chaque côté)
    public float levee = 9f;             // la pale sort de l'eau pendant le retour (degrés)
    public float inclinaisonRepos = 12f; // à l'arrêt, rame levée au-dessus de l'eau
    public float penchementBuste = 22f;      // amplitude du balancement du buste

    // os
    Transform hips, spine, chest, neck, head;
    Transform upperL, foreL, handL, upperR, foreR, handR;
    Quaternion[] restLocal;
    Transform[] os;
    Quaternion toletRepos;
    float tanguageBase;            // inclinaison qui met la pale dans l'eau
    float cote;                    // +1 tribord, -1 bâbord
    float signeLacet;              // pour que +lacet amène la pale vers l'avant du bateau
    float amount;                  // 0 = repos, 1 = rame
    float sensLisse;
    bool mainGaucheExterieure;
    float respiration;
    Vector3 droiteLocale = Vector3.right;

    void Start()
    {
        if (ordre == null) ordre = GetComponentInParent<Ordre>();
        hips = Os("Hips"); spine = Os("Spine"); chest = Os("Chest"); neck = Os("Neck"); head = Os("Head");
        upperL = Os("UpperArm.L"); foreL = Os("ForeArm.L"); handL = Os("Hand.L");
        upperR = Os("UpperArm.R"); foreR = Os("ForeArm.R"); handR = Os("Hand.R");
        os = new[] { hips, spine, chest, neck, head, upperL, foreL, handL, upperR, foreR, handR };
        restLocal = new Quaternion[os.Length];
        for (int i = 0; i < os.Length; i++) if (os[i]) restLocal[i] = os[i].localRotation;

        respiration = Random.Range(0f, 10f);
        // axe "droite" du personnage (de l'épaule gauche vers l'épaule droite)
        if (upperL && upperR) droiteLocale = transform.InverseTransformDirection((upperR.position - upperL.position).normalized);

        if (tolet != null)
        {
            toletRepos = tolet.localRotation;
            Transform bateau = ordre ? ordre.transform : transform.parent;
            cote = Mathf.Sign(bateau.InverseTransformPoint(tolet.position).x);
            Vector3 avant = ordre ? ordre.Avant : bateau.forward;
            signeLacet = Vector3.Dot(tolet.right, avant) > 0 ? 1f : -1f;
            // Inclinaison pour que la pale touche l'eau
            float eau = ordre ? ordre.niveauEau : 0.4f;
            float h = tolet.position.y - (eau - 0.15f);
            tanguageBase = Mathf.Asin(Mathf.Clamp(h / Mathf.Max(0.5f, longueurExterieure), -0.9f, 0.9f)) * Mathf.Rad2Deg;
            if (handL && handR)
                mainGaucheExterieure = Vector3.Dot(handL.position - handR.position, tolet.position - transform.position) > 0f;
        }
    }

    Transform Os(string nom)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == nom) return t;
        return null;
    }

    void LateUpdate()
    {
        if (os == null) return;
        float dt = Time.deltaTime;

        // 1) remettre la pose assise de repos
        for (int i = 0; i < os.Length; i++) if (os[i]) os[i].localRotation = restLocal[i];

        int sens = ordre ? ordre.SensRame(cote) : 0;
        amount = Mathf.MoveTowards(amount, sens != 0 ? 1f : 0f, dt * 1.5f);
        if (sens != 0) sensLisse = Mathf.MoveTowards(sensLisse, sens, dt * 2f);
        float phase = Mathf.Repeat((ordre ? ordre.PhaseRame : Time.time * 0.3f) + decalagePhase, 1f);

        // 2) cycle de rame : 0..0.55 = pale dans l'eau (passage), 0.55..1 = retour pale en l'air
        float lacet, tangage, buste;
        const float passage = 0.55f;
        if (phase < passage)
        {
            float t = Mathf.SmoothStep(0f, 1f, phase / passage);
            lacet = Mathf.Lerp(balayage, -balayage, t);    // la pale va de l'avant vers l'arrière du bateau
            tangage = tanguageBase;
            buste = Mathf.Lerp(1f, -0.5f, t);                // penché en avant à l'attaque, en arrière à la fin
        }
        else
        {
            float t = Mathf.SmoothStep(0f, 1f, (phase - passage) / (1f - passage));
            lacet = Mathf.Lerp(-balayage, balayage, t);
            tangage = tanguageBase - levee * Mathf.Sin(t * Mathf.PI) - levee * 0.3f;
            buste = Mathf.Lerp(-0.5f, 1f, t);
        }
        if (sensLisse < 0f) { lacet = -lacet; buste = -buste * 0.6f; }  // dénage : mouvement inversé

        // repos : rame levée, horizontale au-dessus de l'eau, petite respiration
        respiration += dt;
        float souffle = Mathf.Sin(respiration * 1.6f) * 0.06f;
        float tangageRepos = -inclinaisonRepos;
        lacet = Mathf.Lerp(0f, lacet, amount);
        tangage = Mathf.Lerp(tangageRepos, tangage, amount);
        buste = Mathf.Lerp(souffle, buste, amount);

        if (tolet)
            tolet.localRotation = toletRepos * Quaternion.Euler(0f, lacet * signeLacet, 0f) * Quaternion.Euler(tangage, 0f, 0f);

        // 3) buste
        Vector3 droite = transform.TransformDirection(droiteLocale);
        if (spine) spine.rotation = Quaternion.AngleAxis(buste * penchementBuste * 0.55f, droite) * spine.rotation;
        if (chest) chest.rotation = Quaternion.AngleAxis(buste * penchementBuste * 0.45f, droite) * chest.rotation;
        if (neck) neck.rotation = Quaternion.AngleAxis(-buste * penchementBuste * 0.6f, droite) * neck.rotation;

        // 4) mains sur la poignée
        if (tolet)
        {
            Vector3 axe = tolet.forward;               // du tolet vers la pale
            Vector3 bout = tolet.position - axe * longueurInterieure;
            Vector3 prise1 = bout + axe * 0.10f;       // main intérieure, au bout de la poignée
            Vector3 prise2 = bout + axe * 0.45f;       // main extérieure, plus près du bord
            Vector3 haut = Vector3.up * 0.03f;
            if (mainGaucheExterieure) { IK(upperL, foreL, handL, prise2 + haut); IK(upperR, foreR, handR, prise1 + haut); }
            else { IK(upperR, foreR, handR, prise2 + haut); IK(upperL, foreL, handL, prise1 + haut); }
        }
    }

    /// <summary>IK analytique à deux os (épaule-coude-poignet), indépendante des axes des os.</summary>
    static void IK(Transform a, Transform b, Transform c, Vector3 cible)
    {
        if (!a || !b || !c) return;
        Vector3 pa = a.position, pb = b.position, pc = c.position;
        float lab = (pb - pa).magnitude, lcb = (pb - pc).magnitude;
        float lat = Mathf.Clamp((cible - pa).magnitude, 0.01f, (lab + lcb) * 0.999f);

        float ac_ab_0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((pc - pa).normalized, (pb - pa).normalized), -1f, 1f));
        float ba_bc_0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((pa - pb).normalized, (pc - pb).normalized), -1f, 1f));
        float ac_at_0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((pc - pa).normalized, (cible - pa).normalized), -1f, 1f));
        float ac_ab_1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
        float ba_bc_1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));

        Vector3 axe0 = Vector3.Cross(pc - pa, pb - pa);
        if (axe0.sqrMagnitude < 1e-8f) axe0 = Vector3.Cross(pc - pa, Vector3.down);
        axe0.Normalize();
        Vector3 axe1 = Vector3.Cross(pc - pa, cible - pa);
        if (axe1.sqrMagnitude < 1e-8f) axe1 = axe0; else axe1.Normalize();

        Quaternion ag = a.rotation, bg = b.rotation;
        a.localRotation = a.localRotation * Quaternion.AngleAxis((ac_ab_1 - ac_ab_0) * Mathf.Rad2Deg, Quaternion.Inverse(ag) * axe0);
        b.localRotation = b.localRotation * Quaternion.AngleAxis((ba_bc_1 - ba_bc_0) * Mathf.Rad2Deg, Quaternion.Inverse(bg) * axe0);
        a.localRotation = a.localRotation * Quaternion.AngleAxis(ac_at_0 * Mathf.Rad2Deg, Quaternion.Inverse(ag) * axe1);
    }
}
