using UnityEngine;

public class Ordre : MonoBehaviour
{
    [Header("Joueur")]
    public Transform xrOrigin;
    public Transform tete;
    public BoxCollider zonePont;

    [Header("Menu")]
    public GameObject menu;
    public float distanceMenu = 1.2f;
    public float hauteurMenu = -0.1f;

    [Header("Mouvement")]
    public Vector3 directionAvant = Vector3.forward;
    public float vitesseMax = 2f;
    public float vitesseRotation = 8f;
    public float acceleration = 0.5f;
    public float accelerationRotation = 2f;

    float cibleVitesse, vitesse;
    float cibleRotation, rotation;
    bool surBateau;
    CharacterController cc;

    void Awake()
    {
        if (menu) menu.SetActive(false);
        if (xrOrigin) cc = xrOrigin.GetComponent<CharacterController>();
    }

    // Appelée par le mât (XR Simple Interactable -> Select Entered)
    public void BasculerMenu()
    {
        if (!surBateau) return;                 // on ne l'ouvre que si on est sur le bateau
        bool actif = !menu.activeSelf;
        menu.SetActive(actif);
        if (!actif) return;

        Vector3 avant = Vector3.ProjectOnPlane(tete.forward, Vector3.up).normalized;
        Vector3 pos = tete.position + avant * distanceMenu + Vector3.up * hauteurMenu;
        menu.transform.position = pos;
        menu.transform.rotation = Quaternion.LookRotation(pos - tete.position, Vector3.up);
    }

    void Update()
    {
        // Position / rotation avant déplacement
        Vector3 anciennePos = transform.position;
        Quaternion ancienneRot = transform.rotation;

        vitesse  = Mathf.MoveTowards(vitesse,  cibleVitesse,  acceleration * Time.deltaTime);
        rotation = Mathf.MoveTowards(rotation, cibleRotation, accelerationRotation * Time.deltaTime);

        transform.Translate(directionAvant * (vitesse * vitesseMax) * Time.deltaTime, Space.Self);
        transform.Rotate(0f, rotation * vitesseRotation * Time.deltaTime, 0f, Space.World);

        bool etaitSurBateau = surBateau;
        surBateau = EstSurBateau();

        // On vient de quitter le bateau : on ferme le menu et on arrête
        if (etaitSurBateau && !surBateau)
        {
            if (menu) menu.SetActive(false);
            Stop();
            vitesse = 0f; rotation = 0f;
        }

        // Le joueur suit le bateau
        if (surBateau && xrOrigin)
        {
            Quaternion delta = transform.rotation * Quaternion.Inverse(ancienneRot);
            Vector3 relatif = xrOrigin.position - anciennePos;

            if (cc) cc.enabled = false;
            xrOrigin.position = transform.position + delta * relatif;
            xrOrigin.rotation = delta * xrOrigin.rotation;
            if (cc) cc.enabled = true;
        }
    }

    bool EstSurBateau()
    {
        if (!zonePont || !xrOrigin) return false;
        Vector3 p = zonePont.transform.InverseTransformPoint(xrOrigin.position) - zonePont.center;
        Vector3 h = zonePont.size * 0.5f;
        return Mathf.Abs(p.x) <= h.x && Mathf.Abs(p.y) <= h.y && Mathf.Abs(p.z) <= h.z;
    }

    // ===== Boutons du menu =====
    public void Avancer() { cibleVitesse = 1f; }
    public void Reculer() { cibleVitesse = -0.5f; }
    public void Stop()    { cibleVitesse = 0f; cibleRotation = 0f; }
    public void TournerDroite()  { cibleRotation = (cibleRotation > 0f) ? 0f : 1f; }
    public void TournerGauche()  { cibleRotation = (cibleRotation < 0f) ? 0f : -1f; }
}