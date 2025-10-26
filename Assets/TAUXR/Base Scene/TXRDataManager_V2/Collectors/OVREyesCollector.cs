// OVREyesCollector.cs
// Eye gazes from OVRPlugin + FocusedObject/HitPoint from TXRPlayer.Instance.

using System;
using UnityEngine;
using static OVRPlugin;   // EyeGazesState, Eye, Step
using static TXRData.CollectorUtils;

namespace TXRData
{
    public sealed class OVREyesCollector : IContinuousCollector
    {
        public string CollectorName => "OVREyesCollector";
        private const Step SampleStep = OvrSampling.StepDefault;
        private const int LatestFrame = OvrSampling.LatestFrame;

        // TXRPlayer reference (assigned to TXRPlayer.Instance in Configure)
        private TXRPlayer _player;

        // Dedicated eye block
        private int _idxRightPitch = -1, _idxRightYaw = -1;
        private int _idxLeftPitch = -1, _idxLeftYaw = -1;
        private int _idxLeftValid = -1, _idxLeftConf = -1;
        private int _idxRightValid = -1, _idxRightConf = -1;
        private int _idxEyesTime = -1;

        // Legacy gaze (from TXRPlayer)
        private int _idxFocusedObject = -1;
        private int _idxHitX = -1, _idxHitY = -1, _idxHitZ = -1;

        private bool _writeDedicatedEyes = false;
        private bool _writeLegacyGaze = false;

        public void Configure(ColumnIndex schema, RecordingOptions options)
        {
            if (schema == null) throw new ArgumentNullException(nameof(schema));
            if (options == null) options = new RecordingOptions();

            _writeDedicatedEyes = options.includeEyes;
            _writeLegacyGaze = options.includeGaze;

            _player = TXRPlayer.Instance;

            if (_writeDedicatedEyes)
            {
                TryIndex(schema, "RightEye_Pitch", out _idxRightPitch);
                TryIndex(schema, "RightEye_Yaw", out _idxRightYaw);
                TryIndex(schema, "LeftEye_Pitch", out _idxLeftPitch);
                TryIndex(schema, "LeftEye_Yaw", out _idxLeftYaw);

                TryIndex(schema, "LeftEye_IsValid", out _idxLeftValid);
                TryIndex(schema, "LeftEye_Confidence", out _idxLeftConf);
                TryIndex(schema, "RightEye_IsValid", out _idxRightValid);
                TryIndex(schema, "RightEye_Confidence", out _idxRightConf);

                TryIndex(schema, "Eyes_Time", out _idxEyesTime);
            }

            if (_writeLegacyGaze)
            {
                TryIndex(schema, "FocusedObject", out _idxFocusedObject);
                TryIndex(schema, "EyeGazeHitPosition_X", out _idxHitX);
                TryIndex(schema, "EyeGazeHitPosition_Y", out _idxHitY);
                TryIndex(schema, "EyeGazeHitPosition_Z", out _idxHitZ);
            }
        }

        public void Collect(RowBuffer row, float timeSinceStartup)
        {
            if (_writeDedicatedEyes)
            {
                EyeGazesState state = default;
                bool ok = GetEyeGazesState(SampleStep, LatestFrame, ref state);
                if (ok && state.EyeGazes != null && state.EyeGazes.Length >= (int)Eye.Count)
                {
                    // Right eye
                    EyeGazeState right = state.EyeGazes[(int)Eye.Right];
                    Quaternion qR = new Quaternion(right.Pose.Orientation.x, right.Pose.Orientation.y, right.Pose.Orientation.z, right.Pose.Orientation.w);
                    Vector3 eR = qR.eulerAngles;
                    SetIfValid(row, _idxRightPitch, eR.x);
                    SetIfValid(row, _idxRightYaw, eR.y);
                    SetIfValid(row, _idxRightValid, right.IsValid ? 1 : 0);
                    SetIfValid(row, _idxRightConf, right.Confidence);

                    // Left eye
                    EyeGazeState left = state.EyeGazes[(int)Eye.Left];
                    Quaternion qL = new Quaternion(left.Pose.Orientation.x, left.Pose.Orientation.y, left.Pose.Orientation.z, left.Pose.Orientation.w);
                    Vector3 eL = qL.eulerAngles;
                    SetIfValid(row, _idxLeftPitch, eL.x);
                    SetIfValid(row, _idxLeftYaw, eL.y);
                    SetIfValid(row, _idxLeftValid, left.IsValid ? 1 : 0);
                    SetIfValid(row, _idxLeftConf, left.Confidence);

                    // Shared timestamp
                    SetIfValid(row, _idxEyesTime, state.Time);
                }
            }

            if (_writeLegacyGaze)
            {
                if (!_player)
                {
                    _player = TXRPlayer.Instance;   // try again if TXRPlayer was not ready in Configure
                }
                if (_player)                        // only write if player exists
                {
                    // FocusedObject (null/destroy-safe)
                    if (_idxFocusedObject >= 0)
                    {
                        Transform focusedObject = _player.FocusedObject;
                        row.Set(_idxFocusedObject, focusedObject ? focusedObject.name : "");
                    }

                    // Hit point
                    Vector3 hit = _player.EyeGazeHitPosition;
                    SetIfValid(row, _idxHitX, hit.x);
                    SetIfValid(row, _idxHitY, hit.y);
                    SetIfValid(row, _idxHitZ, hit.z);
                }
            }
        }

        public void Dispose() { }

    }
}
