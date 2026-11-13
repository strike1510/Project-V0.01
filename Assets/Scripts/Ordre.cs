using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Pilotage du drakkar (sur la racine du Langskip).
/// - Monte à bord puis vise la VOILE (ou le mât) et appuie sur la gâchette : le menu de commandes s'ouvre.
/// - Avancer / Reculer / Bâbord (gauche) / Tribord (droite) / Stop.
/// - Les 16 rameurs (VikingRameur) changent d'animation selon l'ordre, la voile se gonfle avec la vitesse.
/// - Le drakkar s'arrête s'il touche la terre ; un message annonce l'arrivée sur l'île du Nord.
/// </summary>
public class Ordre : MonoBehaviour
{
    public enum Manoeuvre { Stop, Avancer, Reculer, Gauche, Droite }

    [Header("Joueur (trouvés automatiquement si vides)")]
    public Transform xrOrigin;
    public Transform tete;
    public BoxCollider zonePont;

    [Header("Menu")]
    [Tooltip("Ancien menu (caché : un menu viking est généré automatiquement).")]
    public GameObject menu;
    public float distanceMenu = 1.1f;
    public float hauteurMenu = -0.15f;

    [Header("Mouvement")]
    public Vector3 directionAvant = Vector3.forward;
    [Tooltip("Vitesse max en m/s (7 m/s ≈ 13 nœuds, un drakkar rapide).")]
    public float vitesseMax = 7f;
    [Tooltip("Vitesse de rotation max (degrés/s).")]
    public float vitesseRotation = 7f;
    public float acceleration = 0.35f;
    public float accelerationRotation = 1.2f;

    [Header("Mer et terre")]
    public float niveauEau = 0.4f;
    [Tooltip("Le port d'arrivée (île du Nord).")]
    public Transform destination;
    public float rayonArrivee = 90f;

    [Header("Rames (cadence en coups par seconde)")]
    public float cadenceAvant = 0.5f;
    public float cadenceArriere = 0.4f;
    public float cadenceVirage = 0.45f;

    // ---- état lisible par les autres scripts
    public Manoeuvre manoeuvre { get; private set; } = Manoeuvre.Stop;
    public float PhaseRame { get; private set; }
    public float Vitesse => vitesse;               // -0.5 .. 1
    public float VitesseMs => vitesse * vitesseMax;
    public bool JoueurABord => surBateau;
    public bool Arrive { get; private set; }
    public Vector3 Avant => transform.TransformDirection(directionAvant).normalized;

    float cibleVitesse, vitesse;
    float cibleRotation, rotationActuelle;
    bool surBateau;
    CharacterController cc;
    Canvas menuViking;
    TextMeshProUGUI etatTexte, messageTexte;
    float messageTimer;
    readonly Dictionary<Manoeuvre, Image> boutons = new Dictionary<Manoeuvre, Image>();
    Collider[] collidersBateau;
    float demiLongueur = 11f;

    InputAction boutonMenu;

    void Awake()
    {
        if (menu) menu.SetActive(false);
        // Bouton "Menu" des manettes (touche M dans le XR Interaction Simulator) : ouvre / ferme les commandes
        boutonMenu = new InputAction("MenuDrakkar", InputActionType.Button);
        boutonMenu.AddBinding("<XRController>{LeftHand}/menuButton");
        boutonMenu.AddBinding("<XRController>{RightHand}/menuButton");
        boutonMenu.AddBinding("<XRController>/menu");
        boutonMenu.performed += _ => BasculerMenu();
    }

    void OnEnable() { boutonMenu?.Enable(); }
    void OnDisable() { boutonMenu?.Disable(); }
    void OnDestroy() { boutonMenu?.Dispose(); }

