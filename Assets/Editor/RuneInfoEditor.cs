using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector de la rune : clique sur l'objet "Rune" (dans la Hierarchy ou la vue Scene)
/// et modifie directement le texte qui s'affichera sur le panneau viking dans le jeu.
/// </summary>
[CustomEditor(typeof(RuneInfo))]
public class RuneInfoEditor : Editor
{
    static GUIStyle styleTexte;
    bool reglages;

    public override void OnInspectorGUI()
    {
        if (styleTexte == null) styleTexte = new GUIStyle(EditorStyles.textArea) { wordWrap = true, richText = false };
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "TEXTE AFFICHÉ PAR LA RUNE\n" +
            "Modifie le titre et le texte de chaque page ci-dessous : c'est exactement ce qui s'affiche au-dessus de la pierre.\n" +
            "Balises possibles : <b>gras</b>, <i>italique</i>, <color=#E8C66A>couleur</color>.",
            MessageType.Info);

        var pages = serializedObject.FindProperty("pages");
        for (int i = 0; i < pages.arraySize; i++)
        {
            var p = pages.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Page {i + 1}", EditorStyles.boldLabel, GUILayout.Width(60));
            GUILayout.FlexibleSpace();
            GUI.enabled = i > 0;
            if (GUILayout.Button("▲", GUILayout.Width(26))) { pages.MoveArrayElement(i, i - 1); break; }
            GUI.enabled = i < pages.arraySize - 1;
            if (GUILayout.Button("▼", GUILayout.Width(26))) { pages.MoveArrayElement(i, i + 1); break; }
            GUI.enabled = true;
            if (GUILayout.Button("Supprimer", GUILayout.Width(80)))
            {
                if (EditorUtility.DisplayDialog("Supprimer la page", $"Supprimer la page {i + 1} ?", "Oui", "Non"))
                { pages.DeleteArrayElementAtIndex(i); break; }
            }
            EditorGUILayout.EndHorizontal();

            var titre = p.FindPropertyRelative("title");
            var corps = p.FindPropertyRelative("body");
            titre.stringValue = EditorGUILayout.TextField("Titre", titre.stringValue);
            EditorGUILayout.LabelField("Texte");
            corps.stringValue = EditorGUILayout.TextArea(corps.stringValue, styleTexte, GUILayout.MinHeight(110));
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Ajouter une page"))
        {
            pages.InsertArrayElementAtIndex(pages.arraySize);
            var p = pages.GetArrayElementAtIndex(pages.arraySize - 1);
            p.FindPropertyRelative("title").stringValue = "Nouveau titre";
            p.FindPropertyRelative("body").stringValue = "Ton texte ici...";
        }
        if (GUILayout.Button("Remettre le texte d'origine"))
        {
            if (EditorUtility.DisplayDialog("Texte d'origine", "Remplacer toutes les pages par l'histoire du drakkar et de la navigation viking ?", "Oui", "Non"))
            {
                serializedObject.ApplyModifiedProperties();
                foreach (var t in targets)
                {
                    Undo.RecordObject(t, "Texte de la rune");
                    ((RuneInfo)t).pages = RuneInfo.DefaultPages();
                    EditorUtility.SetDirty(t);
                }
                serializedObject.Update();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        reglages = EditorGUILayout.Foldout(reglages, "Réglages (lueur, distances, panneau)", true);
        if (reglages) DrawPropertiesExcluding(serializedObject, "m_Script", "pages");

        serializedObject.ApplyModifiedProperties();
    }
}
