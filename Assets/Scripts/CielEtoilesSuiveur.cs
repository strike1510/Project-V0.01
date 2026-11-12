
using UnityEngine;

public class CielEtoilesSuiveur : MonoBehaviour
{
    [Header("Joueur")]
    public Transform cameraJoueur;

    [Header("Orientation du ciel")]
    public float rotationNord = 0f;

    [Header("Position")]
    public float decalageVertical = 0f;

    void LateUpdate()
    {
        if (cameraJoueur == null)
            return;

        // Suit uniquement la position du joueur
        transform.position =
            cameraJoueur.position +
            Vector3.up * decalageVertical;

        // Conserve une orientation fixe dans le monde
        transform.rotation =
            Quaternion.Euler(0f, rotationNord, 0f);
    }
}
