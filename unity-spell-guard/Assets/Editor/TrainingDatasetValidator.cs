#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SpellGuard.EditorTools
{
    public sealed class TrainingDatasetPackageReport
    {
        public string PackagePath { get; set; }
        public List<string> ObservedEntries { get; } = new List<string>();
        public List<string> MissingEntries { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();
        public int TgzCount { get; set; }
        public int AviCount { get; set; }

        public bool IsValid => MissingEntries.Count == 0 && Errors.Count == 0;
    }

    public sealed class TrainingDatasetValidationReport
    {
        public string DatasetRoot { get; set; }
        public TrainingDatasetPackageReport Annotations { get; set; } = new TrainingDatasetPackageReport();
        public List<TrainingDatasetPackageReport> Videos { get; } = new List<TrainingDatasetPackageReport>();
        public List<string> Errors { get; } = new List<string>();

        public bool IsValid => Errors.Count == 0 && Annotations.IsValid && Videos.All(video => video.IsValid);
    }

    public static class TrainingDatasetValidator
    {
        private static readonly string[] RequiredAnnotationEntries =
        {
            "metadata.csv",
            "classIdx.txt",
            "Annot_TrainList.txt",
            "Annot_TestList.txt",
            "Video_TrainList.txt",
            "Video_TestList.txt"
        };

        public static string ResolveDefaultDatasetRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "训练集"));
        }

        [MenuItem("Spell Guard/Validate Training Dataset")]
        public static void ValidateFromMenu()
        {
            var report = ValidateDatasetRoot(ResolveDefaultDatasetRoot());
            var status = report.IsValid ? "PASS" : "FAIL";
            Debug.Log($"[TrainingDataset][{status}] root={report.DatasetRoot} annotations={report.Annotations.IsValid} videos={report.Videos.Count}");

            foreach (var error in report.Errors)
            {
                Debug.LogError($"[TrainingDataset] {error}");
            }

            if (!report.Annotations.IsValid)
            {
                foreach (var error in report.Annotations.Errors)
                {
                    Debug.LogError($"[TrainingDataset][Annotations] {error}");
                }

                foreach (var missing in report.Annotations.MissingEntries)
                {
                    Debug.LogError($"[TrainingDataset][Annotations] Missing: {missing}");
                }
            }

            foreach (var video in report.Videos)
            {
                if (video.IsValid)
                {
                    continue;
                }

                foreach (var error in video.Errors)
                {
                    Debug.LogError($"[TrainingDataset][Video] {Path.GetFileName(video.PackagePath)}: {error}");
                }
            }

            if (!report.IsValid)
            {
                EditorUtility.DisplayDialog("Training Dataset Validation", "Validation failed. Check the Console for details.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Training Dataset Validation", $"Validation passed. Videos: {report.Videos.Count}, AVI clips: {report.Videos.Sum(video => video.AviCount)}", "OK");
            }
        }

        public static TrainingDatasetValidationReport ValidateDatasetRoot(string datasetRoot)
        {
            var report = new TrainingDatasetValidationReport
            {
                DatasetRoot = datasetRoot
            };

            if (string.IsNullOrWhiteSpace(datasetRoot) || !Directory.Exists(datasetRoot))
            {
                report.Errors.Add($"Dataset root not found: {datasetRoot}");
                return report;
            }

            var annotationZips = Directory.GetFiles(datasetRoot, "annotations*.zip", SearchOption.TopDirectoryOnly);
            if (annotationZips.Length == 0)
            {
                report.Errors.Add("No annotations*.zip archive found in dataset root.");
            }
            else
            {
                if (annotationZips.Length > 1)
                {
                    report.Errors.Add($"Expected one annotations archive but found {annotationZips.Length}.");
                }

                report.Annotations = ValidateAnnotationsZip(annotationZips[0]);
            }

            var videoZips = Directory.GetFiles(datasetRoot, "videos*.zip", SearchOption.TopDirectoryOnly);
            if (videoZips.Length == 0)
            {
                report.Errors.Add("No videos*.zip archive found in dataset root.");
            }

            foreach (var videoZip in videoZips)
            {
                report.Videos.Add(ValidateVideosZip(videoZip));
            }

            return report;
        }

        public static TrainingDatasetPackageReport ValidateAnnotationsZip(string zipPath)
        {
            var report = new TrainingDatasetPackageReport { PackagePath = zipPath };

            if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
            {
                report.Errors.Add($"Annotations archive not found: {zipPath}");
                return report;
            }

            try
            {
                using var stream = File.OpenRead(zipPath);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
                var names = archive.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToList();
                report.ObservedEntries.AddRange(names);

                foreach (var requiredEntry in RequiredAnnotationEntries)
                {
                    if (!names.Any(name => string.Equals(Path.GetFileName(name), requiredEntry, StringComparison.OrdinalIgnoreCase)))
                    {
                        report.MissingEntries.Add(requiredEntry);
                    }
                }
            }
            catch (Exception exception)
            {
                report.Errors.Add($"Failed to read annotations archive: {exception.Message}");
            }

            return report;
        }

        public static TrainingDatasetPackageReport ValidateVideosZip(string zipPath)
        {
            var report = new TrainingDatasetPackageReport { PackagePath = zipPath };

            if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
            {
                report.Errors.Add($"Video archive not found: {zipPath}");
                return report;
            }

            try
            {
                using var stream = File.OpenRead(zipPath);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
                var tgzEntries = archive.Entries.Where(entry => entry.FullName.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase)).ToList();
                report.TgzCount = tgzEntries.Count;

                if (tgzEntries.Count == 0)
                {
                    report.Errors.Add("No .tgz archives found inside video zip.");
                    return report;
                }

                foreach (var entry in tgzEntries)
                {
                    report.ObservedEntries.Add(entry.FullName);
                    if (!ContainsAviEntry(entry))
                    {
                        report.Errors.Add($"{entry.FullName} does not contain any .avi clips.");
                    }
                }
            }
            catch (Exception exception)
            {
                report.Errors.Add($"Failed to read video archive: {exception.Message}");
            }

            return report;
        }

        private static bool ContainsAviEntry(ZipArchiveEntry tgzEntry)
        {
            using var compressedStream = tgzEntry.Open();
            using var gzip = new GZipStream(compressedStream, CompressionMode.Decompress);
            var header = new byte[512];

            while (true)
            {
                if (!TryReadExact(gzip, header, 0, header.Length))
                {
                    return false;
                }

                if (IsEmptyTarBlock(header))
                {
                    return false;
                }

                var entryName = ReadTarEntryName(header);
                var entrySize = ReadTarEntrySize(header);
                if (entryName.EndsWith(".avi", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var padding = (512 - (entrySize % 512)) % 512;
                if (!SkipBytes(gzip, entrySize + padding))
                {
                    return false;
                }
            }
        }

        private static string ReadTarEntryName(byte[] header)
        {
            var name = ReadNullTerminatedAscii(header, 0, 100);
            var prefix = ReadNullTerminatedAscii(header, 345, 155);
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                return $"{prefix}/{name}";
            }

            return name;
        }

        private static long ReadTarEntrySize(byte[] header)
        {
            var sizeText = ReadNullTerminatedAscii(header, 124, 12).Trim();
            if (string.IsNullOrWhiteSpace(sizeText))
            {
                return 0;
            }

            return Convert.ToInt64(sizeText, 8);
        }

        private static string ReadNullTerminatedAscii(byte[] buffer, int offset, int length)
        {
            var chars = new char[length];
            var count = 0;
            for (var index = 0; index < length && offset + index < buffer.Length; index++)
            {
                var value = buffer[offset + index];
                if (value == 0)
                {
                    break;
                }

                chars[count++] = (char)value;
            }

            return new string(chars, 0, count).Trim();
        }

        private static bool IsEmptyTarBlock(byte[] block)
        {
            for (var index = 0; index < block.Length; index++)
            {
                if (block[index] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryReadExact(Stream stream, byte[] buffer, int offset, int count)
        {
            var read = 0;
            while (read < count)
            {
                var current = stream.Read(buffer, offset + read, count - read);
                if (current <= 0)
                {
                    return false;
                }

                read += current;
            }

            return true;
        }

        private static bool SkipBytes(Stream stream, long bytesToSkip)
        {
            var buffer = new byte[4096];
            while (bytesToSkip > 0)
            {
                var chunk = (int)Math.Min(buffer.Length, bytesToSkip);
                var current = stream.Read(buffer, 0, chunk);
                if (current <= 0)
                {
                    return false;
                }

                bytesToSkip -= current;
            }

            return true;
        }
    }
}
#endif
