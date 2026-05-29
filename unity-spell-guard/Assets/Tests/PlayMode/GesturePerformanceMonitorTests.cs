using NUnit.Framework;
using SpellGuard.Diagnostics;
using SpellGuard.InputSystem;
using UnityEngine;

namespace SpellGuard.Tests.PlayMode
{
    public class GesturePerformanceMonitorTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("GesturePerformanceMonitorTestsRoot");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void BuildCsvContainsExpectedHeader()
        {
            var monitor = root.AddComponent<GesturePerformanceMonitor>();
            monitor.StartRecording();

            var csv = monitor.BuildCsv();

            StringAssert.Contains("section,session_id,mode,source,elapsed_seconds,total_frames", csv);
            StringAssert.Contains("avg_estimated_latency_ms", csv);
            StringAssert.Contains("avg_hand_update_interval_ms", csv);
            StringAssert.Contains("camera_device", csv);
            StringAssert.Contains("camera_fps", csv);
            StringAssert.Contains("camera_requested_fps", csv);
            StringAssert.Contains("camera_uses_requested_format", csv);
            StringAssert.Contains("native_fresh_frame_only", csv);
            StringAssert.Contains("native_result_fps", csv);
            StringAssert.Contains("timeseries", csv);
            StringAssert.Contains("body_shift_right", csv);
            StringAssert.Contains("external_raw_point_packets", csv);
            StringAssert.Contains("external_motion_packets", csv);
            StringAssert.Contains("external_predicted_packets", csv);
            StringAssert.Contains("last_external_performance", csv);
            StringAssert.Contains("sparse_last_reason", csv);
        }

        [Test]
        public void ExportCsvUsesModeSpecificFileName()
        {
            var router = root.AddComponent<GestureInputRouter>();
            var monitor = root.AddComponent<GesturePerformanceMonitor>();
            monitor.Configure(router, null);
            SetPrivateField(monitor, "outputDirectoryName", "Temp/GesturePerformanceMonitorTests");
            SetPrivateField(monitor, "createReadmeOnExport", false);
            monitor.StartRecording();

            var path = monitor.ExportCsv();

            StringAssert.Contains("gesture_performance_external_", System.IO.Path.GetFileName(path));
            Assert.That(System.IO.File.Exists(path), Is.True);
            System.IO.File.Delete(path);
        }

        [Test]
        public void ConfigureAcceptsExistingRouterAndBridge()
        {
            var router = root.AddComponent<GestureInputRouter>();
            var bridge = root.AddComponent<ExternalGestureBridgeProvider>();
            var monitor = root.AddComponent<GesturePerformanceMonitor>();

            monitor.Configure(router, bridge);
            monitor.StartRecording();

            var summary = monitor.CurrentSummary;

            Assert.That(summary.Mode, Is.EqualTo(GestureInputRouter.InputMode.ExternalBridge.ToString()));
            Assert.That(summary.Source, Is.EqualTo("无"));
        }

        [Test]
        public void WebcamHealthProbeBuildCsvContainsCameraProbeHeader()
        {
            var probe = root.AddComponent<WebcamHealthProbe>();

            var csv = probe.BuildCsv();

            StringAssert.Contains("requested_width", csv);
            StringAssert.Contains("actual_width", csv);
            StringAssert.Contains("p95_interval_ms", csv);
            StringAssert.Contains("stall_count", csv);
            StringAssert.Contains("low_resolution_candidate", csv);
            StringAssert.Contains("is_best", csv);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
