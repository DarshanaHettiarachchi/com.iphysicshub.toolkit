using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace IPhysicsHub.Toolkit.Editor
{
    /// <summary>Result of an install attempt — success path, status message, and any collision.</summary>
    internal struct InstallResult
    {
        public string InstalledPath;   // project-relative path on success, else null
        public string Message;
        public MessageType Type;
        public string CollisionPath;   // existing Assets path when blocked by the duplicate guard
        public string CollisionText;   // prepared source text to write when resolving the collision in place

        public bool Ok => InstalledPath != null;

        public static InstallResult Error(string m) =>
            new InstallResult { Message = m, Type = MessageType.Error };
        public static InstallResult Warn(string m, string col, string text) =>
            new InstallResult { Message = m, Type = MessageType.Warning, CollisionPath = col, CollisionText = text };
        public static InstallResult Done(string path) =>
            new InstallResult { InstalledPath = path };
    }

    /// <summary>Shared logic used by the toolkit windows (template store, install, helpers, sizing).</summary>
    internal static class ToolkitCore
    {
        // Slightly larger, consistent UI metrics.
        public const float ButtonH = 26f;
        public const float LabelW = 170f;

        // Toggle payload identifiers (shared so the controller list can exclude it).
        public const string ToggleScriptName = "Camera2DToggleUI.cs";
        public const string ToggleClassName = "Camera2DToggleUI";
        public const string VisualizerScriptName = "UIHitAreaVisualizer.cs";
        public const string VisualizerClassName = "UIHitAreaVisualizer";

        // Absolute path to the package's hidden Templates~ folder (resolved via Package Manager).
        public static string GetTemplatesDir()
        {
            var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ToolkitCore).Assembly);
            return pkg == null ? null : Path.Combine(pkg.resolvedPath, "Templates~");
        }

        // Reads a .cs file and installs its text (see InstallScriptText). forceDestRel overwrites
        // that exact path; otherwise installs into destFolder.
        public static InstallResult InstallScriptFile(string srcAbsPath, string destFolder, string forceDestRel)
        {
            string srcText;
            try { srcText = File.ReadAllText(srcAbsPath); }
            catch (Exception e) { return InstallResult.Error("Could not read source: " + e.Message); }
            return InstallScriptText(srcText, destFolder, forceDestRel, Path.GetFileNameWithoutExtension(srcAbsPath));
        }

        // Writes C# source into the project, named after its class (a MonoBehaviour file must match
        // its type). Honors the Assets-scope + duplicate-class guards; ImportAsset only (caller
        // refreshes). forceDestRel overwrites that exact path; otherwise installs into destFolder.
        // Works on text so callers can rewrite the source (e.g. retarget a type) before installing.
        public static InstallResult InstallScriptText(string srcText, string destFolder, string forceDestRel, string classNameFallback)
        {
            string className = ExtractClassName(srcText, classNameFallback);

            string destRel;
            if (forceDestRel != null)
            {
                destRel = forceDestRel.Replace('\\', '/');
                if (destRel != "Assets" && !destRel.StartsWith("Assets/"))
                    return InstallResult.Error("Can only overwrite scripts inside the project's Assets/ folder.");
            }
            else
            {
                string dest = string.IsNullOrWhiteSpace(destFolder)
                    ? "Assets/Scripts"
                    : destFolder.Trim().Replace('\\', '/').TrimEnd('/');
                if (dest != "Assets" && !dest.StartsWith("Assets/"))
                    return InstallResult.Error("Destination must be inside the project's Assets/ folder.");

                destRel = dest + "/" + className + ".cs";

                string existing = FindExistingScriptPath(className);
                if (existing != null && !PathsEqual(existing, destRel))
                {
                    bool inAssets = existing.Replace('\\', '/').StartsWith("Assets/");
                    string msg = inAssets
                        ? $"Class '{className}' already exists at {existing}. Installing to {destRel} would " +
                          "cause a duplicate-class error. Use 'Update existing in place', or capture it under " +
                          "a different class name to let copies coexist."
                        : $"Class '{className}' is already provided by a package at {existing}. Installing would " +
                          "cause a duplicate-class error. Capture it under a different class name to coexist.";
                    return InstallResult.Warn(msg, inAssets ? existing : null, inAssets ? srcText : null);
                }
            }

            try
            {
                string destAbs = Path.Combine(Directory.GetCurrentDirectory(), destRel);
                Directory.CreateDirectory(Path.GetDirectoryName(destAbs));
                File.WriteAllText(destAbs, srcText);
                AssetDatabase.ImportAsset(destRel, ImportAssetOptions.ForceUpdate);
                return InstallResult.Done(destRel);
            }
            catch (Exception e) { return InstallResult.Error("Install failed: " + e.Message); }
        }

        // First class name declared in the source, or the fallback if none is found.
        public static string ExtractClassName(string sourceText, string fallback)
        {
            Match m = Regex.Match(sourceText, @"\bclass\s+(\w+)");
            return m.Success ? m.Groups[1].Value : fallback;
        }

        // Asset path of an existing project script that declares the given class, or null.
        public static string FindExistingScriptPath(string className)
        {
            foreach (string guid in AssetDatabase.FindAssets(className + " t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (ms != null && ms.GetClass() != null && ms.GetClass().Name == className)
                    return path;
            }
            return null;
        }

        public static bool PathsEqual(string a, string b) =>
            string.Equals(a.Replace('\\', '/'), b.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);

        public static bool IsValidIdentifier(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (!(char.IsLetter(s[0]) || s[0] == '_')) return false;
            return s.All(c => char.IsLetterOrDigit(c) || c == '_');
        }

        public static Type FindTypeByName(string name) =>
            TypeCache.GetTypesDerivedFrom<MonoBehaviour>().FirstOrDefault(t => t.Name == name);

        // Replace whole-word occurrences of an identifier in C# source, skipping comments and
        // string/char literals so only real code tokens are renamed.
        public static string RenameIdentifierInCode(string text, string oldName, string newName)
        {
            var sb = new StringBuilder(text.Length);
            int i = 0, n = text.Length;
            while (i < n)
            {
                char c = text[i];

                if (c == '/' && i + 1 < n && text[i + 1] == '/') // line comment
                {
                    int end = text.IndexOf('\n', i);
                    if (end < 0) end = n;
                    sb.Append(text, i, end - i); i = end; continue;
                }
                if (c == '/' && i + 1 < n && text[i + 1] == '*') // block comment
                {
                    int end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    end = end < 0 ? n : end + 2;
                    sb.Append(text, i, end - i); i = end; continue;
                }
                if (c == '@' && i + 1 < n && text[i + 1] == '"') // verbatim string
                {
                    int j = i + 2;
                    while (j < n)
                    {
                        if (text[j] == '"')
                        {
                            if (j + 1 < n && text[j + 1] == '"') { j += 2; continue; }
                            j++; break;
                        }
                        j++;
                    }
                    sb.Append(text, i, j - i); i = j; continue;
                }
                if (c == '"') // regular string
                {
                    int j = i + 1;
                    while (j < n)
                    {
                        if (text[j] == '\\') { j += 2; continue; }
                        if (text[j] == '"') { j++; break; }
                        j++;
                    }
                    sb.Append(text, i, j - i); i = j; continue;
                }
                if (c == '\'') // char literal
                {
                    int j = i + 1;
                    while (j < n)
                    {
                        if (text[j] == '\\') { j += 2; continue; }
                        if (text[j] == '\'') { j++; break; }
                        j++;
                    }
                    sb.Append(text, i, j - i); i = j; continue;
                }
                if (c == '_' || char.IsLetter(c)) // identifier
                {
                    int j = i + 1;
                    while (j < n && (text[j] == '_' || char.IsLetterOrDigit(text[j]))) j++;
                    string word = text.Substring(i, j - i);
                    sb.Append(word == oldName ? newName : word);
                    i = j; continue;
                }

                sb.Append(c); i++;
            }
            return sb.ToString();
        }

        // ----- Project type discovery (shared by the windows) -----

        // True for a type defined in the game's own assemblies (excludes Unity/System runtimes).
        public static bool IsProjectType(Type t)
        {
            string asm = t.Assembly.GetName().Name ?? string.Empty;
            if (asm.StartsWith("Unity", StringComparison.Ordinal)) return false;
            if (asm.StartsWith("System", StringComparison.Ordinal)) return false;
            if (asm.StartsWith("mscorlib", StringComparison.Ordinal)) return false;
            if (asm.StartsWith("netstandard", StringComparison.Ordinal)) return false;
            return true;
        }

        public static string NiceTypeName(Type t) =>
            string.IsNullOrEmpty(t.Namespace) ? t.Name : $"{t.Name}  ({t.Namespace})";

        // Namespace-qualified type name (e.g. MyGame.Cameras.CameraControllerV4), so it resolves
        // in generated source without needing a matching `using`. Nested types use '.'.
        public static string FullTypeName(Type t) =>
            (string.IsNullOrEmpty(t.Namespace) ? t.Name : t.Namespace + "." + t.Name).Replace('+', '.');

        // Concrete project MonoBehaviours whose name contains the filter, excluding one name
        // (e.g. the toggle itself). Ordered by name; case-insensitive filter; empty filter = all.
        public static List<Type> GetProjectControllerTypes(string filter, string excludeName)
        {
            string f = filter == null ? string.Empty : filter.Trim();
            return TypeCache.GetTypesDerivedFrom<MonoBehaviour>()
                .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition)
                .Where(IsProjectType)
                .Where(t => t.Name != excludeName)
                .Where(t => f.Length == 0 || t.Name.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(t => t.Name)
                .ToList();
        }

        // Trailing-digit "version" of a bare name (e.g. CameraControllerV4 -> 4), or -1 if none.
        // Uses long + TryParse so a long digit run (generated/timestamped names) can't overflow/throw.
        public static long VersionOfName(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            int end = name.Length, start = end;
            while (start > 0 && char.IsDigit(name[start - 1])) start--;
            if (start == end) return -1;
            return long.TryParse(name.Substring(start, end - start), out long v) ? v : long.MaxValue;
        }

        // Index of the highest-versioned type by trailing-digit name (0 when none/empty).
        public static int LatestIndexByName(IList<Type> types)
        {
            int best = 0;
            for (int i = 1; i < (types?.Count ?? 0); i++)
                if (VersionOfName(types[i].Name) > VersionOfName(types[best].Name)) best = i;
            return best;
        }

        // Declared type of a `public <Type> <fieldName>;` field in C# source, or null.
        public static string GetFieldType(string srcText, string fieldName)
        {
            Match m = Regex.Match(srcText, @"public\s+(\w+)\s+" + Regex.Escape(fieldName) + @"\b");
            return m.Success ? m.Groups[1].Value : null;
        }
    }

    /// <summary>Base window: shared status line + duplicate-class "update in place" resolution.</summary>
    internal abstract class ToolkitWindowBase : EditorWindow
    {
        protected string _message;
        protected MessageType _messageType = MessageType.Info;
        protected string _collisionPath;
        protected string _collisionText;

        protected void ApplyResult(InstallResult r)
        {
            _message = r.Message;
            _messageType = r.Type;
            _collisionPath = r.CollisionPath;
            _collisionText = r.CollisionText;
        }

        // Installs a source file and, on success, refreshes + sets a success message. Returns the path.
        protected string Install(string srcAbs, string destFolder, string forceDestRel, string successMsg) =>
            AfterInstall(ToolkitCore.InstallScriptFile(srcAbs, destFolder, forceDestRel), successMsg);

        // Installs prepared source text (already rewritten by the caller). Returns the path.
        protected string InstallText(string srcText, string destFolder, string forceDestRel, string classNameFallback, string successMsg) =>
            AfterInstall(ToolkitCore.InstallScriptText(srcText, destFolder, forceDestRel, classNameFallback), successMsg);

        private string AfterInstall(InstallResult r, string successMsg)
        {
            ApplyResult(r);
            if (r.Ok)
            {
                AssetDatabase.Refresh();
                _message = successMsg ?? ("Installed " + r.InstalledPath + ".");
                _messageType = MessageType.Info;
            }
            return r.InstalledPath;
        }

        protected void DrawStatus()
        {
            if (_collisionPath != null && _collisionText != null &&
                GUILayout.Button("Update existing in place: " + _collisionPath, GUILayout.Height(ToolkitCore.ButtonH)))
            {
                InstallText(_collisionText, null, _collisionPath, null, "Updated " + _collisionPath + ". Wait for the recompile.");
            }
            if (!string.IsNullOrEmpty(_message))
                EditorGUILayout.HelpBox(_message, _messageType);
        }
    }
}
