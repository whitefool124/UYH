using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public sealed class CustomGestureLibrary
    {
        private const string FolderName = "CustomGestures";
        private const string ProjectLibraryFolder = "ProjectGestureLibrary";
        private readonly List<CustomGestureTemplate> templates = new List<CustomGestureTemplate>();
        private readonly string folderPath;

        [Serializable]
        private sealed class TemplateFile
        {
            public CustomGestureTemplate Template;
        }

        public CustomGestureLibrary() : this(GetDefaultProjectLibraryPath())
        {
        }

        public CustomGestureLibrary(string folderPath)
        {
            this.folderPath = folderPath;
        }

        public IReadOnlyList<CustomGestureTemplate> Templates => templates;
        public string FolderPath => folderPath;

        public void LoadAll()
        {
            templates.Clear();
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return;
            }

            var files = Directory.GetFiles(folderPath, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < files.Length; index++)
            {
                if (TryLoad(files[index], out var template))
                {
                    templates.Add(template);
                }
            }
        }

        public bool Save(CustomGestureTemplate template)
        {
            if (!TrySanitizeTemplate(template, out var sanitized))
            {
                return false;
            }

            Directory.CreateDirectory(folderPath);
            var path = GetTemplatePath(sanitized.GestureId);
            var wrapper = new TemplateFile { Template = sanitized };
            File.WriteAllText(path, JsonUtility.ToJson(wrapper, true));
            ReplaceInMemory(sanitized);
            return true;
        }

        public bool Delete(string gestureId)
        {
            var sanitizedId = SanitizeId(gestureId);
            if (string.IsNullOrWhiteSpace(sanitizedId))
            {
                return false;
            }

            templates.RemoveAll(template => string.Equals(template.GestureId, sanitizedId, StringComparison.OrdinalIgnoreCase));
            var path = GetTemplatePath(sanitizedId);
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }

        public static bool IsAllowedTargetIntent(GestureIntent intent)
        {
            return intent == GestureIntent.CustomGesture || intent == GestureIntent.CastFire || intent == GestureIntent.CastIce || intent == GestureIntent.CastShield;
        }

        public string GetTemplatePath(string gestureId)
        {
            return Path.Combine(folderPath, SanitizeId(gestureId) + ".json");
        }

        private bool TryLoad(string path, out CustomGestureTemplate template)
        {
            template = null;
            try
            {
                var json = File.ReadAllText(path);
                var wrapper = JsonUtility.FromJson<TemplateFile>(json);
                if (wrapper == null || !TrySanitizeTemplate(wrapper.Template, out template))
                {
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[CustomGesture] Failed to load template '{path}': {exception.Message}");
                return false;
            }
        }

        private static bool TrySanitizeTemplate(CustomGestureTemplate template, out CustomGestureTemplate sanitized)
        {
            sanitized = null;
            if (template == null)
            {
                return false;
            }

            var gestureId = SanitizeId(template.GestureId);
            if (string.IsNullOrWhiteSpace(gestureId))
            {
                return false;
            }

            template.GestureId = gestureId;
            if (string.IsNullOrWhiteSpace(template.DisplayName))
            {
                template.DisplayName = gestureId;
            }

            template.Kind = CustomGestureKind.DynamicMotion;
            template.TargetIntent = GestureIntent.CustomGesture;
            template.MatchThreshold = Mathf.Clamp(template.MatchThreshold <= 0f ? CustomGestureRecognizer.DefaultDynamicThreshold : template.MatchThreshold, 0.01f, 2f);
            template.Samples ??= new List<CustomGestureSample>();
            template.RequiredHandedness = ResolveTemplateHandedness(template);
            sanitized = template;
            return true;
        }

        private static string GetDefaultProjectLibraryPath()
        {
            return Path.Combine(Application.dataPath, ProjectLibraryFolder, FolderName);
        }

        private static GestureHandedness ResolveTemplateHandedness(CustomGestureTemplate template)
        {
            if (template.RequiredHandedness != GestureHandedness.Unknown)
            {
                return template.RequiredHandedness;
            }

            if (template.Samples == null)
            {
                return GestureHandedness.Unknown;
            }

            for (var index = 0; index < template.Samples.Count; index++)
            {
                var sample = template.Samples[index];
                if (sample != null && sample.Handedness != GestureHandedness.Unknown)
                {
                    return sample.Handedness;
                }
            }

            return GestureHandedness.Unknown;
        }

        private void ReplaceInMemory(CustomGestureTemplate template)
        {
            for (var index = 0; index < templates.Count; index++)
            {
                if (string.Equals(templates[index].GestureId, template.GestureId, StringComparison.OrdinalIgnoreCase))
                {
                    templates[index] = template;
                    return;
                }
            }

            templates.Add(template);
        }

        private static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.Trim().ToCharArray();
            for (var index = 0; index < chars.Length; index++)
            {
                if (Array.IndexOf(invalid, chars[index]) >= 0 || char.IsWhiteSpace(chars[index]))
                {
                    chars[index] = '_';
                }
            }

            return new string(chars);
        }
    }
}
