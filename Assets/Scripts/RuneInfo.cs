using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// La rune gravée sur la pierre de départ.
/// - Vise-la (rayon VR + gâchette) ou clique dessus : un grand panneau viking s'ouvre au-dessus de la pierre
///   et raconte l'histoire du drakkar et la navigation des Vikings (plusieurs pages).
/// - En t'approchant, la rune pulse ; tout près, le panneau s'ouvre tout seul.
///
/// MODIFIER LE TEXTE : sélectionne l'objet "Rune" dans la Hierarchy, les pages s'éditent dans l'Inspector
/// (titre + texte de chaque page). Ce que tu écris là est exactement ce qui s'affiche dans le jeu.
/// Le texte accepte les balises <b>gras</b>, <i>italique</i>, <color=#E8C66A>couleur</color>.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class RuneInfo : MonoBehaviour
{
    [Header("Contenu (modifiable ici !)")]
    public List<InfoPage> pages = DefaultPages();

    [System.Serializable]
    public class InfoPage
    {
        public string title;
        [TextArea(6, 20)] public string body;
    }

    [Header("Couleur de la rune")]
    public Color runeColor = new Color(0.25f, 0.6f, 1f);
    [Range(0f, 1f)] public float idleBrightness = 0.25f;
    public float activeBrightness = 2.5f;
    [Tooltip("Coché si les runes de l'image sont foncées (noires) : elles sont transformées en runes lumineuses. " +
             "Il faut cocher « Read/Write » dans les réglages d'import de l'image.")]
    public bool runesAreDark = true;

    [Header("Distances (m)")]
    public float showDistance = 3.5f;
    public float hideDistance = 6f;
    public float hintDistance = 10f;
    public float clickDistance = 10f;

    [Header("Panneau viking")]
    [Tooltip("Coché : le panneau stylé viking est généré automatiquement au-dessus de la pierre.")]
    public bool styleViking = true;
    [Tooltip("Hauteur du centre du panneau au-dessus de la rune (m).")]
    public float hauteurPanneau = 1.35f;
    [Tooltip("Taille du panneau (mètres par unité d'interface).")]
    public float echellePanneau = 0.0011f;
    [Tooltip("Ancien panneau (Canvas) : utilisé seulement si 'Style Viking' est décoché.")]
    public GameObject panel;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public TMP_Text pageNumberText;
    public bool facePlayer = true;
    public float fadeDuration = 0.35f;
    public bool autoAdvance = false;
    public float secondsPerPage = 14f;

    Renderer runeRenderer;
    Collider runeCollider;
    MaterialPropertyBlock block;
    Texture2D runeMask;
    CanvasGroup panelGroup;
    bool open, dismissed, hoveredByMouse, hoveredByVR;
    float openAmount, glow, pageTimer;
    int page;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    void OnValidate()
    {
        showDistance = Mathf.Max(0.5f, showDistance);
        hideDistance = Mathf.Max(hideDistance, showDistance + 0.5f);
        hintDistance = Mathf.Max(hintDistance, showDistance);
    }

    void Awake()
    {
        runeRenderer = GetComponent<Renderer>();
        runeCollider = GetComponent<Collider>();
        if (runeCollider == null) runeCollider = gameObject.AddComponent<BoxCollider>();
        block = new MaterialPropertyBlock();
        if (runesAreDark) BuildRuneMask();
        glow = idleBrightness;
        ApplyGlow();
    }

    void Start()
    {
        SetupVR();
        if (styleViking)
        {
            if (panel != null) panel.SetActive(false);
            ConstruirePanneauViking();
        }
        if (panel == null) { Debug.LogWarning("[RuneInfo] Aucun panneau.", this); return; }
        VikingUI.AssurerEventSystem();
        var canvas = panel.GetComponent<Canvas>();
        if (canvas != null && canvas.worldCamera == null) canvas.worldCamera = Camera.main;
        panelGroup = panel.GetComponent<CanvasGroup>();
        if (panelGroup == null) panelGroup = panel.AddComponent<CanvasGroup>();
        panelGroup.alpha = 0f;
        panel.SetActive(false);
        ShowPage();
    }

    void OnDestroy() { if (runeMask != null) Destroy(runeMask); }

    // ---------------------------------------------------------------- panneau viking

    void ConstruirePanneauViking()
    {
        var ancre = new GameObject("Panneau_Rune_Viking").transform;
        ancre.SetParent(transform, false);
        ancre.position = transform.position + Vector3.up * hauteurPanneau;
        var canvas = VikingUI.CreerCanvas("Canvas", ancre, new Vector2(1150, 800), echellePanneau);
        var root = canvas.transform;
        VikingUI.Fond(root);

        titleText = VikingUI.Texte("Titre", root, "", 62, VikingUI.Or, TextAlignmentOptions.Center, true);
        Place(titleText.rectTransform, 0, 270, 1000, 90);
        VikingUI.Separateur(root, -170, 170);

        var corps = VikingUI.Texte("Texte", root, "", 34, VikingUI.Parchemin, TextAlignmentOptions.TopJustified);
        corps.enableAutoSizing = true; corps.fontSizeMin = 22; corps.fontSizeMax = 36;
        corps.lineSpacing = 8;
        Place(corps.rectTransform, 0, -10, 1000, 420);
        bodyText = corps;

        pageNumberText = VikingUI.Texte("Page", root, "", 30, VikingUI.Or, TextAlignmentOptions.Center, true);
        Place(pageNumberText.rectTransform, 0, -290, 200, 60);
        VikingUI.Bouton("Precedent", root, "«  Précédent", new Vector2(-330, -290), new Vector2(300, 84), PreviousPage, 32);
        VikingUI.Bouton("Suivant", root, "Suivant  »", new Vector2(330, -290), new Vector2(300, 84), NextPage, 32);
        VikingUI.Bouton("Fermer", root, "X", new Vector2(510, 335), new Vector2(76, 76), Toggle, 34);

        panel = ancre.gameObject;
    }

    static void Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(w, h);
        r.anchoredPosition = new Vector2(x, y);
    }

    // ---------------------------------------------------------------- interaction

    void SetupVR()
    {
        var interactable = GetComponent<XRSimpleInteractable>();
        if (interactable == null) interactable = gameObject.AddComponent<XRSimpleInteractable>();
        if (!interactable.colliders.Contains(runeCollider)) interactable.colliders.Add(runeCollider);
        interactable.selectEntered.AddListener(_ => Toggle());
        interactable.hoverEntered.AddListener(_ => hoveredByVR = true);
        interactable.hoverExited.AddListener(_ => hoveredByVR = interactable.isHovered);
    }

    void Update()
    {
        Camera cam = Camera.main;
        if (cam == null || panelGroup == null) return;

        Vector3 toPlayer = cam.transform.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;

        if (runeCollider == null) runeCollider = GetComponent<Collider>();
        HandleMouse(cam);

        if (dist > hideDistance) { open = false; dismissed = false; }
        else if (dist < showDistance && !open && !dismissed) Open();

        float target;
        if (open) target = activeBrightness;
        else if (hoveredByMouse || hoveredByVR) target = 1.4f;
        else if (dist < hintDistance) target = Mathf.Lerp(idleBrightness, 1f, 0.5f + 0.5f * Mathf.Sin(Time.time * 3f));
        else target = idleBrightness;
        glow = Mathf.Lerp(glow, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
        ApplyGlow();

        openAmount = Mathf.MoveTowards(openAmount, open ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, fadeDuration));
        float a = openAmount * openAmount * (3f - 2f * openAmount);
        panelGroup.alpha = a;
        panelGroup.interactable = panelGroup.blocksRaycasts = open;
        if (panel.activeSelf != openAmount > 0f) panel.SetActive(openAmount > 0f);
        if (openAmount <= 0f) return;
        // petite apparition : le panneau "monte" de la pierre
        if (styleViking) panel.transform.position = transform.position + Vector3.up * (hauteurPanneau - 0.15f * (1f - a));

        if (facePlayer) VikingUI.FaireFaceAuJoueur(panel.transform, cam.transform);

        if (open && autoAdvance && pages.Count > 1)
        {
            pageTimer += Time.deltaTime;
            if (pageTimer >= secondsPerPage) NextPage();
        }
    }

    void HandleMouse(Camera cam)
    {
        hoveredByMouse = false;
        var mouse = Mouse.current;
        if (mouse == null) return;
        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        hoveredByMouse = runeCollider.Raycast(ray, out _, clickDistance)
                         && !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
        if (hoveredByMouse && mouse.leftButton.wasPressedThisFrame) Toggle();
    }

    void ApplyGlow()
    {
        if (runeRenderer == null) runeRenderer = GetComponent<Renderer>();
        if (block == null) block = new MaterialPropertyBlock();
        runeRenderer.GetPropertyBlock(block);
        if (runeMask != null) block.SetTexture(BaseMapId, runeMask);
        block.SetColor(BaseColorId, runeColor * glow);
        block.SetColor(EmissionId, runeColor * Mathf.Max(0f, glow - 0.3f));
        runeRenderer.SetPropertyBlock(block);
    }

    /// <summary>Ouvre ou ferme le panneau (clic / gâchette sur la rune, ou bouton X).</summary>
    public void Toggle()
    {
        if (open) { open = false; dismissed = true; return; }
        dismissed = false;
        Open();
    }

    void Open()
    {
        open = true;
        if (openAmount <= 0f)
        {
            page = 0;
            pageTimer = 0f;
            ShowPage();
            if (facePlayer && Camera.main) VikingUI.FaireFaceAuJoueur(panel.transform, Camera.main.transform, 0f, true);
        }
    }

    public void NextPage() => GoToPage(page + 1);
    public void PreviousPage() => GoToPage(page - 1);

    void GoToPage(int index)
    {
        if (pages.Count == 0) return;
        page = (index % pages.Count + pages.Count) % pages.Count;
        pageTimer = 0f;
        ShowPage();
    }

    void ShowPage()
    {
        if (pages.Count == 0) return;
        if (titleText != null) titleText.text = pages[page].title;
        if (bodyText != null) bodyText.text = pages[page].body;
        if (pageNumberText != null) pageNumberText.text = $"{page + 1} / {pages.Count}";
    }

    void BuildRuneMask()
    {
        var mat = runeRenderer.sharedMaterial;
        var src = mat != null && mat.HasProperty(BaseMapId) ? mat.GetTexture(BaseMapId) as Texture2D : null;
        if (src == null || !src.isReadable) return;
        Color32[] px = src.GetPixels32();
        for (int i = 0; i < px.Length; i++)
        {
            Color32 c = px[i];
            float lum = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
            byte a = (byte)Mathf.RoundToInt(c.a * (1f - lum));
            px[i] = new Color32(255, 255, 255, a);
        }
        runeMask = new Texture2D(src.width, src.height, TextureFormat.RGBA32, true)
        { name = src.name + "_Masque", wrapMode = src.wrapMode, filterMode = src.filterMode };
        runeMask.SetPixels32(px);
        runeMask.Apply();
    }

    // ---------------------------------------------------------------- textes par défaut

    public static List<InfoPage> DefaultPages() => new List<InfoPage>
    {
        new InfoPage {
            title = "Le drakkar, navire des Vikings",
            body = "Entre la fin du VIIIe et le XIe siècle, les Vikings de Scandinavie (Norvège, Suède, Danemark) ont parcouru les mers " +
                   "comme aucun autre peuple d'Europe. Leur secret : un navire exceptionnel.\n\n" +
                   "Nous l'appelons <b>drakkar</b>, un mot popularisé au XIXe siècle, venu du vieux norrois <i>dreki</i>, « dragon ». " +
                   "Les Vikings, eux, parlaient de <b>langskip</b> : le « navire long ».\n\n" +
                   "À la proue, une tête de dragon ou de serpent sculptée devait effrayer les ennemis... et les esprits du pays." },
        new InfoPage {
            title = "Une coque légère et souple",
            body = "La coque est <b>bordée à clin</b> : des planches de chêne qui se chevauchent, fixées par des rivets de fer. " +
                   "Les joints sont bouchés avec de la laine goudronnée.\n\n" +
                   "Le navire est long, étroit et souple : il plie avec les vagues au lieu de casser. " +
                   "Son <b>tirant d'eau</b> est faible (environ 1 m) : le drakkar remonte les fleuves et accoste directement sur une plage, " +
                   "parfait pour surprendre l'ennemi.\n\n" +
                   "Le navire de Gokstad (vers 890) mesure environ 23 m et porte <b>32 rames</b> : 16 de chaque côté, comme ici !" },
        new InfoPage {
            title = "La voile et les rames",
            body = "Une grande <b>voile carrée</b> en laine, souvent rayée de rouge, pousse le navire quand le vent est favorable. " +
                   "Les répliques modernes dépassent les 10 noeuds.\n\n" +
                   "Sans vent, pour manoeuvrer ou entrer au port, l'équipage prend les <b>rames</b>, passées dans des trous de la coque. " +
                   "Les rameurs, assis face à l'arrière, tirent tous ensemble au meme rythme.\n\n" +
                   "On dirige avec une large rame-gouvernail à l'arrière, sur le côté droit : le <i>styrbord</i>, « le bord du gouvernail ». " +
                   "C'est l'origine de notre mot <b>tribord</b> ! L'autre côté, dans le dos du barreur, est le <b>bâbord</b>." },
        new InfoPage {
            title = "Naviguer sans carte ni compas",
            body = "Les Vikings n'avaient <b>pas de boussole magnétique</b> : elle n'apparaît en Europe qu'à la fin du XIIe siècle.\n\n" +
                   "Ils naviguaient <b>à l'estime</b> : en connaissant leur cap, leur vitesse et le temps écoulé, ils devinaient leur position. " +
                   "Ils suivaient les cotes, repéraient les montagnes et mémorisaient les routes transmises de bouche à oreille.\n\n" +
                   "Pour traverser la haute mer, vers l'Islande ou le Groenland, ils lisaient la nature : " +
                   "le soleil, les étoiles, les vents, la houle et les animaux." },
        new InfoPage {
            title = "Le jour : suivre le soleil",
            body = "Le soleil se lève à l'est, culmine au sud à midi et se couche à l'ouest : il donne la direction.\n\n" +
                   "Au Groenland, on a retrouvé un morceau de disque de bois gravé (le disque d'Uunartoq) : beaucoup de chercheurs pensent " +
                   "que c'était un <b>compas solaire</b>, où l'ombre d'une tige indiquait le cap.\n\n" +
                   "Les sagas parlent aussi d'une mystérieuse <b>pierre de soleil</b> (<i>solarsteinn</i>). Ce serait un cristal, " +
                   "comme le spath d'Islande, qui permet de retrouver le soleil caché derrière les nuages.\n\n" +
                   "<color=#E8C66A>Dans le jeu, ta boussole remplace ces outils : son aiguille montre le Nord.</color>" },
        new InfoPage {
            title = "La nuit : l'Étoile polaire",
            body = "La nuit, toutes les étoiles tournent dans le ciel... sauf une, presque immobile : l'<b>Étoile polaire</b>. " +
                   "Elle indique toujours le <b>Nord</b>.\n\n" +
                   "Pour la trouver, repère la <b>Grande Ourse</b> (la grande casserole). Prolonge environ 5 fois la distance entre les deux " +
                   "étoiles du bord de la casserole (Merak et Dubhe) : tu tombes sur l'Étoile polaire.\n\n" +
                   "Plus on va vers le nord, plus elle est haute dans le ciel : sa hauteur donne une idée de la latitude.\n\n" +
                   "<color=#E8C66A>Dans le jeu, quand la nuit tombe, cherche-la pour garder le cap au Nord.</color>" },
        new InfoPage {
            title = "Lire la mer et les oiseaux",
            body = "Les marins vikings observaient tout :\n" +
                   "- les <b>oiseaux de mer</b> qui rentrent vers la terre le soir ;\n" +
                   "- les <b>baleines</b> et les bancs de poissons ;\n" +
                   "- la <b>couleur de l'eau</b> et la forme de la houle, qui changent près des côtes ;\n" +
                   "- les <b>nuages</b> immobiles qui se forment au-dessus des îles.\n\n" +
                   "Selon les récits islandais, Floki Vilgerdarson emporta des corbeaux : relâché en mer, l'un d'eux s'envola vers une terre inconnue... " +
                   "C'est ainsi qu'il trouva l'Islande !" },
        new InfoPage {
            title = "À toi de jouer, navigateur !",
            body = "Le drakkar t'attend au ponton. Monte à bord (téléporte-toi sur le pont).\n\n" +
                   "Vise la <b>voile</b> et appuie sur la gâchette pour donner tes ordres aux 16 rameurs : " +
                   "avancer, reculer, virer à bâbord ou à tribord, stopper.\n\n" +
                   "Ta mission : mettre <b>cap au NORD</b> et atteindre l'île du Nord.\n" +
                   "<color=#E8C66A>Le jour, fie-toi à la boussole. La nuit, à l'Étoile polaire.</color>\n\n" +
                   "Bon vent, Viking !" },
    };
}
