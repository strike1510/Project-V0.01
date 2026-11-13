using UnityEngine;

/// <summary>
/// Le ciel étoilé suit la position du joueur (les étoiles sont "à l'infini") mais garde une orientation
/// fixe : l'Étoile polaire reste toujours au NORD, où que l'on soit sur la mer.
/// </summary>
public class CielEtoilesSuiveur : MonoBehaviour
{
    [Header("Joueur (caméra principale si vide)")]
    public Transform cameraJoueur;

    [Header("Orientation du ciel")]
    public float rotationNord = 0f;

    [Header("Position")]
    public float decalageVertical = 0f;

    void LateUpdate()
    {
        if (cameraJoueur == null || !cameraJoueur.gameObject.activeInHierarchy)
        {
            var cam = Camera.main;
            if (cam == null) return;
            cameraJoueur = cam.transform;
        }

        transform.position = cameraJoueur.position + Vector3.up * decalageVertical;
        transform.rotation = Quaternion.Euler(0f, rotationNord, 0f);
    }
}
