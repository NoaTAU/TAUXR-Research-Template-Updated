// Writes session_metadata.json with detected skeleton sizes and flags.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


namespace TXRData
{

    [Serializable]
    public sealed class SessionMetaData
    {
        // ---- Identity / timing ----
        public string session_id = "";
        public string utc_start_iso8601 = "";
        public string device_utc_offset = "";

        // ---- Unity / platform ----
        public string unity_version = "";
        public string platform = "";

        // ---- Build info ----
        public string build_id = "";
        public string git_commit = "";
        public string utc_build_iso8601 = "";

        // ---- OVR / SDK versions ----
        public string ovrplugin_runtime_version = "";   // OVRPlugin.version.ToString()
        public string ovrplugin_wrapper_version = "";   // OVRPlugin.wrapperVersion.ToString()

        // ---- Sampling / clocks ----
        public string sampling_mode = "FixedUpdate";
        public float fixedDeltaTime = 0f;
        public float timeScale = 1f;
        public string ovr_step_name = OvrSampling.StepDefault.ToString();        // "Physics" or "Render"
        public int ovr_step_value = 0;                  // 0 or -1

        // ---- Schema / rotation ----
        public string schema_rev = "2";
        public string rotation_euler_order = "ZXY";
        public string rotation_units = "degrees";

        // ---- Feature toggles ----
        public bool face_enabled;
        public bool body_enabled;
        public bool hands_enabled;
        public bool eyes_enabled;
        public bool controllers_enabled;

        // where the per-hand skeleton JSONs are written
        public string left_hand_skeleton_json = "";
        public string right_hand_skeleton_json = "";

        // ---- Detected skeleton sizes (for traceability) ----
        public int detected_hand_bones = 0;
        public bool overprovisioned_hand_bones = false;
        public int detected_body_joints = 0;
        public bool overprovisioned_body_joints = false;
        public int detected_face_expr_count = 0; // e.g., 70

        // ---- Compact data source provenance ----
        public Dictionary<string, string> data_sources = new();
    }

    public static class SessionMetaWriter
    {
        private static string FileName = "session_metadata.json";
        public static string GetPath(string directory) => Path.Combine(directory, FileName);

        public static void WriteInitial(string directory, string fileNamePrefix, SessionMetaData meta)
        {
            FileName = string.IsNullOrWhiteSpace(fileNamePrefix) ? FileName : $"{fileNamePrefix}_{FileName}";
            Directory.CreateDirectory(directory);
            var json = JsonUtility.ToJson(meta, prettyPrint: true);
            AtomicWrite(GetPath(directory), json);
        }

        // Small safety: write to .tmp then move
        private static void AtomicWrite(string path, string json)
        {
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
    }

}