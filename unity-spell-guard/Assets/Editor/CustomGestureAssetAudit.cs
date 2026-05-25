#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpellGuard.InputSystem;
using UnityEditor;
using UnityEngine;

namespace SpellGuard.EditorTools
{
    public sealed class CustomGestureAssetAuditReport
    {
        public string ActiveLibraryPath { get; set; }
        public string ReferenceVideoPath { get; set; }
        public List<string> ActiveTemplateIds { get; } = new List<string>();
        public List<string> ReferenceClipIds { get; } = new List<string>();
        public List<string> ReferenceOnlyClipIds { get; } = new List<string>();
        public List<string> ArchivedTemplateIds { get; } = new List<string>();
        public List<string> MatchedReferenceIds { get; } = new List<string>();
        public List<string> TemplatesMissingReferenceClips { get; } = new List<string>();
        public List<string> ReferenceClipsMissingTemplates { get; } = new List<string>();
        public List<string> UndeclaredReferenceClipsMissingTemplates { get; } = new List<string>();
        public List<string> TemplatesOnlyInArchive { get; } = new List<string>();
        public List<string> InvalidActiveTemplateFiles { get; } = new List<string>();
        public List<string> InvalidArchivedTemplateFiles { get; } = new List<string>();
        public List<string> EmptyReferenceClipFolders { get; } = new List<string>();

        public bool HasBlockingIssues => TemplatesMissingReferenceClips.Count > 0 || UndeclaredReferenceClipsMissingTemplates.Count > 0 || InvalidActiveTemplateFiles.Count > 0 || EmptyReferenceClipFolders.Count > 0;
    }

    public static class CustomGestureAssetAudit
    {
        private const string MenuPath = "Spell Guard/Custom Gestures/Audit Asset Boundaries";
        private static readonly HashSet<string> DeclaredReferenceOnlyClipIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ext_any_motion_easy",
            "ext_finger_snap_video_template"
        };

        [MenuItem(MenuPath)]
        public static void AuditFromMenu()
        {
            var report = AuditProject();
            LogReport(report);
        }

        public static CustomGestureAssetAuditReport AuditProject()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var activeLibraryPath = Path.Combine(Application.dataPath, "ProjectGestureLibrary", "CustomGestures");
            var projectGestureLibraryPath = Path.Combine(Application.dataPath, "ProjectGestureLibrary");
            var referenceVideoPath = Path.Combine(Application.streamingAssetsPath, "CustomGestureReferenceVideos");

            var report = new CustomGestureAssetAuditReport
            {
                ActiveLibraryPath = NormalizePath(activeLibraryPath),
                ReferenceVideoPath = NormalizePath(referenceVideoPath)
            };

            var activeIds = LoadTemplateIds(activeLibraryPath, report.InvalidActiveTemplateFiles);
            report.ActiveTemplateIds.AddRange(activeIds);

            foreach (var archiveFolder in EnumerateArchiveFolders(projectGestureLibraryPath))
            {
                report.ArchivedTemplateIds.AddRange(LoadTemplateIds(archiveFolder, report.InvalidArchivedTemplateFiles));
            }

            report.ReferenceClipIds.AddRange(EnumerateReferenceClipIds(referenceVideoPath, report.EmptyReferenceClipFolders));

            var activeSet = new HashSet<string>(report.ActiveTemplateIds, StringComparer.OrdinalIgnoreCase);
            var referenceSet = new HashSet<string>(report.ReferenceClipIds, StringComparer.OrdinalIgnoreCase);
            var archivedSet = new HashSet<string>(report.ArchivedTemplateIds, StringComparer.OrdinalIgnoreCase);

