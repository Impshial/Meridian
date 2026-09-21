using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Meridian.Editor
{
    /// <summary>A focused saved-asset check. Does not regenerate or modify the menu.</summary>
    public static class MeridianValidation
    {
        [MenuItem("Meridian/Validate Saved Main Menu")]
        public static void ValidateSavedScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before checking saved assets.");
            var scene = EditorSceneManager.OpenScene(MeridianSetup.ScenePath);
            var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
            Require(components.All(component => component), "Missing script.");
            var buttons = components.OfType<Button>().OrderBy(button => button.transform.GetSiblingIndex()).ToArray();
            Require(buttons.Length == 5, "Expected exactly five menu actions.");
            Require(buttons.Select(button => button.GetComponentInChildren<TMP_Text>().text)
                .SequenceEqual(new[] { "NEW COLONY", "CONTINUE", "LOAD COLONY", "SETTINGS", "QUIT" }), "Incorrect labels or order.");
            Require(buttons[0].onClick.GetPersistentEventCount() == 1 && buttons[0].onClick.GetPersistentMethodName(0) == "OpenPlanetSelection", "NEW COLONY entry is missing.");
            string[] actions={"OpenPlanetSelection","ContinueColony","LoadColony","OpenSettings","QuitGame"};
            Require(buttons.Select((button,i)=>button.onClick.GetPersistentEventCount()==1&&button.onClick.GetPersistentMethodName(0)==actions[i]).All(valid=>valid),"A menu action is missing.");
            Require(buttons.All(button => button.GetComponentInChildren<CanvasGroup>().alpha == 0), "Saved menu contains a hover bracket before pointer entry.");
            Require(buttons.All(button => button.GetComponentInChildren<TMP_Text>().color == (Color)new Color32(225,224,219,255)), "Saved menu contains a highlighted idle label.");
            var background = components.OfType<RawImage>().Single();
            Require(background.texture && background.texture.width == 1672 && background.texture.height == 941, "Background resolution changed.");
            Require(background.color == Color.white && !background.raycastTarget, "Background tint or raycast changed.");
            var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            Require(pipeline && pipeline.scriptableRenderer != null, "Invalid URP assignment.");
            for (int i = 0; i < QualitySettings.names.Length; i++)
                Require(QualitySettings.GetRenderPipelineAssetAt(i) == pipeline, "Quality level has a different pipeline: " + i);
            foreach (var component in components)
            {
                var property = new SerializedObject(component).GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                        Require(property.objectReferenceValue || property.objectReferenceEntityIdValue == default,
                            "Missing reference: " + component.name + "/" + property.propertyPath);
            }
            foreach (var label in buttons.Select(button => button.GetComponentInChildren<TMP_Text>()))
                Require(label.text.All(character => character == ' ' || label.font.HasCharacter(character)), "Missing label glyph.");
            Require(EditorBuildSettings.scenes.Length == 3 && EditorBuildSettings.scenes.All(s => s.enabled) &&
                EditorBuildSettings.scenes[0].path == scene.path && EditorBuildSettings.scenes[1].path == PlanetSelectionAuthoring.ScenePath &&
                EditorBuildSettings.scenes[2].path == LandingSiteAuthoring.ScenePath, "Three-scene startup configuration changed.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/MainMenuValidation.txt", "PASS\nUnity " + Application.unityVersion +
                "\nScene " + scene.path + "\nBackend " + PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) +
                "\nAPI " + PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.Standalone) +
                "\nFive functional menu actions; valid saved references, font glyphs, source texture and URP quality assignments.\n");
            Debug.Log("Meridian saved main menu validation passed.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
