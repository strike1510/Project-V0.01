using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Menu principal du jeu, en VR : Jouer, Comment jouer, Options, Quitter.
/// - Au lancement, le menu apparaît devant le joueur et le jeu est en pause.
/// - « Jouer » ferme le menu et lance la partie. Le bouton Menu de la manette gauche (ou Échap au clavier)
///   le rouvre pendant la partie : « Jouer » devient alors « Reprendre ».
/// - Style viking : fond en bois et boutons de VikingUI, contour en runes (Cadre_Runes), logo en haut.
///
/// Utilisation : un objet vide dans la scène Hub → Add Component → Menu Principal.
/// Glisse ensuite le logo EFREI (importé en Sprite) dans « Logo ».
/// </summary>
public class MenuPrincipal : MonoBehaviour
{
    [Header("Titre et logo")]
    public string titreJeu = "Cap au Nord";
    public string sousTitre = "Une aventure viking en réalité virtuelle";
    [Tooltip("Logo EFREI : importe l'image avec Texture Type = Sprite (2D and UI), puis glisse-la ici.")]
    public Sprite logo;
    [Tooltip("Contour en runes (Assets/Textures/Cadre_Runes).")]
    public Sprite cadreRunes;
    public Color couleurRunes = new Color(0.93f, 0.76f, 0.38f);

    [Header("Polices (optionnel)")]
    public TMP_FontAsset policeTitre;
    public TMP_FontAsset policeTexte;

    [Header("Placement devant le joueur")]
    [Tooltip("Distance entre les yeux et le menu (m).")]
    public float distance = 2.2f;
    [Tooltip("Hauteur du centre du menu par rapport aux yeux (m).")]
    public float hauteur = -0.1f;
    [Tooltip("Mètres par unité d'interface : 0.0011 = menu de 2,2 m de large.")]
    public float echelle = 0.0011f;

    [Header("Comportement")]
    public bool ouvrirAuLancement = true;
    [Tooltip("Le jeu est figé tant que le menu est ouvert.")]
    public bool mettreEnPause = true;

    [Header("Comment jouer")]
    [TextArea(8, 20)]
    public string texteCommentJouer =
        "<b>Ta mission</b> : monte à bord du drakkar et mets le cap au Nord. " +
        "Le jour, suis la boussole ; la nuit, l'Étoile polaire.\n\n" +
        "<b>Joystick gauche</b> : se déplacer\n" +
        "<b>Joystick droit</b> : tourner\n" +
        "<b>Gâchette</b> : viser et appuyer (boutons, runes, voile du drakkar)\n" +
        "<b>Bouton de saisie</b> : attraper un objet\n" +
        "<b>Bouton Menu</b> (manette gauche) : ouvrir ce menu";

    // taille de l'interface (unités) : mêmes proportions que l'image Cadre_Runes
    static readonly Vector2 Taille = new Vector2(2000, 1375);

    Canvas canvas;
    CanvasGroup groupe;
    GameObject pageAccueil, pageAide, pageOptions;
    TextMeshProUGUI labelJouer, texteVolume, labelRotation;
    InputAction actionMenu;
    bool ouvert, dejaJoue, rotationFluide;
    float alpha;
    int volume;                 // 0..10
    float suivreTeteJusqua;     // temps (réel) pendant lequel le menu se recale sur la tête

    // ------------------------------------------------------------------ mise en place

    void Awake()
    {
        volume = PlayerPrefs.GetInt("options_volume", 8);
        rotationFluide = PlayerPrefs.GetInt("options_rotation_fluide", 0) == 1;
        AudioListener.volume = volume / 10f;

        actionMenu = new InputAction("Menu", InputActionType.Button);
        actionMenu.AddBinding("<XRController>{LeftHand}/{MenuButton}");
        actionMenu.AddBinding("<Keyboard>/escape");
        actionMenu.performed += _ => { if (ouvert) Jouer(); else Ouvrir(); };
    }

    void OnEnable() => actionMenu?.Enable();
    void OnDisable() => actionMenu?.Disable();

    void OnDestroy()
    {
        actionMenu?.Dispose();
        if (mettreEnPause) Time.timeScale = 1f;
    }

    IEnumerator Start()
    {
        Construire();
        canvas.gameObject.SetActive(false);
        AppliquerRotation();
        if (!ouvrirAuLancement) yield break;
        // on laisse au casque le temps de donner la position de la tête
        yield return new WaitForSecondsRealtime(0.3f);
        Ouvrir();
        suivreTeteJusqua = Time.unscaledTime + 1.5f;
    }

