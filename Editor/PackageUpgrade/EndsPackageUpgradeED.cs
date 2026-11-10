using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

[assembly: InternalsVisibleTo("ALTUSHKA.EasyNodeDialogSystem.PackageUpgrade.Tests")]

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    [InitializeOnLoad]
    internal sealed class EndsPackageUpgradeED : AssetPostprocessor
    {
        private const string ROOT_GUID = "ca13dbb45c22a5e43b94ee23d128d4b0";
        private const string MANIFEST_GUID = "d5cbfb60ca830df4abac45bda7204d92";
        private const string MANIFEST_PATH = "/Editor/PackageUpgrade/EndsPackageLayout.txt";

        private static bool _queued;
        private static bool _running;

        #region INITIALIZATION

        static EndsPackageUpgradeED()
        {
            QueueUpgrade();
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (_running) return;

            string root = AssetDatabase.GUIDToAssetPath(ROOT_GUID);

            if (string.IsNullOrEmpty(root)) return;

            foreach (string path in importedAssets)
            {
                if (!path.StartsWith(root + "/", StringComparison.Ordinal)) continue;
                if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".asmref", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(MANIFEST_PATH, StringComparison.Ordinal)) continue;

                QueueUpgrade();
                return;
            }
        }

        private static void QueueUpgrade()
        {
            if (_queued || _running) return;

            _queued = true;
            EditorApplication.update += UpgradeWhenReady;
        }

        private static void UpgradeWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            EditorApplication.update -= UpgradeWhenReady;
            _queued = false;

            string root = AssetDatabase.GUIDToAssetPath(ROOT_GUID);

            if (!root.StartsWith("Assets/", StringComparison.Ordinal) || !AssetDatabase.IsValidFolder(root)) return;

            string manifestPath = AssetDatabase.GUIDToAssetPath(MANIFEST_GUID);

            if (!manifestPath.StartsWith(root + "/", StringComparison.Ordinal)) return;

            TextAsset manifest = AssetDatabase.LoadAssetAtPath<TextAsset>(manifestPath);

            if (manifest == null) return;

            try
            {
                List<Move> moves = BuildPlan(root, manifest.text);

                if (moves.Count == 0) return;

                ApplyPlan(root, moves);
                Debug.Log("ENDS: updated the package folder layout, moved " + moves.Count + " files. Database content was not changed.");
            }
            catch (Exception exception)
            {
                Debug.LogError("ENDS: package folder upgrade stopped to preserve existing files. " + exception.Message);
            }
        }

        #endregion INITIALIZATION

        #region LAYOUT VALIDATION

        /// <summary> ENDS. Validates every package destination before returning the file moves. </summary>
        internal static List<Move> BuildPlan(string root, string manifest)
        {
            var moves = new List<Move>();
            var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string line in manifest.Split('\n'))
            {
                string trimmed = line.Trim();

                if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal)) continue;

                string[] fields = trimmed.Split('\t');

                if (fields.Length != 2 || !Guid.TryParseExact(fields[0], "N", out _)) throw new InvalidDataException("Invalid package layout entry.");

                string relative = fields[1];

                if (relative.StartsWith("/", StringComparison.Ordinal) || relative.Contains("\\") || relative.Contains(":") || relative.Contains("..")) throw new InvalidDataException("Package layout paths must stay inside the package.");

                string destination = root + "/" + relative;

                if (!destinations.Add(destination) || !identities.Add(fields[0])) throw new InvalidDataException("Duplicate package layout identity or destination.");

                string source = AssetDatabase.GUIDToAssetPath(fields[0]);

                if (string.IsNullOrEmpty(source) || !source.StartsWith(root + "/", StringComparison.Ordinal)) continue;
                if (string.Equals(source, destination, StringComparison.Ordinal)) continue;
                if (AssetDatabase.IsValidFolder(source) || source.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Database assets and folders cannot be moved by the package upgrade.");
                if (!string.Equals(Path.GetExtension(source), Path.GetExtension(destination), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Package file extensions must not change during upgrade.");

                string occupant = AssetDatabase.AssetPathToGUID(destination);

                if (!string.IsNullOrEmpty(occupant) || File.Exists(destination) || Directory.Exists(destination)) throw new IOException("Target path is already occupied: " + destination);

                moves.Add(new Move(source, destination));
            }

            moves.Sort(CompareMoves);
            return moves;
        }

        private static int CompareMoves(Move first, Move second)
        {
            bool firstDefinition = first.Source.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase);
            bool secondDefinition = second.Source.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase);
            int definitionOrder = firstDefinition.CompareTo(secondDefinition);

            if (definitionOrder != 0) return definitionOrder;

            return string.Compare(first.Source, second.Source, StringComparison.Ordinal);
        }

        #endregion LAYOUT VALIDATION

        #region FILE MOVES

        /// <summary> ENDS. Moves package files with their GUIDs and restores earlier moves if an operation fails. </summary>
        internal static void ApplyPlan(string root, List<Move> moves)
        {
            foreach (Move move in moves)
            {
                EnsureFolder(root, Path.GetDirectoryName(move.Destination).Replace('\\', '/'));
            }

            int completed = 0;
            _running = true;
            EditorApplication.LockReloadAssemblies();

            try
            {
                AssetDatabase.StartAssetEditing();

                try
                {
                    foreach (Move move in moves)
                    {
                        string error = AssetDatabase.MoveAsset(move.Source, move.Destination);

                        if (!string.IsNullOrEmpty(error)) throw new IOException(error);

                        completed++;
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
            }
            catch
            {
                for (int index = completed - 1; index >= 0; index--)
                {
                    Move move = moves[index];
                    string error = AssetDatabase.MoveAsset(move.Destination, move.Source);

                    if (!string.IsNullOrEmpty(error)) Debug.LogError("ENDS: could not restore the original package path: " + error);
                }

                throw;
            }
            finally
            {
                EditorApplication.UnlockReloadAssemblies();
                _running = false;
            }
        }

        private static void EnsureFolder(string root, string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            if (!path.StartsWith(root + "/", StringComparison.Ordinal)) throw new IOException("Folder creation outside the package was rejected.");

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(root, parent);
            string guid = AssetDatabase.CreateFolder(parent, Path.GetFileName(path));

            if (string.IsNullOrEmpty(guid)) throw new IOException("Could not create the package folder: " + path);
        }

        #endregion FILE MOVES

        internal readonly struct Move
        {
            internal readonly string Source;
            internal readonly string Destination;

            internal Move(string source, string destination)
            {
                Source = source;
                Destination = destination;
            }
        }
    }
}