// SchemaBuilder.cs
// Builds column-name schemas. Also includes runtime-detecting factories and face-expression names from the SDK.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;                  // Debug.LogWarning
using static OVRPlugin;             // runtime skeleton detection

namespace TXRData
{
    // Thin builder that only deals in column names.
    public sealed class SchemaBuilder
    {
        private readonly List<string> _names = new();

        public SchemaBuilder Add(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Column name cannot be empty", nameof(name));
            _names.Add(name);
            return this;
        }

        // Adds columns like $"{prefix}_{item}" for each item.
        // e.g. prefix=HeadPos, items=[X,Y,Z] -> HeadPos_X, HeadPos_Y, HeadPos_Z
        // If prefix is empty or null, just adds the items as-is.
        public SchemaBuilder AddMany(string prefix, IEnumerable<string> items)
        {
            if (!string.IsNullOrWhiteSpace(prefix))
                prefix = $"{prefix}_";

            foreach (string item in items)
            {
                if (string.IsNullOrWhiteSpace(item))
                    throw new ArgumentException("Column item name cannot be empty in AddMany");
                _names.Add($"{prefix}{item}");
            }

            return this;
        }

        // Adds a numbered sequence: $"{prefix}_{index}" with formatting.
        public SchemaBuilder AddFromRange(string prefix, int startInclusive, int count, string indexFormat = "D2")
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            for (int offset = 0; offset < count; offset++)
                _names.Add($"{prefix}_{(startInclusive + offset).ToString(indexFormat)}");

            return this;
        }

        // Add one column per enum value.
        // Example:
        //   enum MuseumZone { Lobby, RoomA, RoomB }
        //   builder.AddFromEnum<MuseumZone>("TimeInZone");
        // -> TimeInZone_Lobby, TimeInZone_RoomA, TimeInZone_RoomB
        public SchemaBuilder AddFromEnum<TEnum>(string prefix) where TEnum : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentException("prefix cannot be empty", nameof(prefix));

            foreach (string enumName in Enum.GetNames(typeof(TEnum)))
                _names.Add($"{prefix}_{enumName}");

