using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IPhysicsHub.Toolkit.Editor
{
    /// <summary>
    /// Installs/captures the central camera-controller source and upgrades an old controller
    /// component to a new one, copying matching inspector values via reflection. Class names
    /// need not match — the user picks the old and new types.
    /// </summary>
    internal class CameraUpdaterWindow : ToolkitWindowBase
    {
        private GameObject _target;
        private List<Component> _oldCandidates = new List<Component>();
        private int _oldIndex;

        private List<Type> _newCandidates = new List<Type>();
        private string[] _newNames = Array.Empty<string>();
        private int _newIndex;

        private string _newFilter = "Camera";
        private List<GameObject> _sceneTargets = new List<GameObject>();
        private string[] _targetNames = Array.Empty<string>();
        private int _targetIndex = -1;

        private Vector2 _reportScroll;
        private string _report;
        private MessageType _reportType = MessageType.Info;

        private string[] _templatePaths = Array.Empty<string>();
        private string[] _templateNames = Array.Empty<string>();
        private int _templateIndex;
        private int _latestTemplateIndex;
        private string _installDest = "Assets/Scripts";
        private MonoScript _captureScript;
        private string _captureClassName = "";
        private bool _showSource = true;
        private bool _showMaintainer;
        private bool _maintainerUnlocked;            // session-only gate for capture
        private string _maintainerPwd = "";
        private const string MaintainerPassword = "dnhnuwan"; // soft gate, not real security

        [MenuItem("Tools/iPhysicsHub/Camera Updater")]
        private static void Open()
        {
            var w = GetWindow<CameraUpdaterWindow>("Camera Updater");
            w.minSize = new Vector2(400, 460);
            w.Show();
        }

        private void OnEnable()
        {
            if (_target == null && Selection.activeGameObject != null)
                _target = Selection.activeGameObject;
            RefreshTargets();
            RefreshNewTypes();
            RefreshOldCandidates();
            RefreshTemplates();
        }

        private void OnSelectionChange()
        {
            if (Selection.activeGameObject != null && Selection.activeGameObject != _target)
            {
                _target = Selection.activeGameObject;
                RefreshTargets();
                RefreshOldCandidates();
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUIUtility.labelWidth = ToolkitCore.LabelW;

            EditorGUILayout.LabelField("Camera Updater", EditorStyles.boldLabel);
            DrawMaintainer();
            EditorGUILayout.Space(4);
            DrawControllerSource();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Upgrade controller component", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _newFilter = EditorGUILayout.TextField("Filter", _newFilter);
            if (EditorGUI.EndChangeCheck())
            {
                RefreshTargets();
                RefreshNewTypes();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (_targetNames.Length == 0)
                {
                    EditorGUILayout.HelpBox(
                        string.IsNullOrWhiteSpace(_newFilter)
                            ? "No GameObjects in the open scene."
                            : $"No scene objects have a component matching \"{_newFilter}\". Clear the filter to see all.",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    _targetIndex = EditorGUILayout.Popup("Target GameObject", _targetIndex, _targetNames);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _target = (_targetIndex >= 0 && _targetIndex < _sceneTargets.Count)
                            ? _sceneTargets[_targetIndex] : null;
                        RefreshOldCandidates();
                    }
                }
                if (GUILayout.Button("↻", GUILayout.Width(26), GUILayout.Height(ToolkitCore.ButtonH)))
                {
                    RefreshTargets();
                    RefreshNewTypes();
                }
            }

            using (new EditorGUI.DisabledScope(_target == null))
            {
                if (_oldCandidates.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        _target == null ? "Assign a target GameObject."
                                        : "The target has no MonoBehaviour components to replace.",
                        MessageType.Warning);
                }
                else
                {
                    var oldNames = _oldCandidates
                        .Select(c => c == null ? "<missing script>" : c.GetType().Name).ToArray();
                    _oldIndex = Mathf.Clamp(_oldIndex, 0, oldNames.Length - 1);
                    _oldIndex = EditorGUILayout.Popup("Old controller", _oldIndex, oldNames);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (_newNames.Length == 0)
                    {
                        EditorGUILayout.HelpBox(
                            string.IsNullOrWhiteSpace(_newFilter)
                                ? "No project MonoBehaviour types found."
                                : $"No types match \"{_newFilter}\". Clear the filter to see all.",
                            MessageType.Warning);
                    }
                    else
                    {
                        _newIndex = Mathf.Clamp(_newIndex, 0, _newNames.Length - 1);
                        _newIndex = EditorGUILayout.Popup("New controller", _newIndex, _newNames);
                    }
                    if (GUILayout.Button("↻", GUILayout.Width(26), GUILayout.Height(ToolkitCore.ButtonH)))
                        RefreshNewTypes();
                }
            }

            EditorGUILayout.Space(4);
            bool canUpgrade = CanUpgrade(out string reason);
            using (new EditorGUI.DisabledScope(!canUpgrade))
            {
                if (GUILayout.Button("Upgrade", GUILayout.Height(34)))
                    Upgrade();
            }
            if (!canUpgrade && !string.IsNullOrEmpty(reason))
                EditorGUILayout.LabelField(reason, EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(_report))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);
                _reportScroll = EditorGUILayout.BeginScrollView(_reportScroll);
                EditorGUILayout.HelpBox(_report, _reportType);
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space(4);
            DrawStatus(); // shared status + duplicate-class "update in place"
        }

        private void DrawMaintainer()
        {
            _showMaintainer = EditorGUILayout.Foldout(_showMaintainer, "Maintainer — capture to package", true);
            if (!_showMaintainer) return;
            EditorGUI.indentLevel++;

            if (!_maintainerUnlocked)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _maintainerPwd = EditorGUILayout.PasswordField("Password", _maintainerPwd);
                    if (GUILayout.Button("Enable", GUILayout.Width(90), GUILayout.Height(ToolkitCore.ButtonH)))
                    {
                        if (_maintainerPwd == MaintainerPassword)
                        {
                            _maintainerUnlocked = true;
                            _message = "Maintainer unlocked.";
                            _messageType = MessageType.Info;
                        }
                        else
                        {
                            _message = "Incorrect password.";
                            _messageType = MessageType.Error;
                        }
                        _maintainerPwd = "";
                        GUI.FocusControl(null);
                    }
                }
                EditorGUILayout.LabelField("Locked — enter the password to enable capture.", EditorStyles.miniLabel);
            }
            else
            {
                _captureScript = (MonoScript)EditorGUILayout.ObjectField(
                    "Capture script", _captureScript, typeof(MonoScript), false);
                _captureClassName = EditorGUILayout.TextField("Save as class (optional)", _captureClassName);
                if (GUILayout.Button("Save to Package", GUILayout.Height(ToolkitCore.ButtonH)))
                    CaptureToPackage();
            }

            EditorGUI.indentLevel--;
        }

        // ----- Controller source -----

        private void DrawControllerSource()
        {
            _showSource = EditorGUILayout.Foldout(_showSource, "Install controller from package", true);
            if (!_showSource) return;
            EditorGUI.indentLevel++;

            if (_templateNames.Length == 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox("No templates in the package's Templates~ folder.", MessageType.None);
                    if (GUILayout.Button("↻", GUILayout.Width(26), GUILayout.Height(40)))
                        RefreshTemplates();
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    _templateIndex = EditorGUILayout.Popup("Template", _templateIndex, _templateNames);
                    if (EditorGUI.EndChangeCheck())
                        _collisionPath = null;
                    if (GUILayout.Button("↻", GUILayout.Width(26), GUILayout.Height(ToolkitCore.ButtonH)))
                        RefreshTemplates();
                }
                _installDest = EditorGUILayout.TextField("Destination", _installDest);
                if (GUILayout.Button("Install / Update in Project", GUILayout.Height(ToolkitCore.ButtonH)))
                    InstallTemplate();
            }

            EditorGUI.indentLevel--;
        }

        private void RefreshTemplates()
        {
            _templatePaths = Array.Empty<string>();
            _templateNames = Array.Empty<string>();
            string dir = ToolkitCore.GetTemplatesDir();
            if (dir != null && Directory.Exists(dir))
            {
                // Controller templates only — exclude the 2D/3D toggle script (its own window installs it).
                _templatePaths = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
                    .Where(p => Path.GetFileName(p) != ToolkitCore.ToggleScriptName)
                    .Where(p => Path.GetFileName(p) != ToolkitCore.VisualizerScriptName)
                    .ToArray();
                _templateNames = _templatePaths.Select(Path.GetFileName).ToArray();
            }

            // Default to the latest template (highest trailing version, tie-broken by write time).
            _latestTemplateIndex = 0;
            for (int i = 1; i < _templatePaths.Length; i++)
                if (IsNewer(_templatePaths[i], _templatePaths[_latestTemplateIndex]))
                    _latestTemplateIndex = i;
            _templateIndex = _templatePaths.Length == 0 ? 0 : _latestTemplateIndex;
        }

        // "Newer" = higher trailing version number (V4 > V3), tie-broken by last-write time.
        private static bool IsNewer(string a, string b)
        {
            int va = VersionOf(a), vb = VersionOf(b);
            if (va != vb) return va > vb;
            return File.GetLastWriteTimeUtc(a) > File.GetLastWriteTimeUtc(b);
        }

        private static int VersionOf(string path)
        {
            string s = Path.GetFileNameWithoutExtension(path);
            int end = s.Length, start = end;
            while (start > 0 && char.IsDigit(s[start - 1])) start--;
            return start < end ? int.Parse(s.Substring(start, end - start)) : -1;
        }

        private void InstallTemplate(string forceDestRel = null)
        {
            if (_templateIndex < 0 || _templateIndex >= _templatePaths.Length) return;

            // Picking an older-than-latest template needs explicit confirmation.
            if (forceDestRel == null && _templateIndex != _latestTemplateIndex)
            {
                bool ok = EditorUtility.DisplayDialog("Install older template?",
                    $"'{_templateNames[_templateIndex]}' is not the latest " +
                    $"('{_templateNames[_latestTemplateIndex]}'). Install it anyway?",
                    "Install older", "Cancel");
                if (!ok) return;
            }

            string p = Install(_templatePaths[_templateIndex], _installDest, forceDestRel, null);
            if (p != null)
                _message = "Installed " + p + ". Wait for the recompile, then pick it as the New controller below.";
        }

        private void CaptureToPackage()
        {
            if (_captureScript == null) { _message = "Assign a script to capture into the package."; _messageType = MessageType.Warning; return; }
            string dir = ToolkitCore.GetTemplatesDir();
            if (dir == null) { _message = "Could not resolve the package path."; _messageType = MessageType.Error; return; }
            string srcRel = AssetDatabase.GetAssetPath(_captureScript);
            if (string.IsNullOrEmpty(srcRel)) { _message = "The selected script has no asset path."; _messageType = MessageType.Error; return; }

            try
            {
                string srcAbs = Path.Combine(Directory.GetCurrentDirectory(), srcRel);
                string text = File.ReadAllText(srcAbs);
                string origClass = _captureScript.GetClass() != null
                    ? _captureScript.GetClass().Name
                    : ToolkitCore.ExtractClassName(text, Path.GetFileNameWithoutExtension(srcRel));

                string rename = _captureClassName == null ? "" : _captureClassName.Trim();
                string outClass = origClass;
                if (rename.Length > 0 && rename != origClass)
                {
                    if (!ToolkitCore.IsValidIdentifier(rename))
                    { _message = $"'{rename}' is not a valid C# class name."; _messageType = MessageType.Error; return; }
                    text = ToolkitCore.RenameIdentifierInCode(text, origClass, rename);
                    outClass = rename;
                }

                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, outClass + ".cs"), text);
                RefreshTemplates();
                _message = $"Saved {outClass}.cs into the package Templates~" +
                    (outClass != origClass ? $" (renamed from {origClass})" : "") + ". Commit + push to publish it.";
                _messageType = MessageType.Info;
            }
            catch (Exception e)
            {
                _message = "Capture failed — the package may be read-only (git cache). " +
                    "Capture only works while editing the package in its source/embedded location. " + e.Message;
                _messageType = MessageType.Error;
            }
        }

        // ----- Target / type selection -----

        private void RefreshOldCandidates()
        {
            _oldCandidates.Clear();
            _oldIndex = 0;
            if (_target == null) return;
            _oldCandidates = _target.GetComponents<MonoBehaviour>().Cast<Component>().ToList();
        }

        private void RefreshTargets()
        {
            string filter = _newFilter == null ? string.Empty : _newFilter.Trim();
            var list = new List<GameObject>();
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    CollectMatching(root.transform, filter, list);
            }
            if (_target != null && !list.Contains(_target))
                list.Insert(0, _target);

            var withPaths = list.Select(go => (go, path: GetHierarchyPath(go))).OrderBy(p => p.path).ToList();
            _sceneTargets = withPaths.Select(p => p.go).ToList();
            _targetNames = withPaths.Select(p => p.path).ToArray();

            if (_target == null && _sceneTargets.Count > 0)
                _target = _sceneTargets[0];
            _targetIndex = _target != null ? _sceneTargets.IndexOf(_target) : -1;
        }

        private static void CollectMatching(Transform t, string filter, List<GameObject> into)
        {
            if (MatchesFilter(t.gameObject, filter)) into.Add(t.gameObject);
            for (int i = 0; i < t.childCount; i++) CollectMatching(t.GetChild(i), filter, into);
        }

        private static bool MatchesFilter(GameObject go, string filter)
        {
            if (filter.Length == 0) return true;
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                if (c.GetType().Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static string GetHierarchyPath(GameObject go)
        {
            string path = go.name;
            Transform t = go.transform.parent;
            while (t != null) { path = t.name + "/" + path; t = t.parent; }
            return path;
        }

        private void RefreshNewTypes()
        {
            string filter = _newFilter == null ? string.Empty : _newFilter.Trim();
            _newCandidates = ToolkitCore.GetProjectControllerTypes(filter, null);
            _newNames = _newCandidates.Select(ToolkitCore.NiceTypeName).ToArray();
            _newIndex = Mathf.Clamp(_newIndex, 0, Math.Max(0, _newNames.Length - 1));
        }

        // ----- Upgrade -----

        private bool CanUpgrade(out string reason)
        {
            if (_target == null) { reason = "Assign a target GameObject."; return false; }
            if (_oldCandidates.Count == 0) { reason = "No component to replace on the target."; return false; }
            if (_newNames.Length == 0) { reason = "No new controller type available."; return false; }
            Component oldComp = _oldCandidates[_oldIndex];
            if (oldComp == null) { reason = "The selected old component is a missing script."; return false; }
            if (_newCandidates[_newIndex] == oldComp.GetType()) { reason = "Old and new types are the same."; return false; }
            reason = null;
            return true;
        }

        private void Upgrade()
        {
            Component oldComp = _oldCandidates[_oldIndex];
            Type oldType = oldComp.GetType();
            Type newType = _newCandidates[_newIndex];
            GameObject go = _target;

            if (go.GetComponent(newType) != null)
            {
                _report = $"'{go.name}' already has a {newType.Name}. Remove it first, or pick a different new controller.";
                _reportType = MessageType.Warning;
                return;
            }

            Undo.SetCurrentGroupName($"Upgrade {oldType.Name} -> {newType.Name}");
            int group = Undo.GetCurrentGroup();

            Component newComp = Undo.AddComponent(go, newType);
            if (newComp == null)
            {
                _report = $"Failed to add component of type {newType.Name}.";
                _reportType = MessageType.Error;
                return;
            }

            Undo.RegisterCompleteObjectUndo(newComp, "Copy controller values");
            CopySerializedFields(oldComp, newComp, out var copied, out var unmatchedOld, out var defaultedNew);

            Undo.DestroyObjectImmediate(oldComp);
            EditorUtility.SetDirty(go);
            if (go.scene.IsValid()) EditorSceneManager.MarkSceneDirty(go.scene);
            Undo.CollapseUndoOperations(group);

            RefreshOldCandidates();
            BuildReport(oldType, newType, copied, unmatchedOld, defaultedNew);
        }

        private static void CopySerializedFields(
            Component source, Component dest,
            out List<string> copied, out List<string> unmatchedOld, out List<string> defaultedNew)
        {
            copied = new List<string>();
            unmatchedOld = new List<string>();
            defaultedNew = new List<string>();

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            FieldInfo[] sourceFields = source.GetType().GetFields(flags).Where(IsSerializedField).ToArray();
            FieldInfo[] destFields = dest.GetType().GetFields(flags).Where(IsSerializedField).ToArray();

            var sourceByName = new Dictionary<string, FieldInfo>();
            foreach (var f in sourceFields) sourceByName[f.Name] = f;
            var matched = new HashSet<string>();

            foreach (FieldInfo destField in destFields)
            {
                if (sourceByName.TryGetValue(destField.Name, out FieldInfo srcField)
                    && destField.FieldType.IsAssignableFrom(srcField.FieldType))
                {
                    object value = srcField.GetValue(source);
                    destField.SetValue(dest, value);
                    copied.Add($"{destField.Name} = {DescribeValue(value)}");
                    matched.Add(srcField.Name);
                }
                else defaultedNew.Add(destField.Name);
            }
            foreach (FieldInfo srcField in sourceFields)
                if (!matched.Contains(srcField.Name)) unmatchedOld.Add(srcField.Name);
        }

        private static bool IsSerializedField(FieldInfo f)
        {
            if (f.IsStatic || f.IsLiteral || f.IsInitOnly) return false;
            if (f.IsDefined(typeof(NonSerializedAttribute), false)) return false;
            if (f.IsPublic) return true;
            return f.IsDefined(typeof(SerializeField), false);
        }

        private static string DescribeValue(object value)
        {
            if (value == null) return "null";
            if (value is UnityEngine.Object uo) return uo == null ? "None" : uo.name;
            return value.ToString();
        }

        private void BuildReport(Type oldType, Type newType,
            List<string> copied, List<string> unmatchedOld, List<string> defaultedNew)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Upgraded {oldType.Name} → {newType.Name} on \"{_target.name}\".");
            sb.AppendLine();
            sb.AppendLine($"Copied {copied.Count} setting(s):");
            foreach (var c in copied) sb.AppendLine($"  • {c}");
            if (unmatchedOld.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"{unmatchedOld.Count} old setting(s) had no equivalent on the new controller:");
                sb.AppendLine($"  {string.Join(", ", unmatchedOld)}");
            }
            if (defaultedNew.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"{defaultedNew.Count} new setting(s) kept their defaults (no matching old field):");
                sb.AppendLine($"  {string.Join(", ", defaultedNew)}");
            }
            sb.AppendLine();
            sb.AppendLine("Press Play to verify, then delete the old script. Ctrl+Z undoes the upgrade.");
            _report = sb.ToString();
            _reportType = unmatchedOld.Count > 0 ? MessageType.Warning : MessageType.Info;
        }
    }
}
