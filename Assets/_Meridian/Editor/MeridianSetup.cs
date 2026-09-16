using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Meridian.Editor
{
    /// <summary>Explicit, repeatable authoring command; never runs automatically or in a player.</summary>
    public static class MeridianSetup
    {
        public const string Root = "Assets/_Meridian";
        public const string ScenePath = Root + "/Scenes/MainMenu.unity";
        private const string BackgroundPath = Root + "/Art/UI/Meridian_MainMenu_Background.png";
        private static readonly string[] Labels = { "NEW COLONY", "LOAD COLONY", "SETTINGS", "QUIT" };
        private static readonly Vector2 DesignSize = new Vector2(1920f, 1920f * 941f / 1672f);

        [MenuItem("Meridian/Author Main Menu Foundation")]
        public static void Create()
        {
            if (File.Exists(PlanetSelectionAuthoring.ScenePath)) throw new InvalidOperationException("The foundation has been extended. This original setup command would overwrite the planet-selection integration.");
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring.");
            foreach (string folder in new[] { "Scenes", "Settings", "Art/UI", "Art/Fonts", "Prefabs/UI", "Input" })
                Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh();
            ConfigureProject();
            ConfigureRendering();
            ConfigureBackground();

            // These are Unity's redistributable font, shader, and settings resources; no examples are imported.
            if (!AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf"))
            {
                AssetDatabase.importPackageCompleted += FinishFontImport;
                AssetDatabase.ImportPackage("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage", false);
                return;
            }
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var font = CreateFont();
            var actions = CreateInputActions();

            var camera = new GameObject("Menu Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            var canvasObject = new GameObject("MainMenu", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(FittedMenuContent), typeof(MainMenuPresentation));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var bars = MakeRect("Black Letterbox", canvas.transform);
            Stretch(bars);
            var black = bars.gameObject.AddComponent<Image>();
            black.color = Color.black;
            black.raycastTarget = false;

            var content = MakeRect("Fitted Artwork and Menu", canvas.transform);
            canvasObject.GetComponent<FittedMenuContent>().Configure(content, DesignSize);
            var art = MakeRect("Supplied Background (includes title)", content);
            Stretch(art);
            var background = art.gameObject.AddComponent<RawImage>();
            background.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
            background.color = Color.white;
            background.raycastTarget = false;

            var prototype = MakeButton(font);
            string buttonPath = Root + "/Prefabs/UI/MenuButton.prefab";
            var buttonPrefab = PrefabUtility.SaveAsPrefabAsset(prototype, buttonPath);
            UnityEngine.Object.DestroyImmediate(prototype);
            var buttons = new Button[Labels.Length];
            for (int i = 0; i < Labels.Length; i++)
            {
                var row = (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab, content);
                row.name = Labels[i];
                var rect = (RectTransform)row.transform;
                rect.anchoredPosition = new Vector2(92f, -(290f + 77f * i));
                var label = row.GetComponentInChildren<TMP_Text>();
                label.text = Labels[i];
                row.GetComponent<MenuButtonVisual>().Configure(label, row.GetComponentInChildren<CanvasGroup>());
                buttons[i] = row.GetComponent<Button>();
                PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
                PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                PrefabUtility.RecordPrefabInstancePropertyModifications(row.GetComponentInChildren<CanvasGroup>());
            }
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = buttons[(i + buttons.Length - 1) % buttons.Length],
                    selectOnDown = buttons[(i + 1) % buttons.Length]
                };
                PrefabUtility.RecordPrefabInstancePropertyModifications(buttons[i]);
            }
            canvasObject.GetComponent<MainMenuPresentation>().Configure(buttons[0]);

            // Keep the EventSystem with the layout so the reusable screen prefab is self-contained.
            var eventsObject = new GameObject("EventSystem", typeof(EventSystem));
            eventsObject.SetActive(false);
            eventsObject.transform.SetParent(canvas.transform, false);
            var events = eventsObject.GetComponent<EventSystem>();
            events.firstSelectedGameObject = buttons[0].gameObject;
            var module = eventsObject.AddComponent<InputSystemUIInputModule>();
            module.point = module.move = module.leftClick = module.rightClick = module.middleClick = null;
            module.scrollWheel = module.submit = module.cancel = null;
            module.trackedDeviceOrientation = module.trackedDevicePosition = null;
            module.actionsAsset = actions;
            module.point = ActionReference(actions, "Point");
            module.leftClick = ActionReference(actions, "Click");
            module.move = ActionReference(actions, "Navigate");
            module.submit = ActionReference(actions, "Submit");
            module.cancel = module.scrollWheel = module.middleClick = module.rightClick = null;
            module.trackedDeviceOrientation = module.trackedDevicePosition = null;
            module.deselectOnBackgroundClick = false;
            eventsObject.SetActive(true);

            var menuPrefab = PrefabUtility.SaveAsPrefabAsset(canvasObject, Root + "/Prefabs/UI/MainMenu.prefab");
            UnityEngine.Object.DestroyImmediate(canvasObject);
            PrefabUtility.InstantiatePrefab(menuPrefab, scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorBuildSettings.RemoveConfigObject("com.unity.input.settings.actions");

            // Remove only the known template placeholders. Meridian assets use their own generated GUIDs.
            foreach (string path in new[] { "Assets/Scenes", "Assets/TutorialInfo", "Assets/Readme.asset", "Assets/InputSystem_Actions.inputactions" })
                if (AssetDatabase.IsValidFolder(path) || File.Exists(path)) AssetDatabase.DeleteAsset(path);
            KeepTemplateGlobalSettings();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("Meridian foundation authored and saved.");
        }

        private static void FinishFontImport(string packageName)
        {
            AssetDatabase.importPackageCompleted -= FinishFontImport;
            EditorApplication.delayCall += Create;
        }

        private static void ConfigureProject()
        {
            PlayerSettings.productName = "Meridian";
            PlayerSettings.companyName = "DefaultCompany";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, true);
            EditorSettings.serializationMode = SerializationMode.ForceText;
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode3D;
            EditorSettings.lineEndingsForNewScripts = LineEndingsMode.Unix;
            EditorSettings.projectGenerationRootNamespace = "Meridian";
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;
            UnityEditor.VersionControlSettings.mode = "Visible Meta Files";
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            settings.FindProperty("activeInputHandler").intValue = 1;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureRendering()
        {
            string rendererPath = Root + "/Settings/Meridian_Renderer.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (!renderer)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                ResourceReloader.ReloadAllNullIn(renderer, "Packages/com.unity.render-pipelines.universal");
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            string pipelinePath = Root + "/Settings/Meridian_URP.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (!pipeline)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = quality.FindProperty("m_QualitySettings");
            levels.arraySize = 6;
            string[] names = { "Very Low", "Low", "Medium", "High", "Very High", "Ultra" };
            for (int i = 0; i < names.Length; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                level.FindPropertyRelative("name").stringValue = names[i];
                level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
                SetInt(level, "pixelLightCount", new[] { 0, 0, 1, 2, 3, 4 }[i]);
                SetInt(level, "shadows", new[] { 0, 0, 1, 2, 2, 2 }[i]);
                SetInt(level, "shadowResolution", new[] { 0, 0, 0, 1, 2, 2 }[i]);
                SetInt(level, "shadowCascades", new[] { 1, 1, 1, 2, 2, 4 }[i]);
                level.FindPropertyRelative("shadowDistance").floatValue = new[] { 15f, 20f, 20f, 40f, 70f, 150f }[i];
                SetInt(level, "shadowmaskMode", i < 3 ? 0 : 1);
                SetInt(level, "skinWeights", new[] { 1, 2, 2, 2, 4, 255 }[i]);
                SetInt(level, "globalTextureMipmapLimit", i == 0 ? 1 : 0);
                SetInt(level, "anisotropicTextures", new[] { 0, 0, 1, 1, 2, 2 }[i]);
                SetInt(level, "antiAliasing", i == 4 ? 2 : 0);
                SetInt(level, "vSyncCount", i < 2 ? 0 : 1);
                SetInt(level, "particleRaycastBudget", new[] { 4, 16, 64, 256, 1024, 4096 }[i]);
                SetInt(level, "realtimeGICPUUsage", new[] { 25, 25, 25, 50, 50, 100 }[i]);
                level.FindPropertyRelative("lodBias").floatValue = new[] { 0.3f, 0.4f, 0.7f, 1f, 1.5f, 2f }[i];
                foreach (string property in new[] { "realtimeReflectionProbes", "billboardsFaceCameraPosition", "softVegetation" })
                    level.FindPropertyRelative(property).boolValue = i >= 3;
                level.FindPropertyRelative("softParticles").boolValue = i >= 4;
            }
            quality.FindProperty("m_CurrentQuality").intValue = 5;
            var defaults = quality.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < defaults.arraySize; i++)
            {
                var entry = defaults.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue == "Standalone")
                    entry.FindPropertyRelative("second").intValue = 5;
            }
            quality.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.SetQualityLevel(5, true);
        }

        private static void SetInt(SerializedProperty parent, string name, int value) => parent.FindPropertyRelative(name).intValue = value;

        private static void KeepTemplateGlobalSettings()
        {
            // Preserve Unity-created global settings/resources, but give them Meridian paths and names.
            foreach (string name in new[] { "UniversalRenderPipelineGlobalSettings", "DefaultVolumeProfile" })
            {
                string oldPath = "Assets/Settings/" + name + ".asset";
                if (AssetDatabase.LoadMainAssetAtPath(oldPath))
                    AssetDatabase.MoveAsset(oldPath, Root + "/Settings/Meridian_" + name + ".asset");
            }
            if (AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.DeleteAsset("Assets/Settings");
        }

        private static void ConfigureBackground()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(BackgroundPath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static TMP_FontAsset CreateFont()
        {
            string path = Root + "/Art/Fonts/MeridianMenu_SDF.asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font) return font;
            font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf"),
                90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            font.name = "MeridianMenu SDF";
            AssetDatabase.CreateAsset(font, path);
            font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZ ", out string missing);
            if (!string.IsNullOrEmpty(missing)) throw new InvalidOperationException("Missing menu glyphs: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
            AssetDatabase.AddObjectToAsset(font.material, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }

        private static InputActionAsset CreateInputActions()
        {
            string oldPath = Root + "/Input/MenuUI.asset";
            if (AssetDatabase.LoadMainAssetAtPath(oldPath)) AssetDatabase.DeleteAsset(oldPath);
            string path = Root + "/Input/MenuUI.inputactions";
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (asset) return asset;
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "Menu UI";
            var map = asset.AddActionMap("UI");
            map.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position", expectedControlLayout: "Vector2");
            map.AddAction("Click", InputActionType.PassThrough, "<Mouse>/leftButton", expectedControlLayout: "Button");
            var move = map.AddAction("Navigate", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/dpad");
            var submit = map.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter", expectedControlLayout: "Button");
            submit.AddBinding("<Keyboard>/space");
            submit.AddBinding("<Gamepad>/buttonSouth");
            File.WriteAllText(path, asset.ToJson());
            UnityEngine.Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }

        private static InputActionReference ActionReference(InputActionAsset asset, string name) =>
            AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(asset)).OfType<InputActionReference>().First(r => r.action.name == name);

        private static GameObject MakeButton(TMP_FontAsset font)
        {
            var rect = MakeRect("MenuButton", null);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 0.5f);
            rect.sizeDelta = new Vector2(292, 66);
            var face = rect.gameObject.AddComponent<Image>();
            face.color = Color.clear;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            var labelRect = MakeRect("Label", rect);
            Stretch(labelRect);
            labelRect.offsetMin = new Vector2(25, 0);
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = "NEW COLONY";
            label.font = font;
            label.fontSize = 26;
            label.characterSpacing = 18;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            var accentRect = MakeRect("Amber Bracket", rect);
            Stretch(accentRect);
            var accent = accentRect.gameObject.AddComponent<CanvasGroup>();
            accent.blocksRaycasts = false;
            accent.interactable = false;
            Line("Underline", accentRect, new Vector2(0, -28), new Vector2(292, 1.2f));
            Line("Left Bracket", accentRect, new Vector2(0, -20), new Vector2(1.5f, 16));
            Line("Right Bracket", accentRect, new Vector2(290.5f, -20), new Vector2(1.5f, 16));
            rect.gameObject.AddComponent<MenuButtonVisual>().Configure(label, accent);
            return rect.gameObject;
        }

        private static void Line(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = MakeRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color32(255, 183, 88, 255);
            image.raycastTarget = false;
        }

        private static RectTransform MakeRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            if (parent) rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
