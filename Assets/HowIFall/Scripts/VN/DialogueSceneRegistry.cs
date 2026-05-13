using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "How I Fall/VN/Dialogue Scene Registry", fileName = "DialogueSceneRegistry")]
public class DialogueSceneRegistry : ScriptableObject
{
    public List<DialogueSceneData> scenes = new List<DialogueSceneData>();

    public DialogueSceneData FindById(string sceneId)
    {
        if (string.IsNullOrEmpty(sceneId))
        {
            return null;
        }

        for (int i = 0; i < scenes.Count; i++)
        {
            DialogueSceneData scene = scenes[i];

            if (scene != null && scene.sceneId == sceneId)
            {
                return scene;
            }
        }

        return null;
    }
}
