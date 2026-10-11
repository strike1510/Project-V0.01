using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// À mettre sur l'image de rune posée sur la roche (un Quad avec ton image, enfant de la roche).
/// Le panneau est un Canvas que tu construis toi-même dans la scène : ce script ne fait que
/// l'afficher, le cacher et remplir les textes.
/// - De loin : la rune est faiblement bleue. En approchant, elle pulse.
/// - Tout près : la rune s'allume et le panneau apparaît en fondu. Il disparaît quand on s'éloigne.
/// - Boutons du panneau : dans leur On Click (), appelle RuneInfo → NextPage / PreviousPage.
/// - Cliquer sur la rune (ou la viser + gâchette en VR) ferme / rouvre le panneau.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class RuneInfo : MonoBehaviour
{
    [Header("Couleur de la rune")]
    [Tooltip("Couleur quand la rune est allumée (sert aussi pour le cadre et le titre du panneau).")]
    public Color runeColor = new Color(0.25f, 0.6f, 1f);
    [Tooltip("Luminosité au repos, quand le joueur est loin (0 = éteinte).")]
    [Range(0f, 1f)] public float idleBrightness = 0.25f;
    [Tooltip("Luminosité quand le panneau est ouvert (au-dessus de 1, elle brille avec le Bloom).")]
    public float activeBrightness = 2.5f;
    [Tooltip("Coché si les runes de l'image sont foncées (noires). Le script les transforme en runes blanches " +
             "pour pouvoir les colorer en bleu. Il faut cocher « Read/Write » dans les réglages d'import de l'image.")]
    public bool runesAreDark = true;

    [Header("Distances (m)")]
    [Tooltip("En dessous de cette distance, le panneau s'ouvre tout seul.")]
    public float showDistance = 3.5f;
    [Tooltip("Au-delà de cette distance, le panneau se referme (un peu plus que showDistance pour éviter le clignotement).")]
    public float hideDistance = 5f;
    [Tooltip("En dessous de cette distance, la rune pulse pour attirer l'œil.")]
    public float hintDistance = 10f;
    [Tooltip("Distance max pour cliquer la rune à la souris.")]
    public float clickDistance = 10f;

    [Header("Panneau (ton Canvas dans la scène)")]
    [Tooltip("L'objet Canvas du panneau. Il est caché au lancement.")]
    public GameObject panel;
    [Tooltip("Le texte du titre (TextMeshPro).")]
    public TMP_Text titleText;
    [Tooltip("Le texte de la page (TextMeshPro).")]
    public TMP_Text bodyText;
    [Tooltip("Optionnel : texte qui affiche « 1 / 8 ».")]
    public TMP_Text pageNumberText;
    [Tooltip("Coché : le panneau se tourne toujours vers le joueur. Décoché : il reste là où tu l'as placé.")]
    public bool facePlayer = true;
    [Tooltip("Durée du fondu d'apparition (s).")]
    public float fadeDuration = 0.35f;
    [Tooltip("Les pages défilent aussi toutes seules.")]
    public bool autoAdvance = false;
    [Tooltip("Secondes par page si le défilement automatique est activé.")]
    public float secondsPerPage = 12f;

    [Header("Contenu")]
    public List<InfoPage> pages = DefaultPages();

    [System.Serializable]
    public class InfoPage
    {
        public string title;
        [TextArea(4, 10)] public string body;
    }

    // --- état interne ---
    Renderer runeRenderer;
    Collider runeCollider;
    MaterialPropertyBlock block;
    Texture2D runeMask;
    CanvasGroup panelGroup;
    bool open;
    bool dismissed;                          // fermé à la main : ne se rouvre pas tant qu'on ne s'est pas éloigné
    bool hoveredByMouse, hoveredByVR;
    float openAmount;                        // 0 = fermé, 1 = ouvert (animé)
    float glow;
    int page;
    float pageTimer;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    // ---------------------------------------------------------------- cycle de vie

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
        if (runeCollider == null)
        {
            runeCollider = gameObject.AddComponent<BoxCollider>();
            Debug.Log("[RuneInfo] Pas de Collider sur la rune : un BoxCollider a été ajouté.", this);
        }
        block = new MaterialPropertyBlock();
        if (runesAreDark) BuildRuneMask();
        glow = idleBrightness;
        ApplyGlow();
    }

    void Start()
    {
        SetupVR();
        if (panel == null)
        {
            Debug.LogWarning("[RuneInfo] Aucun panneau assigné : glisse ton Canvas dans le champ « Panel ».", this);
            return;
        }
        EnsureEventSystem();
        var canvas = panel.GetComponent<Canvas>();
        if (canvas != null && canvas.worldCamera == null) canvas.worldCamera = Camera.main;   // clics souris
        panelGroup = panel.GetComponent<CanvasGroup>();
        if (panelGroup == null) panelGroup = panel.AddComponent<CanvasGroup>();
        panelGroup.alpha = 0f;
        panel.SetActive(false);
        ShowPage();
    }

    void OnDestroy()
    {
        if (runeMask != null) Destroy(runeMask);
    }

    /// <summary>Crée un EventSystem compatible souris + VR s'il n'y en a pas encore dans la scène.</summary>
    static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
        es.AddComponent<XRUIInputModule>();
    }

    /// <summary>VR : la rune devient « visable » au rayon des manettes (gâchette = ouvrir / fermer).</summary>
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

        HandleMouse(cam);

        // --- Ouverture / fermeture à la distance ---
        if (dist > hideDistance)
        {
            open = false;
            dismissed = false;
        }
        else if (dist < showDistance && !open && !dismissed)
        {
            Open();
        }

        // --- Lueur de la rune ---
        float target;
        if (open) target = activeBrightness;
        else if (hoveredByMouse || hoveredByVR) target = 1.4f;
        else if (dist < hintDistance) target = Mathf.Lerp(idleBrightness, 1f, 0.5f + 0.5f * Mathf.Sin(Time.time * 3f));
        else target = idleBrightness;
        glow = Mathf.Lerp(glow, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
        ApplyGlow();

        // --- Fondu du panneau ---
        openAmount = Mathf.MoveTowards(openAmount, open ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, fadeDuration));
        panelGroup.alpha = openAmount * openAmount * (3f - 2f * openAmount);
        panelGroup.interactable = panelGroup.blocksRaycasts = open;
        if (panel.activeSelf != openAmount > 0f) panel.SetActive(openAmount > 0f);
        if (openAmount <= 0f) return;

        // Le panneau pivote sur place pour faire face au joueur (seulement autour de l'axe vertical)
        if (facePlayer)
        {
            Vector3 look = panel.transform.position - cam.transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f)
                panel.transform.rotation = Quaternion.Slerp(panel.transform.rotation, Quaternion.LookRotation(look),
                                                            1f - Mathf.Exp(-6f * Time.deltaTime));
        }

        if (open && autoAdvance && pages.Count > 1)
        {
            pageTimer += Time.deltaTime;
            if (pageTimer >= secondsPerPage) NextPage();
        }
    }

    /// <summary>Souris : survol + clic gauche sur la rune (ignoré si on clique sur un bouton du panneau).</summary>
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
        runeRenderer.GetPropertyBlock(block);
        if (runeMask != null) block.SetTexture(BaseMapId, runeMask);
        block.SetColor(BaseColorId, runeColor * glow);
        runeRenderer.SetPropertyBlock(block);
    }

    /// <summary>Ouvre ou ferme le panneau à la main (clic sur la rune).</summary>
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
            // Nouvelle ouverture : on repart de la première page, déjà tourné vers le joueur
            page = 0;
            pageTimer = 0f;
            ShowPage();
            if (facePlayer)
            {
                Vector3 look = panel.transform.position - Camera.main.transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.001f) panel.transform.rotation = Quaternion.LookRotation(look);
            }
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
        if (src == null)
        {
            Debug.LogWarning("[RuneInfo] Pas d'image dans la Base Map du matériau de la rune.", this);
            return;
        }
        if (!src.isReadable)
        {
            Debug.LogWarning($"[RuneInfo] L'image « {src.name} » n'est pas lisible : sélectionne-la, " +
                             "coche Advanced → Read/Write, puis Apply. En attendant, les runes restent noires.", src);
            return;
        }
        Color32[] px = src.GetPixels32();
        for (int i = 0; i < px.Length; i++)
        {
            Color32 c = px[i];
            float lum = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
            byte a = (byte)Mathf.RoundToInt(c.a * (1f - lum));     // sombre et opaque → visible
            px[i] = new Color32(255, 255, 255, a);
        }
        runeMask = new Texture2D(src.width, src.height, TextureFormat.RGBA32, true)
        {
            name = src.name + "_Masque",
            wrapMode = src.wrapMode,
            filterMode = src.filterMode,
        };
        runeMask.SetPixels32(px);
        runeMask.Apply();
    }

    // ---------------------------------------------------------------- textes par défaut

    static List<InfoPage> DefaultPages() => new List<InfoPage>
    {
        new InfoPage {
            title = "Les Vikings",
            body = "Les Vikings viennent de Scandinavie : Norvège, Suède et Danemark actuels.\n\n" +
                   "On situe leur époque entre la fin du VIIIe et le XIe siècle, du pillage du monastère de Lindisfarne (793) " +
                   "jusqu'à la bataille de Stamford Bridge (1066).\n\n" +
                   "Ce n'étaient pas que des pillards : la plupart étaient paysans, artisans, marchands et explorateurs." },
        new InfoPage {
            title = "Jusqu'où sont-ils allés ?",
            body = "<b>À l'ouest</b> : l'Islande vers 870, le Groenland avec Erik le Rouge vers 985, et l'Amérique du Nord vers l'an 1000 " +
                   "avec Leif Erikson (site de L'Anse aux Meadows, à Terre-Neuve).\n\n" +
                   "<b>À l'est</b> : par les fleuves de l'actuelle Russie, ils sont descendus jusqu'à Constantinople." },
        new InfoPage {
            title = "Les Vikings en France",
            body = "En 845, des Vikings remontent la Seine et assiègent Paris.\n\n" +
                   "En 911, par le traité de Saint-Clair-sur-Epte, le roi Charles le Simple cède des terres au chef viking Rollon. " +
                   "Elles deviennent la Normandie, le « pays des hommes du Nord »." },
        new InfoPage {
            title = "Pourquoi « drakkar » ?",
            body = "Le mot <i>drakkar</i> est récent : il a été popularisé en France au XIXe siècle. Il vient du vieux norrois <i>dreki</i>, « dragon ».\n\n" +
                   "Les Vikings parlaient plutôt de <i>langskip</i>, le « navire long ». Certains portaient une tête de dragon ou de serpent sculptée à la proue." },
        new InfoPage {
            title = "Un navire très malin",
            body = "La coque est faite de planches de chêne qui se chevauchent, fixées par des rivets en fer : on dit « bordée à clin ». Elle est légère et souple.\n\n" +
                   "Le tirant d'eau est faible, environ un mètre : le drakkar peut remonter les fleuves et accoster directement sur une plage." },
        new InfoPage {
            title = "Voile, rames et gouvernail",
            body = "Le drakkar avance avec une grande voile carrée en laine, ou à la rame quand le vent manque.\n\n" +
                   "On le dirige avec une large rame-gouvernail fixée sur le côté droit, à l'arrière. Le mot <i>tribord</i> vient de là : c'est le côté du gouvernail." },
        new InfoPage {
            title = "Quelques chiffres",
            body = "• Le navire d'Oseberg (vers 820) mesure environ 21 m.\n" +
                   "• Celui de Gokstad (vers 890) fait environ 23 m pour 32 rames.\n" +
                   "• Le grand navire de guerre Skuldelev 2 mesurait environ 30 m et embarquait 60 à 70 hommes.\n\n" +
                   "Les répliques modernes atteignent une dizaine de nœuds sous voile." },
        new InfoPage {
            title = "La vie à bord",
            body = "Pas de cabine : l'équipage vivait et dormait sur le pont, à l'abri de toiles ou de peaux.\n\n" +
                   "Sans boussole, on naviguait au soleil, aux étoiles, en observant les oiseaux, la houle et la couleur de l'eau.\n\n" +
                   "<b>Le drakkar t'attend au ponton !</b>" },
    };
}