            return this;
        }

        public ColumnIndex Build()
        {
            ColumnIndex columnIndex = new ColumnIndex();
            foreach (string name in _names)
                columnIndex.Add(name);
            return columnIndex;
        }
    }

    // Options that control what goes into the ContinuousData schema (exposed in DataManager inspector).
    [Serializable]
    public sealed class RecordingOptions
    {
        public bool includeNodes = true;        // Head/Hands/Controllers/Eyes node poses
        public bool includeEyes = true;         // Dedicated eye API (pitch/yaw/valid/time)
        public bool includeHands = true;        // Hand skeleton arrays
        public bool includeBody = true;         // Body skeleton arrays
        public bool includePerformance = true;         // AppMotionToPhotonLatency
        public bool includeGaze = true;         // FocusedObject, EyeGazeHitPosition
        public bool includeRecenter = true;     // shouldRecenter, recenterEvent

        // Researchers can drag Transforms in the inspector
        public List<Transform> customTransformsToRecord = new();
    }

    public static class SchemaFactories
    {
        // --------- Runtime detection helpers ---------

        public static int DetectHandBoneCount(out bool success)
        {
            success = false;

            OVRHandSkeletonVersion handSkeletonVersion = OVRRuntimeSettings.Instance.HandSkeletonVersion;

            if (handSkeletonVersion == OVRHandSkeletonVersion.OpenXR)
            {
                // OpenXR hands path
                success = true;
                return (int)OVRPlugin.SkeletonConstants.MaxXRHandBones; // TODO should we use getskeleton2 instead? has bones[] array
            }
            else if (handSkeletonVersion == OVRHandSkeletonVersion.OVR)
            {
                // OVR hands path
                success = true;
                return (int)OVRPlugin.SkeletonConstants.MaxHandBones;
            }

            return 0;
        }

        public static int DetectBodyJointCount(out bool success)
        {
            success = false;
            try
            {
                Skeleton2 skel = new Skeleton2();
                if (GetSkeleton2(SkeletonType.Body, ref skel) && skel.NumBones > 0)
                {
                    success = true;
                    return (int)skel.NumBones;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SchemaFactories] Body skeleton detection failed: {e.Message}");
            }
            Debug.LogWarning("[SchemaFactories] Body skeleton detection failed, defaulting to SkeletonConstants.MaxBodyBones.");
            return (int)SkeletonConstants.MaxBodyBones; // safe fallback (may over-provision)
        }

        public static string[] GetHandBonesNames(out bool success)
        {
            success = false;
            OVRHandSkeletonVersion handSkeletonVersion = OVRRuntimeSettings.Instance.HandSkeletonVersion;

            // Get all enum values of BoneId
            string[] allNames = Enum.GetNames(typeof(BoneId));

            if (handSkeletonVersion == OVRHandSkeletonVersion.OpenXR)
            {
                // Filter for only XRHand bones
                // Exactly the 26 XR joints (excluding Start/Max/End)
                string[] xrHandNames = allNames
                    .Where(n => n.StartsWith("XRHand_"))
                    .Where(n => n != nameof(BoneId.XRHand_Start) &&
                                n != nameof(BoneId.XRHand_Max) &&
                                n != nameof(BoneId.XRHand_End))
                    .ToArray();

                success = xrHandNames.Length == 26;
                return xrHandNames;
            }
            else if (handSkeletonVersion == OVRHandSkeletonVersion.OVR)
            {
                // Filter for only OVR bones
                string[] ovrAllHandNames = allNames
                    .Where(n => n.StartsWith("Hand_"))
                    .Where(n => n != nameof(BoneId.Hand_Start) &&
                                n != nameof(BoneId.Hand_MaxSkinnable) &&
                                n != nameof(BoneId.Hand_End))
                    .ToArray();
                success = ovrAllHandNames.Length > 0;
                return ovrAllHandNames;
            }
            return Array.Empty<string>();
        }

        public static string[] GetBodyJointNames()
        {
            // Get all enum values of BoneId
            string[] allNames = Enum.GetNames(typeof(BoneId));

            // Filter for only FullBody joints
            string[] bodyJointNames = allNames
                .Where(n => n.StartsWith("Body"))
                .Where(n => n != nameof(BoneId.Body_Start) &&
                            n != nameof(BoneId.Body_End))
                .ToArray();
            return bodyJointNames;
        }

        // --------- Schema builders ---------

        // Build ContinuousData schema based on options and runtime counts.
        // Returns schema and the detected counts + over-provision flags (for metadata).
        public static (ColumnIndex schema, int handBones, bool handOverprovisioned,
                       int bodyJoints, bool bodyOverprovisioned)
            BuildContinuousDataV2(RecordingOptions recordingOptions)
        {
            SchemaBuilder schemaBuilder = new SchemaBuilder();

            int detectedHandBoneCount = DetectHandBoneCount(out bool handDetectionOk);
            int detectedBodyJointCount = DetectBodyJointCount(out bool bodyDetectionOk);

            // Timing
            schemaBuilder.Add("timeSinceStartup"); // Unity Time.realTimeSinceStartup

            // Legacy head pose (Euler) from Head node
            if (recordingOptions.includeNodes)
            {
                schemaBuilder.Add("Head_Position_x");
                schemaBuilder.Add("Head_Height");
                schemaBuilder.Add("Head_Position_z");
                schemaBuilder.AddMany("Gaze", new[] { "Pitch", "Yaw", "Roll" });
                schemaBuilder.Add("HeadNodeOrientationValid");
                schemaBuilder.Add("HeadNodePositionValid");
                schemaBuilder.Add("HeadNodeOrientationTracked");
                schemaBuilder.Add("HeadNodePositionTracked");
                schemaBuilder.Add("HeadNodeTime");
            }

            // Legacy gaze / raycast
            if (recordingOptions.includeGaze)
            {
                schemaBuilder.Add("FocusedObject");
                schemaBuilder.AddMany("EyeGazeHitPosition", new[] { "X", "Y", "Z" });
            }

            // Eyes (dedicated API)
            if (recordingOptions.includeEyes)
            {
                schemaBuilder.Add("RightEye_Pitch");
                schemaBuilder.Add("RightEye_Yaw");
                schemaBuilder.Add("LeftEye_Pitch");
                schemaBuilder.Add("LeftEye_Yaw");
                schemaBuilder.Add("LeftEye_IsValid");
                schemaBuilder.Add("LeftEye_Confidence");
                schemaBuilder.Add("RightEye_IsValid");
                schemaBuilder.Add("RightEye_Confidence");
                schemaBuilder.Add("Eyes_Time");
            }

            // Recenter flags
            if (recordingOptions.includeRecenter)
            {
                schemaBuilder.Add("shouldRecenter");
                schemaBuilder.Add("recenterEvent");
            }

            // Device nodes
            if (recordingOptions.includeNodes)
            {
                string[] nodeNames =
                {
                    "EyeLeft","EyeRight","EyeCenter","Head",
                    "HandLeft","HandRight","ControllerLeft","ControllerRight"
                };

                foreach (string node in nodeNames)
                {
                    schemaBuilder.Add($"Node_{node}_Present");
                    schemaBuilder.AddMany($"Node_{node}", new[] { "px", "py", "pz" });
                    schemaBuilder.AddMany($"Node_{node}", new[] { "qx", "qy", "qz", "qw" });
                    schemaBuilder.AddMany($"Node_{node}_Vel", new[] { "x", "y", "z" });
                    schemaBuilder.AddMany($"Node_{node}_AngVel", new[] { "x", "y", "z" });
                    schemaBuilder.Add($"Node_{node}_Valid_Position");
                    schemaBuilder.Add($"Node_{node}_Valid_Orientation");
                    schemaBuilder.Add($"Node_{node}_Tracked_Position");
                    schemaBuilder.Add($"Node_{node}_Tracked_Orientation");
                    schemaBuilder.Add($"Node_{node}_Time");
                }
            }

            // Hands (skeleton arrays)
            if (recordingOptions.includeHands)
            {
                string[] handBoneNames = GetHandBonesNames(out bool handBoneNamesOk);
                if (!handBoneNamesOk || handBoneNames.Length != detectedHandBoneCount)
                {
                    Debug.LogError($"[SchemaFactories] Hand bone names detection failed or count mismatch. Detected count: {detectedHandBoneCount}, Names count: {handBoneNames.Length}");
                }

                foreach (string side in new[] { "Left", "Right" })
                {
                    schemaBuilder.Add($"{side}Hand_Status");
                    schemaBuilder.AddMany($"{side}Hand_Root", new[] { "px", "py", "pz" });
                    schemaBuilder.AddMany($"{side}Hand_Root", new[] { "qx", "qy", "qz", "qw" });
                    schemaBuilder.Add($"{side}Hand_HandScale");
                    schemaBuilder.Add($"{side}Hand_HandConfidence");

                    schemaBuilder.Add($"{side}Hand_FingerConf_Thumb");
                    schemaBuilder.Add($"{side}Hand_FingerConf_Index");
                    schemaBuilder.Add($"{side}Hand_FingerConf_Middle");
                    schemaBuilder.Add($"{side}Hand_FingerConf_Ring");
                    schemaBuilder.Add($"{side}Hand_FingerConf_Pinky");

                    schemaBuilder.Add($"{side}Hand_RequestedTS");
                    schemaBuilder.Add($"{side}Hand_SampleTS");

                    for (int boneIndex = 0; boneIndex < detectedHandBoneCount; boneIndex++)
                    {

                        string boneName = handBoneNames[boneIndex];
                        schemaBuilder.AddMany($"{side}_{boneName}", new[] { "x", "y", "z" });
                        schemaBuilder.AddMany($"{side}_{boneName}", new[] { "qx", "qy", "qz", "qw" });

                    }
                }
            }

            // Body (skeleton arrays)
            if (recordingOptions.includeBody)
            {
                schemaBuilder.Add("Body_Time");
                schemaBuilder.Add("Body_Confidence");
                schemaBuilder.Add("Body_Fidelity");
                schemaBuilder.Add("Body_CalibrationStatus");
                schemaBuilder.Add("Body_SkeletonChangedCount");

                string[] bodyJointNames = GetBodyJointNames();
                if (bodyJointNames.Length != detectedBodyJointCount)
                {
                    Debug.LogError($"[SchemaFactories] Body joint names count mismatch. Detected count: {detectedBodyJointCount}, Names count: {bodyJointNames.Length}");
                }

                for (int jointIndex = 0; jointIndex < detectedBodyJointCount; jointIndex++)
                {
                    if (jointIndex < bodyJointNames.Length)
                    {
                        string jointName = bodyJointNames[jointIndex];
                        schemaBuilder.AddMany($"{jointName}", new[] { "px", "py", "pz" });
                        schemaBuilder.AddMany($"{jointName}", new[] { "qx", "qy", "qz", "qw" });
                        schemaBuilder.Add($"{jointName}_Flags");
                    }
                }
            }

            // Perf
            if (recordingOptions.includePerformance)
                schemaBuilder.Add("AppMotionToPhotonLatency");

            // Custom transforms (from inspector)
            if (recordingOptions.customTransformsToRecord != null)
            {
                foreach (Transform transform in recordingOptions.customTransformsToRecord)
                {
                    if (transform == null) continue;
                    string name = transform.name;
                    schemaBuilder.AddMany($"Custom_{name}", new[] { "px", "py", "pz" });
                    schemaBuilder.AddMany($"Custom_{name}", new[] { "qx", "qy", "qz", "qw" });
                }
            }

            ColumnIndex schema = schemaBuilder.Build();
            bool handOverprovisioned = !handDetectionOk;
            bool bodyOverprovisioned = !bodyDetectionOk;

            return (schema, detectedHandBoneCount, handOverprovisioned, detectedBodyJointCount, bodyOverprovisioned);
        }

        // Build FaceExpressions schema using expression names from OVRPlugin.FaceExpression2, and region confidences from OVRPlugin.FaceRegionConfidence.
        // Returns schema and counts of expressions and confidences (for metadata).
        public static (ColumnIndex schema, int faceExprCount, int perExpressionConfidenceCount, int regionConfidenceCount)
        BuildFaceExpressionsV2()
        {
            SchemaBuilder schemaBuilder = new SchemaBuilder();

            // timing + state
            schemaBuilder.Add("timeSinceStartup");
            schemaBuilder.Add("Face_Time");
            schemaBuilder.Add("Face_Status");

            // expression names from enum (skip sentinels)
            List<string> expressionNames = new List<string>();
            foreach (string enumName in Enum.GetNames(typeof(OVRPlugin.FaceExpression2)))
            {
                if (enumName == "Invalid" || enumName == "Max") continue;
                expressionNames.Add(enumName);
            }
            int expressionCount = expressionNames.Count; // typically 70

            // weights: one column per expression name
            schemaBuilder.AddMany("", expressionNames); // e.g., Brow_Lowerer_L, Jaw_Drop, ...

            // region confidences: Upper/Lower (skip Max)
            List<string> regionNames = new List<string>();
            foreach (string region in Enum.GetNames(typeof(OVRPlugin.FaceRegionConfidence)))
            {
                if (region == "Max") continue;
                regionNames.Add(region);
            }
            schemaBuilder.AddMany("FaceRegionConfidence", regionNames); // RegionConf_Upper, RegionConf_Lower

            ColumnIndex built = schemaBuilder.Build();
            return (built, expressionCount, expressionCount, regionNames.Count);
        }
    }
}
