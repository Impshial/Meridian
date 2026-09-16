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
            Require(buttons.Length == 4, "Expected exactly four buttons.");
            Require(buttons.Select(button => button.GetComponentInChildren<TMP_Text>().text)
                .SequenceEqual(new[] { "NEW COLONY", "LOAD COLONY", "SETTINGS", "QUIT" }), "Incorrect labels or order.");
            Require(buttons[0].onClick.GetPersistentEventCount() == 1 && buttons[0].onClick.GetPersistentMethodName(0) == "OpenPlanetSelection", "NEW COLONY entry is missing.");
            Require(buttons.Skip(1).All(button => button.onClick.GetPersistentEventCount() == 0), "A placeholder action callback was assigned.");
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
                "\nNEW COLONY entry; three inert placeholders; valid saved references, font glyphs, source texture and URP quality assignments.\n");
            Debug.Log("Meridian saved main menu validation passed.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
