// TXRDataManager_V2.cs
// Builds schemas, opens CSVs, runs collectors every FixedUpdate.
// Researchers use: LogCustom(...) and the inspector list of custom transforms.

using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using static OVRPlugin;
using static TXRData.BuildInfoLoader;


namespace TXRData
{
    public sealed class TXRDataManager_V2 : TXRSingleton<TXRDataManager_V2>
    {
        #region fields and properties

        [Header("Editor Export")]
        public bool exportInEditor = false;

        [ShowIf(nameof(exportInEditor))]
        public string saveFilePath;  // if null/empty, falls back to tmp

        [Header("Output")]
        private string sessionTime;
        public string SessionTime => sessionTime; // read-only property
        private bool appendIfFilesExist = false;
        public string csvDelimiter = ",";

        [Header("What to record (ContinuousData)")]
        public RecordingOptions recordingOptions = new RecordingOptions();

        [Header("FaceExpressions.csv")]
        public bool recordFaceExpressions = true;

        // paths
        private string _rootDir;

        // schemas
        private ColumnIndex _continuousSchema;
        private ColumnIndex _faceSchema;

        // row buffers
        private RowBuffer _continuousRow;
        private RowBuffer _faceRow;

        // writers
        private CsvRowWriter _continuousWriter;
        private CsvRowWriter _faceWriter;

        // collectors
        private readonly List<IContinuousCollector> _continuousCollectors = new List<IContinuousCollector>();
        private OVRFaceCollector _faceCollector;

        #endregion

        #region unity lifecycle
        private void Awake()
        {
            // 0) session time suffix
            sessionTime = DateTime.UtcNow.ToString("yyyy.MM.dd_HH-mm");

            // 1) Output directory
            if (Application.isEditor && exportInEditor)
            {
                if (!string.IsNullOrWhiteSpace(saveFilePath))
                {
                    _rootDir = saveFilePath.Replace('\\', '/'); // normalize slashes
                    Directory.CreateDirectory(_rootDir);
                }
                else
                {
                    _rootDir = Path.Combine(Path.GetTempPath(), "TXR_EditorLogs");
                }
            }
            else
            {
                _rootDir = Application.persistentDataPath;
                //_rootDir = Path.Combine(Application.persistentDataPath, outputFolderName);
                //Directory.CreateDirectory(_rootDir);
            }

            // 2) Metadata
            WriteMetadata();

            // 3) StartBodyTracking if needed
            //Moved To Start() 

            // 4) Build schemas
            var cont = SchemaFactories.BuildContinuousDataV2(recordingOptions);  // (schema, counts, flags)
            _continuousSchema = cont.schema;

            var face = SchemaFactories.BuildFaceExpressionsV2();                 // (schema, counts)
            _faceSchema = face.schema;

            // 5) Writers
            string contPath = Path.Combine(_rootDir, $"{sessionTime}_ContinuousData.csv");
            _continuousWriter = new CsvRowWriter(contPath, csvDelimiter, null, appendIfFilesExist);

            if (recordFaceExpressions)
            {
                string facePath = Path.Combine(_rootDir, $"{sessionTime}_FaceExpressionData.csv");
                _faceWriter = new CsvRowWriter(facePath, csvDelimiter, null, appendIfFilesExist);
            }

            // 6) Row buffers
            _continuousRow = new RowBuffer(_continuousSchema);
            _faceRow = recordFaceExpressions ? new RowBuffer(_faceSchema) : null;

            // 7) Initialize Collectors for ContinuousData
            if (recordingOptions.includeNodes) _continuousCollectors.Add(new OVRNodesCollector());
            if (recordingOptions.includeEyes) _continuousCollectors.Add(new OVREyesCollector());
            if (recordingOptions.includeHands) _continuousCollectors.Add(new OVRHandsCollector());
            if (recordingOptions.includeBody) _continuousCollectors.Add(new OVRBodyCollector());
            if (recordingOptions.includeRecenter) _continuousCollectors.Add(new RecenterCollector());
            if (recordingOptions.includePerformance) _continuousCollectors.Add(new OVRPerformanceCollector());
            if (recordingOptions.customTransformsToRecord != null &&
                recordingOptions.customTransformsToRecord.Count > 0)
                _continuousCollectors.Add(new CustomTransformsCollector());

            foreach (var c in _continuousCollectors)
                c.Configure(_continuousSchema, recordingOptions);

            if (recordFaceExpressions)
            {
                _faceCollector = new OVRFaceCollector();
                _faceCollector.Configure(_faceSchema, recordingOptions);
            }

            // 8) Custom data tables: set base directory + delimiter once
            CustomCsvFromDataClass.Initialize(_rootDir, csvDelimiter, sessionTime);
        }

