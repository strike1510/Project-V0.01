using UnityEngine;

/// <summary>
/// Aiguille de la boussole : elle tourne DANS le plan du cadran (autour de l'axe du boîtier)
/// et pointe toujours vers le NORD du monde (+Z), comme une vraie boussole magnétique.
/// Elle a de l'inertie : elle oscille un peu puis se stabilise quand on bouge la boussole.
/// À mettre sur l'aiguille (enfant du boîtier de la boussole).
/// </summary>
[DefaultExecutionOrder(100)]
public class CompassNeedle : MonoBehaviour
{
    [Tooltip("Optionnel : si rempli, l'aiguille vise cet objet au lieu du Nord.")]
    public Transform target;
    [Tooltip("Non utilisé (conservé pour compatibilité).")]
    public Transform pivot;
    [Tooltip("Correction d'angle si l'aiguille vise de travers (degrés).")]
    public float angleOffset = 0f;
    [Tooltip("Axe local de l'aiguille qui montre le Nord (pointe).")]
    public Vector3 axePointe = new Vector3(-1f, 0f, 0f);
    [Tooltip("Axe local du boîtier perpendiculaire au cadran.")]
    public Vector3 axeCadran = new Vector3(0f, 0f, 1f);
    [Tooltip("Direction du Nord dans le monde.")]
    public Vector3 nordMonde = Vector3.forward;
    [Header("Physique de l'aiguille")]
    public float raideur = 40f;
    public float amortissement = 6f;

    Quaternion repos;
    float angle, vitesseAngle;
    bool init;

    void Start()
    {
        repos = transform.localRotation;
        // si l'ancien réglage (90°) traîne dans la scène, on l'annule : il était fait pour l'ancien calcul
        if (Mathf.Approximately(angleOffset, 90f)) angleOffset = 0f;
    }

    void LateUpdate()
    {
        Transform boitier = transform.parent;
        if (boitier == null) return;

        Vector3 dirMonde = nordMonde;
        if (target != null)
        {
            dirMonde = target.position - transform.position;
            if (dirMonde.sqrMagnitude < 1e-6f) dirMonde = nordMonde;
        }

        // direction voulue dans le repère du boîtier, projetée sur le plan du cadran
        Vector3 n = axeCadran.normalized;
        Vector3 d = Vector3.ProjectOnPlane(boitier.InverseTransformDirection(dirMonde), n);
        // pointe de l'aiguille au repos, dans le repère du boîtier
        Vector3 p0 = Vector3.ProjectOnPlane(repos * axePointe, n);
        if (d.sqrMagnitude < 1e-6f || p0.sqrMagnitude < 1e-6f) return;   // boussole tenue verticale : l'aiguille reste où elle est

        float voulu = Vector3.SignedAngle(p0, d, n) + angleOffset;
        if (!init) { angle = voulu; init = true; }

        // ressort amorti (inertie de l'aiguille)
        float ecart = Mathf.DeltaAngle(angle, voulu);
        vitesseAngle += (ecart * raideur - vitesseAngle * amortissement) * Time.deltaTime;
        angle += vitesseAngle * Time.deltaTime;

        transform.localRotation = Quaternion.AngleAxis(angle, n) * repos;
    }
}