    void Construire()
    {
        canvas = VikingUI.CreerCanvas("Menu_Principal", transform, Taille, echelle);
        canvas.sortingOrder = 50;
        groupe = canvas.GetComponent<CanvasGroup>();
        Transform racine = canvas.transform;

        VikingUI.Fond(racine, false);
        if (cadreRunes != null)
        {
            var cadre = VikingUI.CreerImage("Cadre_Runes", racine, cadreRunes);
            cadre.color = couleurRunes;
            VikingUI.Etirer(cadre.rectTransform, 0, 0, 0, 0);
        }
        else Debug.LogWarning("[MenuPrincipal] Glisse Assets/Textures/Cadre_Runes dans « Cadre Runes » pour le contour en runes.", this);

        // ---------- Accueil
        pageAccueil = Page("Accueil", racine);
        if (logo != null)
        {
            var img = VikingUI.CreerImage("Logo_EFREI", pageAccueil.transform, logo);
            img.preserveAspect = true;
            // coin en haut à droite, à l'intérieur du contour de runes
            Vector2 tailleLogo = new Vector2(460, 181);
            Placer(img.rectTransform,
                   new Vector2(Taille.x / 2f - 145f - tailleLogo.x / 2f, Taille.y / 2f - 138f - tailleLogo.y / 2f),
                   tailleLogo);
        }
        else Debug.LogWarning("[MenuPrincipal] Pas de logo : glisse le logo EFREI (Sprite) dans « Logo ».", this);

        var titre = VikingUI.Texte("Titre", pageAccueil.transform, titreJeu, 92, VikingUI.Or, TextAlignmentOptions.Center, true);
        Placer(titre.rectTransform, new Vector2(0, 280), new Vector2(900, 130));
        Police(titre, policeTitre);
        var sous = VikingUI.Texte("Sous_Titre", pageAccueil.transform, sousTitre, 48, VikingUI.Parchemin);
        Placer(sous.rectTransform, new Vector2(0, 180), new Vector2(1500, 80));
        Police(sous, policeTexte);

        float y0 = 55, pas = 118;
        var bJouer = Bouton("Jouer", pageAccueil.transform, "Jouer", new Vector2(0, y0), Jouer);
        labelJouer = bJouer.GetComponentInChildren<TextMeshProUGUI>();
        Bouton("Comment_Jouer", pageAccueil.transform, "Comment jouer", new Vector2(0, y0 - pas), () => Afficher(pageAide));
        Bouton("Options", pageAccueil.transform, "Options", new Vector2(0, y0 - 2 * pas), () => Afficher(pageOptions));
        Bouton("Quitter", pageAccueil.transform, "Quitter", new Vector2(0, y0 - 3 * pas), Quitter);

        // ---------- Comment jouer
        pageAide = Page("Comment_Jouer", racine);
        TitrePage(pageAide.transform, "Comment jouer");
        var aide = VikingUI.Texte("Texte", pageAide.transform, texteCommentJouer, 40, VikingUI.Parchemin, TextAlignmentOptions.TopLeft);
        aide.enableAutoSizing = true;
        aide.fontSizeMax = 40;
        aide.fontSizeMin = 20;
        aide.lineSpacing = 8;
        VikingUI.Etirer(aide.rectTransform, 210, 300, 210, 330);
        Police(aide, policeTexte);
        Bouton("Retour", pageAide.transform, "Retour", new Vector2(0, -440), () => Afficher(pageAccueil));

        // ---------- Options
        pageOptions = Page("Options", racine);
        TitrePage(pageOptions.transform, "Options");

        Etiquette(pageOptions.transform, "Volume", new Vector2(-330, 150));
        BoutonCarre(pageOptions.transform, "Volume_Moins", "−", new Vector2(90, 150), () => ChangerVolume(-1));
        texteVolume = VikingUI.Texte("Volume_Valeur", pageOptions.transform, "", 44, VikingUI.Or);
        Placer(texteVolume.rectTransform, new Vector2(235, 150), new Vector2(170, 90));
        Police(texteVolume, policeTexte);
        BoutonCarre(pageOptions.transform, "Volume_Plus", "+", new Vector2(380, 150), () => ChangerVolume(+1));

        Etiquette(pageOptions.transform, "Rotation", new Vector2(-330, 10));
        var bRot = VikingUI.Bouton("Rotation", pageOptions.transform, "", new Vector2(235, 10), new Vector2(380, 96), BasculerRotation, 36);
        labelRotation = bRot.GetComponentInChildren<TextMeshProUGUI>();
        Police(labelRotation, policeTexte);

        var note = VikingUI.Texte("Note", pageOptions.transform,
            "<i>Rotation par crans : plus confortable si tu as facilement mal au cœur.</i>", 28, VikingUI.Parchemin);
        Placer(note.rectTransform, new Vector2(0, -130), new Vector2(1150, 80));
        Police(note, policeTexte);
        Bouton("Retour", pageOptions.transform, "Retour", new Vector2(0, -440), () => Afficher(pageAccueil));

        MettreAJourOptions();
        Afficher(pageAccueil);
    }

    // ------------------------------------------------------------------ ouverture / fermeture

