using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IPhysicsHub.Toolkit.Editor
{
    /// <summary>
    /// Installs the 2D/3D toggle (script + icons) from the package, then builds a wired toggle
    /// button in the open scene (Canvas + button + icon, with the controller hooked up).
    /// </summary>
    internal class Toggle2D3DWindow : ToolkitWindowBase
    {
        private static readonly string[] ToggleIcons = { "2d-icon.png", "3d-icon.png" };

        private string _scriptDest = "Assets/Scripts";
        private string _iconsDest = "Assets/Icons";

        // Controller the installed toggle binds to (rewritten into the script on install).
        private string _controllerFilter = "Camera";
        private List<Type> _controllerTypes = new List<Type>();
        private string[] _controllerNames = new string[0];
        private int _controllerIndex;

        [MenuItem("Tools/iPhysicsHub/2D-3D Toggle")]
        private static void Open()
        {
            var w = GetWindow<Toggle2D3DWindow>("2D/3D Toggle");
            w.minSize = new Vector2(400, 300);
            w.Show();
        }

        private void OnEnable() => RefreshControllerTypes();

        private void RefreshControllerTypes()
        {
            _controllerTypes = ToolkitCore.GetProjectControllerTypes(_controllerFilter, ToolkitCore.ToggleClassName);
            _controllerNames = _controllerTypes.Select(ToolkitCore.NiceTypeName).ToArray();
            _controllerIndex = ToolkitCore.LatestIndexByName(_controllerTypes);
        }

        private void OnGUI()
        {
            EditorGUIUtility.labelWidth = ToolkitCore.LabelW;

            EditorGUILayout.LabelField("2D/3D Toggle", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Install the files, let them compile, then add the wired button to the open scene.",
                MessageType.None);

            EditorGUILayout.Space(4);
            _scriptDest = EditorGUILayout.TextField("Script destination", _scriptDest);
            _iconsDest = EditorGUILayout.TextField("Icons destination", _iconsDest);

            EditorGUILayout.Space(2);
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                _controllerFilter = EditorGUILayout.TextField("Controller filter", _controllerFilter);
                if (check.changed) RefreshControllerTypes();
            }
            bool haveController = _controllerTypes.Count > 0;
            if (haveController)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _controllerIndex = EditorGUILayout.Popup("Controller type", _controllerIndex, _controllerNames);
                    if (GUILayout.Button("↻", GUILayout.Width(26), GUILayout.Height(ToolkitCore.ButtonH)))
                        RefreshControllerTypes();
                }
                EditorGUILayout.LabelField("The installed toggle binds to this controller.", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "No controller type found. Install one first via Camera Updater (or clear the filter).",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(2);
            using (new EditorGUI.DisabledScope(!haveController))
            {
                if (GUILayout.Button("1. Install toggle files", GUILayout.Height(ToolkitCore.ButtonH)))
                    InstallToggleFiles();
            }

            bool compiled = ToolkitCore.FindTypeByName(ToolkitCore.ToggleClassName) != null;
            using (new EditorGUI.DisabledScope(!compiled))
            {
                if (GUILayout.Button("2. Add toggle to scene", GUILayout.Height(ToolkitCore.ButtonH)))
                    AddToggleToScene();
            }
            if (!compiled)
                EditorGUILayout.LabelField("Install the files and let them compile first.", EditorStyles.miniLabel);

            EditorGUILayout.Space(2);
            DrawStatus();
        }

        private void InstallToggleFiles()
        {
            string dir = ToolkitCore.GetTemplatesDir();
            if (dir == null) { _message = "Could not resolve the package path."; _messageType = MessageType.Error; return; }

            string scriptSrc = Path.Combine(dir, ToolkitCore.ToggleScriptName);
            if (!File.Exists(scriptSrc)) { _message = ToolkitCore.ToggleScriptName + " not found in the package Templates~."; _messageType = MessageType.Error; return; }
            if (_controllerTypes.Count == 0) { _message = "Install a controller first via Camera Updater."; _messageType = MessageType.Warning; return; }

            string text;
            try { text = File.ReadAllText(scriptSrc); }
            catch (Exception e) { _message = "Could not read the toggle template: " + e.Message; _messageType = MessageType.Error; return; }

            // Retarget the toggle's controller type to the selected one (field type + auto-find).
            // Use the namespace-qualified name so a namespaced controller still resolves in the
            // generated source (no matching `using` is added).
            string chosen = ToolkitCore.FullTypeName(_controllerTypes[_controllerIndex]);
            string declared = ToolkitCore.GetFieldType(text, "cameraController");
            if (declared != null && declared != chosen)
                text = ToolkitCore.RenameIdentifierInCode(text, declared, chosen);

            InstallResult r = ToolkitCore.InstallScriptText(text, _scriptDest, null, ToolkitCore.ToggleClassName);
            ApplyResult(r);
            // Hard error (bad path / unresolvable package collision): stop. A duplicate-class
            // collision that offers an in-place update (CollisionText != null) still installs the
            // icons below, so 'Update existing in place' has everything it needs.
            if (!r.Ok && r.CollisionText == null) return;

            string iconsDest = string.IsNullOrWhiteSpace(_iconsDest)
                ? "Assets/Icons" : _iconsDest.Trim().Replace('\\', '/').TrimEnd('/');
            if (iconsDest != "Assets" && !iconsDest.StartsWith("Assets/"))
            { _message = "Icons destination must be inside the project's Assets/ folder."; _messageType = MessageType.Error; return; }

            int icons = 0;
            foreach (string icon in ToggleIcons)
            {
                string iconSrc = Path.Combine(dir, "Icons", icon);
                if (!File.Exists(iconSrc)) continue;
                string iconRel = iconsDest + "/" + icon;
                string iconAbs = Path.Combine(Directory.GetCurrentDirectory(), iconRel);
                if (!File.Exists(iconAbs)) // copy png + .meta (preserves sprite import + GUID); skip if present
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(iconAbs));
                    File.Copy(iconSrc, iconAbs, false);
                    if (File.Exists(iconSrc + ".meta")) File.Copy(iconSrc + ".meta", iconAbs + ".meta", false);
                }
                AssetDatabase.ImportAsset(iconRel, ImportAssetOptions.ForceUpdate);
                icons++;
            }

            AssetDatabase.Refresh();
            if (r.Ok)
            {
                _message = $"Installed {r.InstalledPath} (bound to {chosen}) + {icons} icon(s). " +
                    "After it compiles, click '2. Add toggle to scene'.";
                _messageType = MessageType.Info;
            }
            else
            {
                // Script blocked by the duplicate-class guard; icons are in. Keep the Warning +
                // 'Update existing in place' button (collision state preserved by ApplyResult).
                _message = r.Message + $"  ({icons} icon(s) installed.)";
            }
        }

        private void AddToggleToScene()
        {
            Type toggleType = ToolkitCore.FindTypeByName(ToolkitCore.ToggleClassName);
            if (toggleType == null)
            { _message = "Toggle type isn't compiled yet — install the files and wait for the recompile."; _messageType = MessageType.Warning; return; }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Add 2D/3D Toggle");
            int group = Undo.GetCurrentGroup();

            Canvas canvas = FindObjectOfType<Canvas>();
            GameObject canvasGo;
            if (canvas == null)
            {
                canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
            }
            else canvasGo = canvas.gameObject;

            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            }

            int uiLayer = LayerMask.NameToLayer("UI");

            var buttonGo = new GameObject("Toggle2D3DButton",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(buttonGo, "Create Toggle Button");
            if (uiLayer >= 0) buttonGo.layer = uiLayer;
            buttonGo.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)buttonGo.transform;
            rt.anchorMin = rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-92, -92);
            rt.sizeDelta = new Vector2(80, 80);
            var bg = buttonGo.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 80f / 255f);
            var button = buttonGo.GetComponent<Button>();
            button.targetGraphic = bg;

            var iconGo = new GameObject("IconImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            if (uiLayer >= 0) iconGo.layer = uiLayer;
            iconGo.transform.SetParent(buttonGo.transform, false);
            var irt = (RectTransform)iconGo.transform;
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta = new Vector2(-20, -20);
            var iconImage = iconGo.GetComponent<Image>();
            iconImage.color = Color.black; // match the reference; runtime Start() only swaps the sprite, not the color

            Sprite sprite3D = LoadIcon("3d-icon.png");
            Sprite sprite2D = LoadIcon("2d-icon.png");
            iconImage.sprite = sprite3D; // edit-time preview; runtime resets it in Start()

            Component toggle = Undo.AddComponent(buttonGo, toggleType);
            var so = new SerializedObject(toggle);
            SetRef(so, "button", button);
            SetRef(so, "iconImage", iconImage);
            SetRef(so, "sprite3D", sprite3D);
            SetRef(so, "sprite2D", sprite2D);
            UnityEngine.Object ctrl = null;
            FieldInfo ctrlField = toggleType.GetField("cameraController");
            if (ctrlField != null)
            {
                ctrl = FindObjectOfType(ctrlField.FieldType) as UnityEngine.Object;
                SetRef(so, "cameraController", ctrl);
            }
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(buttonGo);
            EditorSceneManager.MarkSceneDirty(buttonGo.scene);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = buttonGo;

            _message = "Added Toggle2D3DButton under " + canvasGo.name +
                (ctrl != null ? " (wired to the scene controller)." : " (no controller found — it auto-finds at Play).");
            _messageType = MessageType.Info;
        }

        private Sprite LoadIcon(string fileName)
        {
            string iconsDest = string.IsNullOrWhiteSpace(_iconsDest)
                ? "Assets/Icons" : _iconsDest.Trim().Replace('\\', '/').TrimEnd('/');
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(iconsDest + "/" + fileName);
            if (s != null) return s;
            foreach (string guid in AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(fileName) + " t:Sprite"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(p) == fileName)
                {
                    var sp = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                    if (sp != null) return sp;
                }
            }
            return null;
        }

        private static void SetRef(SerializedObject so, string prop, UnityEngine.Object value)
        {
            SerializedProperty p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = value;
        }
    }
}
