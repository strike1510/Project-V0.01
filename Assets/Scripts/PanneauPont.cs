using TMPro;
using UnityEngine;

/// <summary>
/// Panneau viking sur le pont du drakkar : annonce la mission (cap au NORD) et rappelle comment s'orienter :
/// le jour avec la boussole, la nuit avec l'Étoile polaire. Il affiche aussi le cap actuel et les annonces.
/// À mettre sur un objet vide enfant du drakkar, à l'endroit où le panneau doit apparaître.
/// </summary>
public class PanneauPont : MonoBehaviour
{
    [TextArea(1, 3)] public string titre = "Cap au NORD !";
    [TextArea(2, 6)] public string mission = "Le jarl nous ordonne de rejoindre l'île du Nord.\nVise la voile pour donner tes ordres aux rameurs.";
    [TextArea(2, 4)] public string conseilJour = "Le jour : tiens la boussole, son aiguille montre le Nord.";
    [TextArea(2, 4)] public string conseilNuit = "La nuit : cherche l'Étoile polaire, au bout de la Grande Ourse. Elle brille toujours au Nord.";
    public float echelle = 0.0011f;
    public bool faireFaceAuJoueur = true;

    static PanneauPont instance;
    TextMeshProUGUI titreTxt, missionTxt, conseilTxt, capTxt, annonceTxt;
    Ordre ordre;
    Transform tete;
    float annonceTimer;
    Canvas canvas;

    public static void Annoncer(string texte, float duree)
    {
        if (instance == null || instance.annonceTxt == null) return;
        instance.annonceTxt.text = texte;
        instance.annonceTimer = duree;
    }

    void Awake() { instance = this; }

    void Start()
    {
        ordre = GetComponentInParent<Ordre>();
        canvas = VikingUI.CreerCanvas("Panneau_Cap_Nord", transform, new Vector2(1000, 640), echelle);
        var root = canvas.transform;
        VikingUI.Fond(root);
        titreTxt = VikingUI.Texte("Titre", root, titre, 92, VikingUI.Or, TextAlignmentOptions.Center, true);
        Place(titreTxt.rectTransform, 0, 205, 900, 120);
        VikingUI.Separateur(root, -165);
        missionTxt = VikingUI.Texte("Mission", root, mission, 36, VikingUI.Parchemin);
        Place(missionTxt.rectTransform, 0, 60, 860, 120);
        conseilTxt = VikingUI.Texte("Conseil", root, "", 34, new Color(1f, 0.88f, 0.6f));
        conseilTxt.fontStyle = FontStyles.Italic;
        Place(conseilTxt.rectTransform, 0, -55, 860, 100);
        capTxt = VikingUI.Texte("Cap", root, "", 34, VikingUI.Or, TextAlignmentOptions.Center, true);
        Place(capTxt.rectTransform, 0, -135, 860, 50);
        annonceTxt = VikingUI.Texte("Annonce", root, "", 34, new Color(1f, 0.6f, 0.35f), TextAlignmentOptions.Center, true);
        Place(annonceTxt.rectTransform, 0, -205, 860, 60);
    }

    static void Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(w, h);
        r.anchoredPosition = new Vector2(x, y);
    }

    void Update()
    {
        if (canvas == null) return;
        if (tete == null && Camera.main) tete = Camera.main.transform;
        if (faireFaceAuJoueur) VikingUI.FaireFaceAuJoueur(canvas.transform, tete, 3f);

        bool nuit = CycleJourNuit.Nuit > 0.5f;
        conseilTxt.text = nuit ? conseilNuit : conseilJour;
        if (ordre)
        {
            Vector3 avant = Vector3.ProjectOnPlane(ordre.Avant, Vector3.up);
            float cap = Mathf.Repeat(Vector3.SignedAngle(Vector3.forward, avant, Vector3.up), 360f);
            string correction = cap < 12f || cap > 348f ? "bon cap !" : (cap < 180f ? "vire à bâbord (gauche)" : "vire à tribord (droite)");
            capTxt.text = $"Cap actuel : {cap:0}° {Ordre.PointCardinal(cap)}  -  {correction}";
        }
        if (annonceTimer > 0f)
        {
            annonceTimer -= Time.deltaTime;
            if (annonceTimer <= 0f) annonceTxt.text = "";
        }
    }
}