    /// <summary>Ouvre le menu devant le joueur (et met le jeu en pause).</summary>
    public void Ouvrir()
    {
        if (canvas == null) return;
        ouvert = true;
        if (labelJouer != null) labelJouer.text = dejaJoue ? "Reprendre" : "Jouer";
        Afficher(pageAccueil);
        canvas.gameObject.SetActive(true);
        PlacerDevantJoueur(true);
        if (mettreEnPause) Time.timeScale = 0f;
    }

    /// <summary>Bouton « Jouer » / « Reprendre » : ferme le menu et relance le jeu.</summary>
    public void Jouer()
    {
        ouvert = false;
        dejaJoue = true;
        if (mettreEnPause) Time.timeScale = 1f;
    }

    public void Quitter()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void Update()
    {
        if (canvas == null) return;
        float dt = Time.unscaledDeltaTime;       // le jeu peut être en pause (timeScale = 0)
        alpha = Mathf.MoveTowards(alpha, ouvert ? 1f : 0f, dt / 0.25f);
        groupe.alpha = alpha * alpha * (3f - 2f * alpha);
        groupe.interactable = groupe.blocksRaycasts = ouvert;
        if (!ouvert && alpha <= 0f && canvas.gameObject.activeSelf) canvas.gameObject.SetActive(false);

        if (ouvert && Time.unscaledTime < suivreTeteJusqua) PlacerDevantJoueur(true);
    }

    void PlacerDevantJoueur(bool instantane)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 avant = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (avant.sqrMagnitude < 0.001f) avant = Vector3.forward;
        avant.Normalize();
        canvas.transform.position = cam.transform.position + avant * distance + Vector3.up * hauteur;
        canvas.worldCamera = cam;
        VikingUI.FaireFaceAuJoueur(canvas.transform, cam.transform, 6f, instantane);
    }

    void Afficher(GameObject page)
    {
        pageAccueil.SetActive(page == pageAccueil);
        pageAide.SetActive(page == pageAide);
        pageOptions.SetActive(page == pageOptions);
    }

    // ------------------------------------------------------------------ options

    void ChangerVolume(int delta)
    {
        volume = Mathf.Clamp(volume + delta, 0, 10);
        AudioListener.volume = volume / 10f;
        PlayerPrefs.SetInt("options_volume", volume);
        MettreAJourOptions();
    }

    void BasculerRotation()
    {
        rotationFluide = !rotationFluide;
        PlayerPrefs.SetInt("options_rotation_fluide", rotationFluide ? 1 : 0);
        AppliquerRotation();
        MettreAJourOptions();
    }

    /// <summary>Règle la rotation sur les manettes du rig XR (script ControllerInputActionManager des Starter Assets).</summary>
    void AppliquerRotation()
    {
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb.GetType().Name != "ControllerInputActionManager") continue;
            var p = mb.GetType().GetProperty("smoothTurnEnabled", BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite) p.SetValue(mb, rotationFluide);
        }
    }

    void MettreAJourOptions()
    {
        if (texteVolume != null) texteVolume.text = (volume * 10) + " %";
        if (labelRotation != null) labelRotation.text = rotationFluide ? "Fluide" : "Par crans";
    }

    // ------------------------------------------------------------------ petits outils

    static GameObject Page(string nom, Transform parent)
    {
        var go = new GameObject(nom, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        VikingUI.Etirer((RectTransform)go.transform, 0, 0, 0, 0);
        return go;
    }

    void TitrePage(Transform parent, string texte)
    {
        var t = VikingUI.Texte("Titre", parent, texte, 80, VikingUI.Or, TextAlignmentOptions.Center, true);
        Placer(t.rectTransform, new Vector2(0, 420), new Vector2(1150, 120));
        Police(t, policeTitre);
        VikingUI.Separateur(parent, -345, 300);
    }

    void Etiquette(Transform parent, string texte, Vector2 pos)
    {
        var t = VikingUI.Texte("Label_" + texte, parent, texte, 44, VikingUI.Parchemin, TextAlignmentOptions.Left);
        Placer(t.rectTransform, pos, new Vector2(420, 90));
        Police(t, policeTexte);
    }

    Button Bouton(string nom, Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        var b = VikingUI.Bouton(nom, parent, label, pos, new Vector2(580, 98), action, 40);
        Police(b.GetComponentInChildren<TextMeshProUGUI>(), policeTexte);
        return b;
    }

    void BoutonCarre(Transform parent, string nom, string label, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        var b = VikingUI.Bouton(nom, parent, label, pos, new Vector2(110, 96), action, 52);
        Police(b.GetComponentInChildren<TextMeshProUGUI>(), policeTexte);
    }

    static void Placer(RectTransform r, Vector2 pos, Vector2 taille)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = taille;
        r.anchoredPosition = pos;
    }

    static void Police(TMP_Text t, TMP_FontAsset police)
    {
        if (t != null && police != null) t.font = police;
    }
}