    /// <summary>Raccourcis clavier pour tester sans casque : P = menu, O = monter à bord.</summary>
    void RaccourcisClavier()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.pKey.wasPressedThisFrame) BasculerMenu();
        if (kb.oKey.wasPressedThisFrame) MonterABord();
    }

    /// <summary>Place le joueur sur le pont, à l'arrière du mât, face à l'avant du drakkar.</summary>
    public void MonterABord()
    {
        TrouverJoueur();
        if (!xrOrigin) return;
        Vector3 pont = transform.position + Vector3.up * 1.25f - Avant * 5.5f;
        if (Physics.Raycast(pont + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && hit.collider.transform.IsChildOf(transform)) pont = hit.point;
        bool actif = cc && cc.enabled;
        if (actif) cc.enabled = false;
        // on tourne le rig pour que la tête regarde vers l'avant du bateau
        if (tete)
        {
            float yawTete = Vector3.SignedAngle(Vector3.ProjectOnPlane(tete.forward, Vector3.up), Avant, Vector3.up);
            xrOrigin.Rotate(0f, yawTete, 0f, Space.World);
            Vector3 decalage = tete.position - xrOrigin.position; decalage.y = 0f;
            xrOrigin.position = pont - decalage;
        }
        else xrOrigin.position = pont;
        if (actif) cc.enabled = true;
        surBateau = true;
        Message("Bienvenue à bord ! Vise la voile ou appuie sur Menu pour commander.");
    }

    void Start()
    {
        TrouverJoueur();
        collidersBateau = GetComponentsInChildren<Collider>(true);

        // Demi-longueur du bateau, d'après la coque
        var b = new Bounds(transform.position, Vector3.zero);
        foreach (var r in GetComponentsInChildren<MeshRenderer>()) if (r.name == "Cube") b.Encapsulate(r.bounds);
        if (b.size.sqrMagnitude > 1f) demiLongueur = Vector3.Scale(b.extents, Avant).magnitude;

        RendreInteractif("Plane", true);       // la voile
        RendreInteractif("Cylinder", false);   // le mât
        ConstruireMenu();
    }

    void TrouverJoueur()
    {
        if (xrOrigin == null || !xrOrigin.gameObject.activeInHierarchy)
        {
            var o = FindAnyObjectByType<XROrigin>();
            if (o) xrOrigin = o.transform;
        }
        if (tete == null || !tete.gameObject.activeInHierarchy)
        {
            var o = xrOrigin ? xrOrigin.GetComponent<XROrigin>() : null;
            if (o && o.Camera) tete = o.Camera.transform;
            else if (Camera.main) tete = Camera.main.transform;
        }
        if (xrOrigin) cc = xrOrigin.GetComponent<CharacterController>();
    }

    /// <summary>Ajoute un collider + XR Simple Interactable sur la voile / le mât : gâchette = menu.</summary>
    void RendreInteractif(string nomEnfant, bool estVoile)
    {
        Transform t = TrouverEnfant(transform, nomEnfant);
        if (t == null) return;
        if (estVoile && t.GetComponent<VoileAnimee>() == null) t.gameObject.AddComponent<VoileAnimee>().ordre = this;

        Collider col = t.GetComponent<Collider>();
        if (col == null)
        {
            var mf = t.GetComponent<MeshFilter>();
            var box = t.gameObject.AddComponent<BoxCollider>();
            if (mf && mf.sharedMesh)
            {
                var bb = mf.sharedMesh.bounds;
                Vector3 s = bb.size;
                // épaisseur minimale pour pouvoir viser la voile
                int axeMin = s.x < s.y ? (s.x < s.z ? 0 : 2) : (s.y < s.z ? 1 : 2);
                s[axeMin] = Mathf.Max(s[axeMin], 0.01f);
                box.center = bb.center; box.size = s;
            }
            col = box;
        }
        var inter = t.GetComponent<XRSimpleInteractable>();
        if (inter == null) inter = t.gameObject.AddComponent<XRSimpleInteractable>();
        if (!inter.colliders.Contains(col)) inter.colliders.Add(col);
        inter.selectEntered.AddListener(_ => BasculerMenu());
    }

    static Transform TrouverEnfant(Transform racine, string nom)
    {
        foreach (var t in racine.GetComponentsInChildren<Transform>(true)) if (t.name == nom) return t;
        return null;
    }

    // ------------------------------------------------------------------ Menu viking

    void ConstruireMenu()
    {
        menuViking = VikingUI.CreerCanvas("Menu_Drakkar", transform, new Vector2(760, 900), 0.00075f);
        var root = menuViking.transform;
        VikingUI.Fond(root);
        var titre = VikingUI.Texte("Titre", root, "Commandes du Drakkar", 58, VikingUI.Or, TextAlignmentOptions.Center, true);
        Place(titre.rectTransform, 0, 330, 680, 80);
        VikingUI.Separateur(root, -170);

        AjouterBouton(Manoeuvre.Avancer, "Avancer", new Vector2(0, 175), Avancer);
        AjouterBouton(Manoeuvre.Gauche, "Bâbord  (gauche)", new Vector2(-170, 55), TournerGauche, new Vector2(320, 100));
        AjouterBouton(Manoeuvre.Droite, "Tribord  (droite)", new Vector2(170, 55), TournerDroite, new Vector2(320, 100));
        AjouterBouton(Manoeuvre.Stop, "Stop", new Vector2(0, -65), Stop);
        AjouterBouton(Manoeuvre.Reculer, "Reculer", new Vector2(0, -185), Reculer);

        etatTexte = VikingUI.Texte("Etat", root, "", 30, VikingUI.Parchemin);
        Place(etatTexte.rectTransform, 0, -285, 660, 60);
        messageTexte = VikingUI.Texte("Message", root, "", 28, new Color(1f, 0.85f, 0.5f));
        Place(messageTexte.rectTransform, 0, -345, 660, 60);

        menuViking.gameObject.SetActive(false);
        MettreAJourBoutons();
    }

    void AjouterBouton(Manoeuvre m, string label, Vector2 pos, UnityEngine.Events.UnityAction action, Vector2? taille = null)
    {
        var b = VikingUI.Bouton("Bouton_" + m, menuViking.transform, label, pos, taille ?? new Vector2(440, 100), action, 36);
        boutons[m] = b.GetComponent<Image>();
    }

    static void Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(w, h);
        r.anchoredPosition = new Vector2(x, y);
    }

    void MettreAJourBoutons()
    {
        foreach (var kv in boutons)
            kv.Value.color = kv.Key == manoeuvre ? new Color(1f, 0.82f, 0.45f) : Color.white;
    }

    /// <summary>Appelée par la voile / le mât (gâchette en VR).</summary>
    public void BasculerMenu()
    {
        TrouverJoueur();
        if (!surBateau)
        {
            Message("Monte à bord du drakkar pour le commander !");
            if (!menuViking.gameObject.activeSelf) return;
        }
        bool actif = !menuViking.gameObject.activeSelf;
        menuViking.gameObject.SetActive(actif);
        if (!actif || tete == null) return;

        Vector3 avant = Vector3.ProjectOnPlane(tete.forward, Vector3.up).normalized;
        if (avant.sqrMagnitude < 0.01f) avant = Avant;
        Vector3 pos = tete.position + avant * distanceMenu + Vector3.up * hauteurMenu;
        menuViking.transform.position = pos;
        menuViking.transform.rotation = Quaternion.LookRotation(pos - tete.position, Vector3.up);
        menuViking.worldCamera = Camera.main;
    }

    void Message(string texte, float duree = 4f)
    {
        if (messageTexte) messageTexte.text = texte;
        messageTimer = duree;
        PanneauPont.Annoncer(texte, duree);
    }

    // ------------------------------------------------------------------ Boucle

    void Update()
    {
        RaccourcisClavier();
        Vector3 anciennePos = transform.position;
        Quaternion ancienneRot = transform.rotation;
        float dt = Time.deltaTime;

        // Cadence des rames (phase partagée par tous les rameurs pour qu'ils soient synchronisés)
        float cadence = manoeuvre == Manoeuvre.Avancer ? cadenceAvant :
                        manoeuvre == Manoeuvre.Reculer ? cadenceArriere :
                        manoeuvre == Manoeuvre.Stop ? 0.25f : cadenceVirage;
        PhaseRame = Mathf.Repeat(PhaseRame + cadence * dt, 1f);

        vitesse = Mathf.MoveTowards(vitesse, cibleVitesse, acceleration * dt);
        rotationActuelle = Mathf.MoveTowards(rotationActuelle, cibleRotation, accelerationRotation * dt);

        // Terre devant ? on s'arrête (on ne traverse pas les îles)
        if (Mathf.Abs(vitesse) > 0.01f && ObstacleDevant(Mathf.Sign(vitesse), Mathf.Abs(vitesse) * vitesseMax))
        {
            vitesse = 0f;
            cibleVitesse = 0f;
            if (manoeuvre == Manoeuvre.Avancer || manoeuvre == Manoeuvre.Reculer) DefinirManoeuvre(Manoeuvre.Stop);
            Message(EstProcheDestination() ? "Terre ! Vous avez atteint l'île du Nord." : "Attention, terre droit devant ! Le drakkar s'arrête.");
        }

        // Le virage n'est efficace que si le bateau a un peu d'erre (ou à la rame sur place, lentement)
        float efficaciteVirage = Mathf.Lerp(0.45f, 1f, Mathf.Abs(vitesse));
        transform.Translate(directionAvant.normalized * (vitesse * vitesseMax) * dt, Space.Self);
        transform.Rotate(0f, rotationActuelle * vitesseRotation * efficaciteVirage * dt, 0f, Space.World);

        bool etaitSurBateau = surBateau;
        surBateau = EstSurBateau();

        if (etaitSurBateau && !surBateau && menuViking) menuViking.gameObject.SetActive(false);

        // Le joueur est transporté par le bateau
        if (surBateau && xrOrigin)
        {
            Quaternion delta = transform.rotation * Quaternion.Inverse(ancienneRot);
            Vector3 relatif = xrOrigin.position - anciennePos;
            bool ccActif = cc && cc.enabled;
            if (ccActif) cc.enabled = false;
            xrOrigin.position = transform.position + delta * relatif;
            xrOrigin.rotation = delta * xrOrigin.rotation;
            if (ccActif) cc.enabled = true;
        }

        // Arrivée
        if (!Arrive && EstProcheDestination() && Mathf.Abs(vitesse) < 0.05f)
        {
            Arrive = true;
            Message("Bravo ! Le drakkar est arrivé à l'île du Nord. Téléporte-toi sur la plage !", 10f);
        }

        // Menu : état
        if (menuViking && menuViking.gameObject.activeSelf && etatTexte)
        {
            float cap = Mathf.Repeat(Vector3.SignedAngle(Vector3.forward, Vector3.ProjectOnPlane(Avant, Vector3.up), Vector3.up), 360f);
            etatTexte.text = $"Cap : {cap:0}° {PointCardinal(cap)}   ·   {Mathf.Abs(VitesseMs) * 1.944f:0.0} noeuds";
        }
        if (messageTimer > 0f)
        {
            messageTimer -= dt;
            if (messageTimer <= 0f && messageTexte) messageTexte.text = "";
        }
    }

    public static string PointCardinal(float cap)
    {
        string[] noms = { "N", "NE", "E", "SE", "S", "SO", "O", "NO" };
        return noms[Mathf.RoundToInt(Mathf.Repeat(cap, 360f) / 45f) % 8];
    }

    bool EstProcheDestination()
    {
        if (destination == null) return false;
        Vector3 d = destination.position - transform.position; d.y = 0f;
        return d.magnitude < rayonArrivee;
    }

    readonly RaycastHit[] hits = new RaycastHit[16];
    Collider derniereAlerte;
    bool ObstacleDevant(float sens, float vitesseMs)
    {
        Vector3 dir = Avant * sens;
        Vector3 origine = transform.position + dir * (demiLongueur - 2f) + Vector3.up * (niveauEau - transform.position.y + 0.3f);
        float distance = 2.5f + vitesseMs * 0.6f;
        int n = Physics.SphereCastNonAlloc(origine, 1.4f, dir, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            if (c == null || hits[i].distance <= 0f) continue;          // collider déjà chevauché au départ : ignoré
            if (c.transform.IsChildOf(transform)) continue;             // le bateau lui-même, rameurs, rames
            if (xrOrigin && c.transform.IsChildOf(xrOrigin)) continue;  // le joueur
            if (c.attachedRigidbody && !c.attachedRigidbody.isKinematic) continue; // objets mobiles (boussole...)
            // seulement la terre (terrain) et les grosses structures : on ignore poteaux, tonneaux, lampadaires...
            if (!(c is TerrainCollider) && c.bounds.size.magnitude < 8f) continue;
            string nomCol = c.name.ToLowerInvariant();
            if (nomCol.Contains("water") || nomCol.Contains("rope") || nomCol.Contains("corde")) continue;   // la mer, les cordages
            if (c is TerrainCollider && hits[i].point.y < niveauEau - 0.6f) continue;   // fond marin profond
            if (derniereAlerte != c) { derniereAlerte = c; Debug.Log("[Drakkar] Obstacle devant le drakkar : " + c.name); }
            return true;
        }
        return false;
    }

    bool EstSurBateau()
    {
        if (!xrOrigin) return false;
        Vector3 pos = tete ? new Vector3(tete.position.x, xrOrigin.position.y, tete.position.z) : xrOrigin.position;
        if (!zonePont)
        {
            Vector3 l = transform.InverseTransformPoint(pos);
            return Mathf.Abs(l.x) < 5f && Mathf.Abs(l.z) < 22f && l.y > -2f && l.y < 15f;
        }
        Vector3 p = zonePont.transform.InverseTransformPoint(pos) - zonePont.center;
        Vector3 h = zonePont.size * 0.5f;
        return Mathf.Abs(p.x) <= h.x && Mathf.Abs(p.y) <= h.y && Mathf.Abs(p.z) <= h.z;
    }

    void DefinirManoeuvre(Manoeuvre m)
    {
        manoeuvre = m;
        MettreAJourBoutons();
    }

    /// <summary>
    /// Pour un rameur situé du côté "coteX" (+1 = tribord / droite, -1 = bâbord / gauche) :
    /// +1 = il tire (le bateau avance), -1 = il dénage (le bateau recule), 0 = rame levée.
    /// </summary>
    public int SensRame(float coteX)
    {
        switch (manoeuvre)
        {
            case Manoeuvre.Avancer: return 1;
            case Manoeuvre.Reculer: return -1;
            case Manoeuvre.Droite: return coteX > 0f ? -1 : 1;   // tribord dénage, bâbord tire : on vire à droite
            case Manoeuvre.Gauche: return coteX > 0f ? 1 : -1;
            default: return 0;
        }
    }

    // ===== Boutons du menu =====
    public void Avancer() { cibleVitesse = 1f; cibleRotation = 0f; DefinirManoeuvre(Manoeuvre.Avancer); }
    public void Reculer() { cibleVitesse = -0.5f; cibleRotation = 0f; DefinirManoeuvre(Manoeuvre.Reculer); }
    public void Stop() { cibleVitesse = 0f; cibleRotation = 0f; DefinirManoeuvre(Manoeuvre.Stop); }
    public void TournerDroite()
    {
        if (manoeuvre == Manoeuvre.Droite) { cibleRotation = 0f; DefinirManoeuvre(cibleVitesse > 0 ? Manoeuvre.Avancer : cibleVitesse < 0 ? Manoeuvre.Reculer : Manoeuvre.Stop); return; }
        cibleRotation = 1f; DefinirManoeuvre(Manoeuvre.Droite);
    }
    public void TournerGauche()
    {
        if (manoeuvre == Manoeuvre.Gauche) { cibleRotation = 0f; DefinirManoeuvre(cibleVitesse > 0 ? Manoeuvre.Avancer : cibleVitesse < 0 ? Manoeuvre.Reculer : Manoeuvre.Stop); return; }
        cibleRotation = -1f; DefinirManoeuvre(Manoeuvre.Gauche);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position + Vector3.up * 3f, transform.TransformDirection(directionAvant).normalized * 15f);
    }
}