        private async void Start()
        {
            if (recordingOptions.includeBody)
            {
                await StartBodyTrackingAsync(BodyJointSet.FullBody, BodyTrackingFidelity2.High);
            }
        }

        private void FixedUpdate()
        {
            float t = Time.realtimeSinceStartup;

            // ContinuousData row
            _continuousRow.Clear();
            _continuousRow.TrySet("timeSinceStartup", t);                        // friendly setter :contentReference[oaicite:9]{index=9}
            for (int i = 0; i < _continuousCollectors.Count; i++)
                _continuousCollectors[i].Collect(_continuousRow, t);

            // write row
            _continuousWriter.WriteRow(_continuousSchema,
                                       _continuousRow.ValuesArray,
                                       _continuousRow.ColumnIsSetMask);         // WriteRow signature 

            // FaceExpressions row
            if (recordFaceExpressions && _faceCollector != null)
            {
                _faceRow.Clear();
                _faceRow.TrySet("timeSinceStartup", t);
                _faceCollector.Collect(_faceRow, t);

                _faceWriter.WriteRow(_faceSchema,
                                     _faceRow.ValuesArray,
                                     _faceRow.ColumnIsSetMask);
            }
        }

        private void OnDestroy()
        {
            // collectors
            for (int i = 0; i < _continuousCollectors.Count; i++)
            {
                try { _continuousCollectors[i].Dispose(); } catch { }
            }
            _continuousCollectors.Clear();

            // writers
            try { _continuousWriter?.Dispose(); } catch { }
            try { _faceWriter?.Dispose(); } catch { }

            // custom tables
            try { CustomCsvFromDataClass.CloseAll(); } catch { }
        }

        #endregion

        #region Tracking Initializing 
        public async UniTask StartBodyTrackingAsync(BodyJointSet jointSet = BodyJointSet.FullBody,
                                                   BodyTrackingFidelity2 fidelity = BodyTrackingFidelity2.High)
        {
            // Wait a couple of frames so OVR/Link fully initializes
            await UniTask.Yield(PlayerLoopTiming.Update);
            await UniTask.Yield(PlayerLoopTiming.Update);

            Debug.Log($"[DataManager_V2] Body Tracking Supported={OVRPlugin.bodyTrackingSupported}");

            // Request fidelity first, then try v2 start, then fallback to legacy start
            OVRPlugin.RequestBodyTrackingFidelity(fidelity);
            bool started = OVRPlugin.StartBodyTracking2(jointSet);
            if (!started)
                started = OVRPlugin.StartBodyTracking();

            // Wait up to ~2 seconds for the runtime to enable body tracking
            float timeout = 2f;
            float elapsed = 0f;

            while (elapsed < timeout && !OVRPlugin.bodyTrackingEnabled)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                elapsed += Time.unscaledDeltaTime;
            }

            Debug.Log($"[DataManager_V2] Body Tracking started={started}, Body Tracking enabled={OVRPlugin.bodyTrackingEnabled}, set={jointSet}, fid={fidelity}");
        }
        #endregion

        #region custom DataClass logging
        // ---------- minimal API for researchers ----------

        // Create & write a row to <TableName>.csv using a custom data class instance.
        public void LogCustom(CustomDataClass data)
        {
            if (data == null) return;
            CustomCsvFromDataClass.Write(data);
        }

        // Overload that builds the object on demand (avoids allocations at call site).
        public void LogCustom(Func<CustomDataClass> make)
        {
            if (make == null) return;
            var inst = make();
            if (inst == null) return;
            CustomCsvFromDataClass.Write(inst);
        }

        #endregion

        public string GetOutputDirectory() => _rootDir;

        #region METADATA

