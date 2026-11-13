using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Gestion de la boussole qu'on peut prendre en main (VR) :
/// - plus de rotation forcée : on la tient comme on veut, l'aiguille (CompassNeedle) indique le Nord ;
/// - posée sur le drakkar, elle reste sur le pont et voyage avec lui ;
/// - si elle tombe à l'eau ou se perd, elle revient à sa place (ou dans la main du joueur s'il est en mer).
/// À mettre sur l'objet "boussole" (avec XR Grab Interactable + Rigidbody).
/// </summary>
[DefaultExecutionOrder(0)]
public class CompassBodyLock : MonoBehaviour
{
    public float niveauEau = 0.4f;
    [Tooltip("Distance au joueur au-delà de laquelle la boussole revient (si personne ne la tient).")]
    public float distanceMax = 60f;

    XRGrabInteractable grab;
    Rigidbody rb;
    Vector3 posDepart;
    Quaternion rotDepart;
    Transform parentDepart;
    bool tenue;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        posDepart = transform.position;
        rotDepart = transform.rotation;
        parentDepart = transform.parent;
        if (rb)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            // au départ elle est posée : on la fige pour qu'elle ne glisse pas
            rb.isKinematic = true;
        }
        if (grab)
        {
            grab.selectEntered.AddListener(_ => { tenue = true; });
            grab.selectExited.AddListener(_ => { tenue = false; StartCoroutine(Poser()); });
        }
    }

    IEnumerator Poser()
    {
        // on attend que XRI ait fini de relâcher l'objet
        yield return null;
        if (tenue) yield break;
        if (rb) { rb.isKinematic = false; rb.useGravity = true; }
        // on la laisse tomber un instant puis on regarde où elle a atterri
        float t = 0f;
        while (t < 3f && !tenue)
        {
            t += Time.deltaTime;
            if (Physics.Raycast(transform.position + Vector3.up * 0.05f, Vector3.down, out RaycastHit hit, 0.25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.collider.attachedRigidbody != rb)
            {
                var bateau = hit.collider.GetComponentInParent<Ordre>();
                if (bateau != null || (rb && rb.linearVelocity.sqrMagnitude < 0.01f && t > 0.3f))
                {
                    // posée : sur le bateau elle devient passagère du drakkar
                    if (rb) { rb.isKinematic = true; }
                    transform.SetParent(bateau ? bateau.transform : parentDepart, true);
                    yield break;
                }
            }
            yield return null;
        }
    }

    void LateUpdate()
    {
        if (tenue) return;
        bool perdue = transform.position.y < niveauEau - 0.5f;
        var cam = Camera.main;
        if (!perdue && cam && Vector3.Distance(cam.transform.position, transform.position) > distanceMax) perdue = true;
        if (perdue) Revenir(cam);
    }

    void Revenir(Camera cam)
    {
        if (rb) { rb.isKinematic = true; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        var bateau = FindAnyObjectByType<Ordre>();
        if (bateau && bateau.JoueurABord && cam)
        {
            // en mer : elle réapparaît posée devant le joueur, sur le pont
            Vector3 avant = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            Vector3 p = cam.transform.position + avant * 0.6f;
            if (Physics.Raycast(p, Vector3.down, out RaycastHit hit, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                p = hit.point + Vector3.up * 0.05f;
            transform.SetParent(bateau.transform, true);
            transform.SetPositionAndRotation(p, rotDepart);
        }
        else
        {
            transform.SetParent(parentDepart, true);
            transform.SetPositionAndRotation(posDepart, rotDepart);
        }
    }
}
