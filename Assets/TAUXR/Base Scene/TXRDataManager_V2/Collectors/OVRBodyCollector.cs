// OVRBodyCollector.cs
// Collects body tracking data (frame-level fields + per-joint pose/flags) via OVRPlugin.GetBodyState4.

using System;
using UnityEngine;
using static OVRPlugin;   // BodyState, BodyJointLocation, BodyJointSet, Step, Vector3f, Quatf
using static TXRData.CollectorUtils;
namespace TXRData
{
    public sealed class OVRBodyCollector : IContinuousCollector
    {
        public string CollectorName => "OVRBodyCollector";
        private const Step SampleStep = OvrSampling.StepDefault;   // Plugin maps Physics -> Render when needed
        private const BodyJointSet JointSet = BodyJointSet.FullBody; //UpperBody or FullBody if you enable it in options

        private bool _includeBody = false;
        private int _jointCount = 0;

        // Root/body state columns
        private int _idxBodyTime = -1;
        private int _idxBodyConfidence = -1;
        private int _idxBodyFidelity = -1;
        private int _idxBodyCalibrationStatus = -1;
        private int _idxBodySkeletonChangedCount = -1;

        // Per-joint column indices
        private int[] _idxPosX, _idxPosY, _idxPosZ;
        private int[] _idxQx, _idxQy, _idxQz, _idxQw;
        private int[] _idxFlags;

        public void Configure(ColumnIndex schema, RecordingOptions options)
        {
            if (schema == null) throw new ArgumentNullException(nameof(schema));
            if (options == null) options = new RecordingOptions();

            _includeBody = options.includeBody;
            if (!_includeBody) return;

            // Match the body joint count used by SchemaBuilder
            _jointCount = SchemaFactories.DetectBodyJointCount(out bool bodyDetectionOk);


            // Root/body state indices
            TryIndex(schema, "Body_Time", out _idxBodyTime);
            TryIndex(schema, "Body_Confidence", out _idxBodyConfidence);
            TryIndex(schema, "Body_Fidelity", out _idxBodyFidelity);
            TryIndex(schema, "Body_CalibrationStatus", out _idxBodyCalibrationStatus);
            TryIndex(schema, "Body_SkeletonChangedCount", out _idxBodySkeletonChangedCount);

            // Allocate and cache per-joint indices
            _idxPosX = new int[_jointCount];
            _idxPosY = new int[_jointCount];
            _idxPosZ = new int[_jointCount];

            _idxQx = new int[_jointCount];
            _idxQy = new int[_jointCount];
            _idxQz = new int[_jointCount];
            _idxQw = new int[_jointCount];

            _idxFlags = new int[_jointCount];

            string[] bodyJointNames = SchemaFactories.GetBodyJointNames();
            if (bodyJointNames.Length != _jointCount)
            {
                Debug.LogError($"[OVRBodyCollector] Body joint names count mismatch. Detected count: {_jointCount}, Names count: {bodyJointNames.Length}");
            }

            for (int jointIndex = 0; jointIndex < _jointCount; jointIndex++)
            {
                if (jointIndex < bodyJointNames.Length)
                {
                    string jointName = bodyJointNames[jointIndex];
                    _idxPosX[jointIndex] = IndexOrMinusOne(schema, $"{jointName}_px");
                    _idxPosY[jointIndex] = IndexOrMinusOne(schema, $"{jointName}_py");
                    _idxPosZ[jointIndex] = IndexOrMinusOne(schema, $"{jointName}_pz");

                    _idxQx[jointIndex] = IndexOrMinusOne(schema, $"{jointName}_qx");
                    _idxQy[jointIndex] = IndexOrMinusOne(schema, $"{jointName}_qy");
                    _idxQz[jointIndex] = IndexOrMinusOne(schema, $"{jointName}_qz");
                    _idxQw[jointIndex] = IndexOrMinusOne(schema, $"{jointName}_qw");

                    _idxFlags[jointIndex] = IndexOrMinusOne(schema, $"{jointName}_Flags");
                }
            }

        }

        public void Collect(RowBuffer row, float timeSinceStartup)
        {
            if (!_includeBody) return;

            BodyState bodyState = default;

            // Plugin API: GetBodyState4(step, jointSet, ref bodyState)
            if (!GetBodyState4(SampleStep, JointSet, ref bodyState)) return;  // fails if not active or unsupported. 

            // Frame-level fields
            SetIfValid(row, _idxBodyTime, bodyState.Time);                    // double.
            SetIfValid(row, _idxBodyConfidence, bodyState.Confidence);        // float. 
            SetIfValid(row, _idxBodyFidelity, bodyState.Fidelity.ToString());       // enum -> string. 
            SetIfValid(row, _idxBodyCalibrationStatus, bodyState.CalibrationStatus.ToString()); // enum -> string. 
            SetIfValid(row, _idxBodySkeletonChangedCount, bodyState.SkeletonChangedCount); // uint -> int ok. 

            // Per-joint
            int count = bodyState.JointLocations != null ? bodyState.JointLocations.Length : 0;
            int limit = Math.Min(_jointCount, count);

            for (int j = 0; j < limit; j++)
            {
                BodyJointLocation loc = bodyState.JointLocations[j];

                Vector3f p = loc.Pose.Position;
                SetIfValid(row, _idxPosX[j], p.x);
                SetIfValid(row, _idxPosY[j], p.y);
                SetIfValid(row, _idxPosZ[j], p.z);

                Quatf q = loc.Pose.Orientation;
                SetIfValid(row, _idxQx[j], q.x);
                SetIfValid(row, _idxQy[j], q.y);
                SetIfValid(row, _idxQz[j], q.z);
                SetIfValid(row, _idxQw[j], q.w);

                SetIfValid(row, _idxFlags[j], loc.LocationFlags.ToString());
            }
        }

        public void Dispose() { }

    }
}