        private void WriteMetadata()
        {

            // 0) Capture skeletons once (if hands are enabled)
            OVRPlugin.Skeleton2 leftSkel = default, rightSkel = default;
            bool haveLeft = recordingOptions.includeHands && OVRPlugin.GetSkeleton2(OVRPlugin.SkeletonType.HandLeft, ref leftSkel);
            bool haveRight = recordingOptions.includeHands && OVRPlugin.GetSkeleton2(OVRPlugin.SkeletonType.HandRight, ref rightSkel);

            // 1) Build the object
            var meta = new SessionMetaData
            {
                // session and identity
                session_id = sessionTime,
                utc_start_iso8601 = DateTime.UtcNow.ToString("o"),
                device_utc_offset = TimeZoneInfo.Local.BaseUtcOffset.ToString(),
                platform = Application.platform.ToString(),
                unity_version = Application.unityVersion,

                // feature toggles
                eyes_enabled = recordingOptions.includeEyes,
                hands_enabled = recordingOptions.includeHands,
                body_enabled = recordingOptions.includeBody,
                face_enabled = recordFaceExpressions,
                controllers_enabled = true, // TODO update if we actually gate controllers

                // OVR sampling (document the choice)
                ovr_step_name = OvrSampling.StepDefault.ToString(),
                ovr_step_value = (int)OvrSampling.StepDefault,

                // sampeling timing
                sampling_mode = "FixedUpdate",
                timeScale = Time.timeScale,
                fixedDeltaTime = Time.fixedDeltaTime,

                // for rotation conversion
                rotation_units = "degrees",
                rotation_euler_order = "XYZ",

            };


            // fill detected_hand_bones if we have a skeleton
            if (haveLeft) meta.detected_hand_bones = Math.Max(meta.detected_hand_bones, (int)leftSkel.NumBones);
            if (haveRight) meta.detected_hand_bones = Math.Max(meta.detected_hand_bones, (int)rightSkel.NumBones);

            // 2) Write per-hand skeleton JSONs (if available)
            if (haveLeft)
            {
                meta.left_hand_skeleton_json = Path.Combine(_rootDir, $"{sessionTime}_HandSkeleton_Left.json");
                //WriteHandSkeletonJson(meta.left_hand_skeleton_json, leftSkel);
            }
            if (haveRight)
            {
                meta.right_hand_skeleton_json = Path.Combine(_rootDir, $"{sessionTime}_HandSkeleton_Right.json");
                //riteHandSkeletonJson(meta.right_hand_skeleton_json, rightSkel);
            }

            // 2) Build info (player build) or editor fallback
            BuildInfo bi = BuildInfoLoader.Instance != null ? BuildInfoLoader.Instance.Current : null;

#if UNITY_EDITOR
            // In Editor we likely don’t have a real build_info.json—stamp an editor ID
            meta.build_id = $"EDITOR_{SessionTime}";
            meta.utc_build_iso8601 = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            meta.git_commit = "N/A";
            // If AutoBuildInfo generated something for editor you can still prefer it:
            if (bi != null && !string.IsNullOrWhiteSpace(bi.build_id))
            {
                meta.build_id = bi.build_id;
                meta.utc_build_iso8601 = bi.utc_build_iso8601;
                meta.unity_version = string.IsNullOrEmpty(bi.unity) ? meta.unity_version : bi.unity;
                meta.git_commit = bi.git_commit;
            }
#else
            // On device / player builds we expect StreamingAssets/build_info.json
            if (bi != null)
            {
                meta.build_id          = bi.build_id;
                meta.utc_build_iso8601 = bi.utc_build_iso8601;
                meta.unity_version     = string.IsNullOrEmpty(bi.unity) ? meta.unity_version : bi.unity;
                meta.git_commit        = bi.git_commit;
            }
            else
            {
                // Fallback if missing
                meta.build_id          = $"NO_BUILDINFO_{SessionTime}";
                meta.utc_build_iso8601 = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                meta.git_commit        = "unknown";
            }
#endif

            // 3) (Optional) sprinkle OVR/OVRPlugin versions if available; wrap in try to avoid hard deps
            try
            {
                meta.ovrplugin_wrapper_version = OVRPlugin.wrapperVersion.ToString();
                meta.ovrplugin_runtime_version = OVRPlugin.version.ToString();

            }
            catch { /* safe no-op if OVR not present */ }

            // 4) Finally write
            SessionMetaWriter.WriteInitial(GetOutputDirectory(), SessionTime, meta);
        }
        #endregion

    }




    [Serializable]
    public class HandSkeletonMeta
    {
        public string skeleton_type;        // "HandLeft" or "HandRight"
        public int num_bones;
        public int[] parent_index;         // length = num_bones
        public string[] bone_id;            // human readable, optional
        public float[][] bind_pos;          // [i][x,y,z]
        public float[][] bind_rot;          // [i][x,y,z,w]


        public static void WriteHandSkeletonJson(string path, OVRPlugin.Skeleton2 sk)
        {
            var m = new HandSkeletonMeta
            {
                skeleton_type = sk.Type.ToString(),
                num_bones = (int)sk.NumBones,
                parent_index = new int[sk.NumBones],
                bone_id = new string[sk.NumBones],
                bind_pos = new float[sk.NumBones][],
                bind_rot = new float[sk.NumBones][]
            };

            for (int i = 0; i < sk.NumBones; i++)
            {
                var b = sk.Bones[i];
                m.parent_index[i] = b.ParentBoneIndex;
                m.bone_id[i] = b.Id.ToString();
                m.bind_pos[i] = new[] { b.Pose.Position.x, b.Pose.Position.y, b.Pose.Position.z };
                m.bind_rot[i] = new[] { b.Pose.Orientation.x, b.Pose.Orientation.y, b.Pose.Orientation.z, b.Pose.Orientation.w };
            }

            var json = JsonUtility.ToJson(m, true);
            File.WriteAllText(path, json);
        }
    }
}


