using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IPhysicsHub.Toolkit.Editor
{
    /// <summary>
    /// Installs the UI Hit Area Visualizer script from the package, then adds it to the scene's Canvas.
    /// </summary>
    internal class UIEnhancerWindow : ToolkitWindowBase
    {
        // Negative raycast padding expands the hit area outward by this many pixels on every edge.
        private const float DefaultPadding = -10f;

        private string _scriptDest = "Assets/Scripts";

        [MenuItem("Tools/iPhysicsHub/UI Enhancer")]
        private static void Open()
        {
            var w = GetWindow<UIEnhancerWindow>("UI Enhancer");
            w.minSize = new Vector2(380, 220);
            w.Show();
        }

        private void OnGUI()
        {
            EditorGUIUtility.labelWidth = ToolkitCore.LabelW;

            EditorGUILayout.LabelField("UI Enhancer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Install the UI Hit Area Visualizer, then add it to a Canvas. In Play mode, press F12 to toggle debug overlays.",
                MessageType.None);

            EditorGUILayout.Space(4);
            _scriptDest = EditorGUILayout.TextField("Script destination", _scriptDest);

            EditorGUILayout.Space(2);
            if (GUILayout.Button("1. Install UI Hit Area Visualizer", GUILayout.Height(ToolkitCore.ButtonH)))
                InstallVisualizer();

            bool compiled = ToolkitCore.FindTypeByName(ToolkitCore.VisualizerClassName) != null;
            using (new EditorGUI.DisabledScope(!compiled))
            {
                if (GUILayout.Button("2. Add to Canvas", GUILayout.Height(ToolkitCore.ButtonH)))
                    AddToCanvas();
            }
            if (!compiled)
                EditorGUILayout.LabelField("Install the script and let it compile first.", EditorStyles.miniLabel);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Raycast Padding", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"Apply {DefaultPadding} padding to all raycast-target UI Graphics under interactable Selectables in the Canvas.",
                MessageType.None);

            bool hasCanvas = FindObjectOfType<Canvas>() != null;
            using (new EditorGUI.DisabledScope(!hasCanvas))
            {
                if (GUILayout.Button($"Apply {DefaultPadding} Raycast Padding to Canvas", GUILayout.Height(ToolkitCore.ButtonH)))
                    ApplyRaycastPadding();
            }
            if (!hasCanvas)
                EditorGUILayout.LabelField("No Canvas in the scene.", EditorStyles.miniLabel);

            EditorGUILayout.Space(2);
            DrawStatus();
        }

        private void InstallVisualizer()
        {
            string dir = ToolkitCore.GetTemplatesDir();
            if (dir == null) { _message = "Could not resolve the package path."; _messageType = MessageType.Error; return; }

            string scriptSrc = Path.Combine(dir, ToolkitCore.VisualizerScriptName);
            if (!File.Exists(scriptSrc)) { _message = ToolkitCore.VisualizerScriptName + " not found in the package Templates~."; _messageType = MessageType.Error; return; }

            string p = Install(scriptSrc, _scriptDest, null, null);
            if (p != null)
                _message = "Installed " + p + ". Wait for the recompile, then click '2. Add to Canvas'.";
        }

        private void AddToCanvas()
        {
            Type visualizerType = ToolkitCore.FindTypeByName(ToolkitCore.VisualizerClassName);
            if (visualizerType == null)
            { _message = "Visualizer type isn't compiled yet — install the script and wait for the recompile."; _messageType = MessageType.Warning; return; }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Add UI Hit Area Visualizer");
            int group = Undo.GetCurrentGroup();

            Canvas canvas = FindObjectOfType<Canvas>();
            GameObject canvasGo;
            if (canvas == null)
            {
                canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
            }
            else
            {
                canvasGo = canvas.gameObject;
            }

            // A GraphicRaycaster needs an EventSystem to deliver input; add one if the scene lacks it.
            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            }

            // Check if already attached
            if (canvasGo.GetComponent(visualizerType) != null)
            {
                _message = "The Canvas already has a UIHitAreaVisualizer component.";
                _messageType = MessageType.Warning;
                Undo.CollapseUndoOperations(group);
                return;
            }

            Undo.AddComponent(canvasGo, visualizerType);
            EditorUtility.SetDirty(canvasGo);
            if (canvasGo.scene.IsValid()) EditorSceneManager.MarkSceneDirty(canvasGo.scene);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = canvasGo;

            _message = "Added UIHitAreaVisualizer to " + canvasGo.name + ". Press Play and hit F12 to toggle debug overlays.";
            _messageType = MessageType.Info;
        }

        private void ApplyRaycastPadding()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            { _message = "No Canvas found in the scene."; _messageType = MessageType.Warning; return; }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Apply Raycast Padding");
            int group = Undo.GetCurrentGroup();

            Selectable[] selectables = canvas.GetComponentsInChildren<Selectable>(true);
            int selCount = 0;
            int gfxCount = 0;
            var padded = new HashSet<Graphic>();

            foreach (Selectable sel in selectables)
            {
                if (!sel.interactable)
                    continue;

                bool modified = false;
                Graphic[] graphics = sel.GetComponentsInChildren<Graphic>(true);
                foreach (Graphic g in graphics)
                {
                    if (!g.raycastTarget || !padded.Add(g))
                        continue;

                    Undo.RecordObject(g, "Apply Raycast Padding");
                    g.raycastPadding = new Vector4(DefaultPadding, DefaultPadding, DefaultPadding, DefaultPadding);
                    EditorUtility.SetDirty(g);
                    gfxCount++;
                    modified = true;
                }
                if (modified) selCount++;
            }

            if (canvas.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

            Undo.CollapseUndoOperations(group);

            if (gfxCount > 0)
            {
                _message = $"Applied {DefaultPadding} raycast padding to {gfxCount} Graphic(s) on {selCount} interactable Selectable(s).";
                _messageType = MessageType.Info;
            }
            else
            {
                _message = "No raycast-target Graphics found on interactable Selectables in the Canvas.";
                _messageType = MessageType.Warning;
            }
        }
    }
}
