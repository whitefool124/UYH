using System;
using System.IO;
using System.IO.Compression;
using NUnit.Framework;
using SpellGuard.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpellGuard.Tests.EditMode
{
    public class TrainingDatasetValidatorTests
    {
        private string tempRoot;

        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(Path.GetTempPath(), $"SpellGuardDatasetTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }

        [Test]
        public void ValidatesLiveDatasetRootStructure()
        {
            var report = TrainingDatasetValidator.ValidateDatasetRoot(TrainingDatasetValidator.ResolveDefaultDatasetRoot());

            Assert.That(report.IsValid, Is.True, string.Join("\n", report.Errors));
            Assert.That(report.Annotations.IsValid, Is.True, string.Join("\n", report.Annotations.Errors));
            Assert.That(report.Annotations.MissingEntries, Is.Empty);
            Assert.That(report.Videos.Count, Is.GreaterThan(0));
            Assert.That(report.Videos.TrueForAll(video => video.IsValid), Is.True);
        }

        [Test]
        public void RejectsAnnotationsZipMissingRequiredFile()
        {
            var sourceZip = FindSingleZip(TrainingDatasetValidator.ResolveDefaultDatasetRoot(), "annotations*.zip");
            var tamperedZip = Path.Combine(tempRoot, "annotations-tampered.zip");
            CreateZipWithoutEntry(sourceZip, tamperedZip, "metadata.csv");

            var report = TrainingDatasetValidator.ValidateAnnotationsZip(tamperedZip);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.MissingEntries, Does.Contain("metadata.csv"));
        }

        [Test]
        public void RejectsVideoZipWithoutTgz()
        {
            var emptyZip = Path.Combine(tempRoot, "videos-empty.zip");
            using (var archive = ZipFile.Open(emptyZip, ZipArchiveMode.Create))
            {
                archive.CreateEntry("notes.txt");
            }

            var report = TrainingDatasetValidator.ValidateVideosZip(emptyZip);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors, Does.Contain("No .tgz archives found inside video zip."));
        }

        private static string FindSingleZip(string root, string pattern)
        {
            var files = Directory.GetFiles(root, pattern, SearchOption.TopDirectoryOnly);
            Assert.That(files.Length, Is.EqualTo(1), $"Expected exactly one {pattern} under {root}");
            return files[0];
        }

        private static void CreateZipWithoutEntry(string sourceZip, string targetZip, string entryNameToRemove)
        {
            using var sourceStream = File.OpenRead(sourceZip);
            using var sourceArchive = new ZipArchive(sourceStream, ZipArchiveMode.Read);
            using var targetStream = File.Create(targetZip);
            using var targetArchive = new ZipArchive(targetStream, ZipArchiveMode.Create);

            foreach (var entry in sourceArchive.Entries)
            {
                if (string.Equals(Path.GetFileName(entry.FullName), entryNameToRemove, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var targetEntry = targetArchive.CreateEntry(entry.FullName, System.IO.Compression.CompressionLevel.Optimal);
                using var entrySource = entry.Open();
                using var entryTarget = targetEntry.Open();
                entrySource.CopyTo(entryTarget);
            }
        }
    }
}
