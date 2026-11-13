using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// Empêche le joueur VR de traverser les objets avec la tête (en se penchant ou en marchant dans sa pièce) :
/// si la tête rentre dans un collider, tout le rig XR est repoussé, et l'écran s'assombrit un peu.
/// Sécurité : si le joueur tombe à l'eau ou sous le monde, il est replacé (sur le drakkar s'il y était).
/// À mettre sur l'objet "XR Origin (XR Rig)".
/// </summary>
[DefaultExecutionOrder(200)]
public class TeteAntiTraversee : MonoBehaviour
{
    public float rayonTete = 0.16f;
    public LayerMask couches = Physics.DefaultRaycastLayers;
    public float niveauEau = 0.4f;
    [Tooltip("Assombrir la vue quand la tête est dans un mur.")]
    public bool assombrir = true;

    Transform tete;
    CharacterController cc;
    Vector3 teteAvant, origineAvant;
    Vector3 dernierSolSur;
    Transform dernierSupport;
    Vector3 dernierSupportLocal;
    readonly Collider[] proches = new Collider[16];
    Renderer voile;
    Material voileMat;
    float noir;

    void Start()
    {
        var o = GetComponent<XROrigin>();
        tete = o && o.Camera ? o.Camera.transform : (Camera.main ? Camera.main.transform : null);
        cc = GetComponent<CharacterController>();
        if (tete) teteAvant = tete.position;
        origineAvant = transform.position;
        dernierSolSur = transform.position;
        if (assombrir && tete) CreerVoileNoir();
    }

    void CreerVoileNoir()
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "Voile_Collision";
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(tete, false);
        q.transform.localPosition = new Vector3(0, 0, 0.06f);
        q.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) { Destroy(q); return; }
        voileMat = new Material(shader);
        voileMat.SetFloat("_Surface", 1f);   // transparent
        voileMat.SetFloat("_Blend", 0f);
        voileMat.SetFloat("_ZWrite", 0f);
        voileMat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        voileMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        voileMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        voileMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        voileMat.renderQueue = 4000;
        voileMat.SetColor("_BaseColor", new Color(0, 0, 0, 0));
        voile = q.GetComponent<Renderer>();
        voile.sharedMaterial = voileMat;
        voile.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        voile.enabled = false;
    }

    bool Ignorer(Collider c)
    {
        if (c == null || c.isTrigger) return true;
        if (c.transform.IsChildOf(transform)) return true;                 // le joueur lui-même (mains, objets tenus)
        if (c.attachedRigidbody && !c.attachedRigidbody.isKinematic) return true; // objets que l'on peut prendre
        if (c.GetComponent<CharacterController>()) return true;
        return false;
    }

    void LateUpdate()
    {
        if (tete == null) return;
        Vector3 p = tete.position;
        Vector3 poussee = Vector3.zero;

        // 1) traversée rapide d'un mur entre deux images (mouvement propre de la tête, sans le transport par le bateau)
        Vector3 d = (p - teteAvant) - (transform.position - origineAvant);
        Vector3 depart = p - d;
        if (d.sqrMagnitude > 1e-6f && d.magnitude < 2f &&
            Physics.SphereCast(depart, rayonTete * 0.5f, d.normalized, out RaycastHit hit, d.magnitude, couches, QueryTriggerInteraction.Ignore)
            && !Ignorer(hit.collider))
        {
            Vector3 stop = depart + d.normalized * Mathf.Max(0f, hit.distance - 0.02f);
            poussee += stop - p;
        }
        else
        {
            // 2) tête en contact : on la ressort
            int n = Physics.OverlapSphereNonAlloc(p, rayonTete, proches, couches, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = proches[i];
                if (Ignorer(c)) continue;
                if (Physics.ComputePenetration(SphereRef(), p, Quaternion.identity, c, c.transform.position, c.transform.rotation, out Vector3 dir, out float dist))
                    poussee += dir * dist;
            }
        }
        poussee.y = 0f;   // on ne repousse qu'à l'horizontale (pas de "saut" vers le haut)
        if (poussee.sqrMagnitude > 1e-6f)
        {
            bool actif = cc && cc.enabled;
            if (actif) cc.enabled = false;
            transform.position += poussee;
            if (actif) cc.enabled = true;
        }
        teteAvant = tete.position;
        origineAvant = transform.position;

        // assombrissement
        noir = Mathf.MoveTowards(noir, poussee.sqrMagnitude > 1e-6f ? 0.85f : 0f, Time.deltaTime * 4f);
        if (voile)
        {
            voile.enabled = noir > 0.01f;
            if (voile.enabled) voileMat.SetColor("_BaseColor", new Color(0, 0, 0, noir));
        }

        SecuriteChute();
    }

    SphereCollider sphereRef;
    SphereCollider SphereRef()
    {
        if (sphereRef == null)
        {
            var go = new GameObject("Tete_Collision_Ref");
            go.layer = 2;   // Ignore Raycast
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.down * 50f;   // à l'écart : sert uniquement de forme pour ComputePenetration
            sphereRef = go.AddComponent<SphereCollider>();
            sphereRef.radius = rayonTete;
            sphereRef.isTrigger = true;
        }
        return sphereRef;
    }

    void SecuriteChute()
    {
        // mémorise le dernier sol sûr (et l'objet qui le porte, ex. le drakkar en mouvement)
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit sol, 1.2f, couches, QueryTriggerInteraction.Ignore)
            && !Ignorer(sol.collider) && sol.point.y > niveauEau + 0.05f)
        {
            dernierSupport = sol.collider.transform;
            dernierSupportLocal = dernierSupport.InverseTransformPoint(sol.point);
            dernierSolSur = sol.point;
        }
        if (transform.position.y < niveauEau - 1.2f)
        {
            Vector3 cible = dernierSupport ? dernierSupport.TransformPoint(dernierSupportLocal) : dernierSolSur;
            bool actif = cc && cc.enabled;
            if (actif) cc.enabled = false;
            transform.position = cible + Vector3.up * 0.1f;
            if (actif) cc.enabled = true;
        }
    }
}