            report.MatchedReferenceIds.AddRange(activeSet.Where(referenceSet.Contains).OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
            report.TemplatesMissingReferenceClips.AddRange(activeSet.Where(id => !referenceSet.Contains(id)).OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
            report.ReferenceClipsMissingTemplates.AddRange(referenceSet.Where(id => !activeSet.Contains(id)).OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
            report.ReferenceOnlyClipIds.AddRange(report.ReferenceClipsMissingTemplates.Where(id => DeclaredReferenceOnlyClipIds.Contains(id)).OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
            report.UndeclaredReferenceClipsMissingTemplates.AddRange(report.ReferenceClipsMissingTemplates.Where(id => !DeclaredReferenceOnlyClipIds.Contains(id)).OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
            report.TemplatesOnlyInArchive.AddRange(archivedSet.Where(id => !activeSet.Contains(id) && referenceSet.Contains(id)).OrderBy(id => id, StringComparer.OrdinalIgnoreCase));

            Debug.Log($"[CustomGestureAssetAudit] project={NormalizePath(projectRoot)} activeTemplates={report.ActiveTemplateIds.Count} referenceClips={report.ReferenceClipIds.Count} matched={report.MatchedReferenceIds.Count}");
            return report;
        }

        private static IEnumerable<string> EnumerateArchiveFolders(string projectGestureLibraryPath)
        {
            if (!Directory.Exists(projectGestureLibraryPath))
            {
                return Array.Empty<string>();
            }

            return Directory.GetDirectories(projectGestureLibraryPath, "ArchivedCustomGestures_*", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        }

        private static List<string> LoadTemplateIds(string folderPath, List<string> invalidTemplateFiles)
        {
            var ids = new List<string>();
            if (!Directory.Exists(folderPath))
            {
                return ids;
            }

            var library = new CustomGestureLibrary(folderPath);
            library.LoadAll();
            ids.AddRange(library.Templates.Select(template => template.GestureId));

            var loadedSet = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(folderPath, "*.json", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var fallbackId = Path.GetFileNameWithoutExtension(file);
                if (!loadedSet.Contains(fallbackId))
                {
                    invalidTemplateFiles.Add(NormalizePath(file));
                }
            }

            return ids.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static List<string> EnumerateReferenceClipIds(string referenceVideoPath, List<string> emptyReferenceClipFolders)
        {
            var ids = new List<string>();
            if (!Directory.Exists(referenceVideoPath))
            {
                return ids;
            }

            foreach (var directory in Directory.GetDirectories(referenceVideoPath, "*", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var frameCount = Directory.GetFiles(directory, "*.jpg", SearchOption.TopDirectoryOnly).Length
                                 + Directory.GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly).Length;
                if (frameCount <= 0)
                {
                    emptyReferenceClipFolders.Add(NormalizePath(directory));
                    continue;
                }

                ids.Add(Path.GetFileName(directory));
            }

            return ids;
        }

        private static void LogReport(CustomGestureAssetAuditReport report)
        {
            var status = report.HasBlockingIssues ? "WARN" : "PASS";
            Debug.Log($"[CustomGestureAssetAudit][{status}] active={report.ActiveTemplateIds.Count}, reference={report.ReferenceClipIds.Count}, matched={report.MatchedReferenceIds.Count}, archivedReferenceMatches={report.TemplatesOnlyInArchive.Count}");
            LogList("Matched active template/reference ids", report.MatchedReferenceIds);
            LogList("Active templates missing reference clips", report.TemplatesMissingReferenceClips, true);
            LogList("Declared reference-only clips", report.ReferenceOnlyClipIds);
            LogList("Undeclared reference clips missing active templates", report.UndeclaredReferenceClipsMissingTemplates, true);
            LogList("Reference clips backed only by archive templates", report.TemplatesOnlyInArchive);
            LogList("Invalid or inactive active template files", report.InvalidActiveTemplateFiles, true);
            LogList("Invalid or inactive archived template files", report.InvalidArchivedTemplateFiles);
            LogList("Empty reference clip folders", report.EmptyReferenceClipFolders, true);
        }

        private static void LogList(string title, IReadOnlyList<string> values, bool warning = false)
        {
            if (values == null || values.Count == 0)
            {
                Debug.Log($"[CustomGestureAssetAudit] {title}: none");
                return;
            }

            var message = $"[CustomGestureAssetAudit] {title} ({values.Count}):\n- {string.Join("\n- ", values)}";
            if (warning)
            {
                Debug.LogWarning(message);
            }
            else
            {
                Debug.Log(message);
            }
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/');
        }
    }
}
#endif
