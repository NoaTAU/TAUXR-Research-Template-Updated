// OVRHandsCollector.cs
// Collects hand skeleton data from OVRPlugin.GetHandState().
// Logs status, root pose, scale, confidences, per-finger confidences, timestamps, and bone arrays.

using System;
using UnityEngine;
using static OVRPlugin;   // HandState, Skeleton, Posef, Hand, Step
using static TXRData.CollectorUtils;
namespace TXRData
{
    public sealed class OVRHandsCollector : IContinuousCollector
    {
        public string CollectorName => "OVRHandsCollector";
        private const Step SampleStep = OvrSampling.StepDefault;

        private struct HandCols
        {
            public int Status;
            public int RootPosX, RootPosY, RootPosZ;
            public int RootQx, RootQy, RootQz, RootQw;
            public int HandScale;
            public int HandConfidence;

            public int ConfThumb, ConfIndex, ConfMiddle, ConfRing, ConfPinky;

            public int RequestedTs;
            public int SampleTs;

            public int[] BonePosX;
            public int[] BonePosY;
            public int[] BonePosZ;
            public int[] BoneQx;
            public int[] BoneQy;
            public int[] BoneQz;
            public int[] BoneQw;
        }

        private HandCols _leftCols;
        private HandCols _rightCols;

        private int _handBoneCount = 0;
        private bool _includeHands = false;

        public void Configure(ColumnIndex schema, RecordingOptions options)
        {
            if (schema == null) throw new ArgumentNullException(nameof(schema));
            if (options == null) options = new RecordingOptions();

            _includeHands = options.includeHands;
            if (!_includeHands) return;

            // Use the same count the schema was built with
            _handBoneCount = SchemaFactories.DetectHandBoneCount(out bool handDetectionOk);
            if (!handDetectionOk)
            {
                Debug.LogError($"[OVRHandsCollector] Hand bone count detection failed. No hand bones will be outputted, defaulting to 0 bones.");
                _handBoneCount = 0;
            }

            _leftCols = CacheHandIndices(schema, "Left");
            _rightCols = CacheHandIndices(schema, "Right");
        }

        public void Collect(RowBuffer row, float timeSinceStartup)
        {
            if (!_includeHands) return;

            WriteHand(row, Hand.HandLeft, _leftCols);
            WriteHand(row, Hand.HandRight, _rightCols);
        }

        public void Dispose() { }

        // --- helpers ---

        private HandCols CacheHandIndices(ColumnIndex schema, string side)
        {
            HandCols cols = new HandCols();

            string[] handBoneNames = SchemaFactories.GetHandBonesNames(out bool handBoneNamesOk);
            if (!handBoneNamesOk || handBoneNames.Length != _handBoneCount)
            {
                Debug.LogError($"[SchemaFactories] Hand bone names detection failed or count mismatch. Detected count: {_handBoneCount}, Names count: {handBoneNames.Length}");
            }

            cols.Status = IndexOrMinusOne(schema, $"{side}Hand_Status");

            cols.RootPosX = IndexOrMinusOne(schema, $"{side}Hand_Root_px");
            cols.RootPosY = IndexOrMinusOne(schema, $"{side}Hand_Root_py");
            cols.RootPosZ = IndexOrMinusOne(schema, $"{side}Hand_Root_pz");

            cols.RootQx = IndexOrMinusOne(schema, $"{side}Hand_Root_qx");
            cols.RootQy = IndexOrMinusOne(schema, $"{side}Hand_Root_qy");
            cols.RootQz = IndexOrMinusOne(schema, $"{side}Hand_Root_qz");
            cols.RootQw = IndexOrMinusOne(schema, $"{side}Hand_Root_qw");

            cols.HandScale = IndexOrMinusOne(schema, $"{side}Hand_HandScale");
            cols.HandConfidence = IndexOrMinusOne(schema, $"{side}Hand_HandConfidence");

            cols.ConfThumb = IndexOrMinusOne(schema, $"{side}Hand_FingerConf_Thumb");
            cols.ConfIndex = IndexOrMinusOne(schema, $"{side}Hand_FingerConf_Index");
            cols.ConfMiddle = IndexOrMinusOne(schema, $"{side}Hand_FingerConf_Middle");
            cols.ConfRing = IndexOrMinusOne(schema, $"{side}Hand_FingerConf_Ring");
            cols.ConfPinky = IndexOrMinusOne(schema, $"{side}Hand_FingerConf_Pinky");

            cols.RequestedTs = IndexOrMinusOne(schema, $"{side}Hand_RequestedTS");
            cols.SampleTs = IndexOrMinusOne(schema, $"{side}Hand_SampleTS");

            cols.BonePosX = new int[_handBoneCount];
            cols.BonePosY = new int[_handBoneCount];
            cols.BonePosZ = new int[_handBoneCount];
            cols.BoneQx = new int[_handBoneCount];
            cols.BoneQy = new int[_handBoneCount];
            cols.BoneQz = new int[_handBoneCount];
            cols.BoneQw = new int[_handBoneCount];

            for (int i = 0; i < _handBoneCount; i++)
            {
                string boneName = handBoneNames[i];
                cols.BonePosX[i] = IndexOrMinusOne(schema, $"{side}_{boneName}_x");
                cols.BonePosY[i] = IndexOrMinusOne(schema, $"{side}_{boneName}_y");
                cols.BonePosZ[i] = IndexOrMinusOne(schema, $"{side}_{boneName}_z");

                cols.BoneQx[i] = IndexOrMinusOne(schema, $"{side}_{boneName}_qx");
                cols.BoneQy[i] = IndexOrMinusOne(schema, $"{side}_{boneName}_qy");
                cols.BoneQz[i] = IndexOrMinusOne(schema, $"{side}_{boneName}_qz");
                cols.BoneQw[i] = IndexOrMinusOne(schema, $"{side}_{boneName}_qw");
            }

            return cols;
        }

        private void WriteHand(RowBuffer row, Hand whichHand, HandCols cols)
        {
            HandState handState = default;

            // NOTE: signature is (Step, Hand, ref HandState)
            bool got = GetHandState(SampleStep, whichHand, ref handState);
            if (!got) return;

            SetIfValid(row, cols.Status, handState.Status.ToString());

            Posef root = handState.RootPose;
            SetIfValid(row, cols.RootPosX, root.Position.x);
            SetIfValid(row, cols.RootPosY, root.Position.y);
            SetIfValid(row, cols.RootPosZ, root.Position.z);
            SetIfValid(row, cols.RootQx, root.Orientation.x);
            SetIfValid(row, cols.RootQy, root.Orientation.y);
            SetIfValid(row, cols.RootQz, root.Orientation.z);
            SetIfValid(row, cols.RootQw, root.Orientation.w);

            SetIfValid(row, cols.HandScale, handState.HandScale);
            SetIfValid(row, cols.HandConfidence, handState.HandConfidence.ToString());

            // Finger confidences (array of 5)
            if (handState.FingerConfidences != null && handState.FingerConfidences.Length >= 5)
            {
                SetIfValid(row, cols.ConfThumb, handState.FingerConfidences[0].ToString());
                SetIfValid(row, cols.ConfIndex, handState.FingerConfidences[1].ToString());
                SetIfValid(row, cols.ConfMiddle, handState.FingerConfidences[2].ToString());
                SetIfValid(row, cols.ConfRing, handState.FingerConfidences[3].ToString());
                SetIfValid(row, cols.ConfPinky, handState.FingerConfidences[4].ToString());
            }

            // Timestamps (double)
            SetIfValid(row, cols.RequestedTs, handState.RequestedTimeStamp);
            SetIfValid(row, cols.SampleTs, handState.SampleTimeStamp);

            // Bone arrays
            int positionsCount = handState.BonePositions != null ? handState.BonePositions.Length : 0;
            int rotationsCount = handState.BoneRotations != null ? handState.BoneRotations.Length : 0;

            for (int i = 0; i < positionsCount; i++)
            {
                Vector3f bonePositions = handState.BonePositions[i];
                SetIfValid(row, cols.BonePosX[i], bonePositions.x);
                SetIfValid(row, cols.BonePosY[i], bonePositions.y);
                SetIfValid(row, cols.BonePosZ[i], bonePositions.z);

            }

            for (int i = 0; i < rotationsCount; i++)
            {
                Quatf boneRotations = handState.BoneRotations[i];
                SetIfValid(row, cols.BoneQx[i], boneRotations.x);
                SetIfValid(row, cols.BoneQy[i], boneRotations.y);
                SetIfValid(row, cols.BoneQz[i], boneRotations.z);
                SetIfValid(row, cols.BoneQw[i], boneRotations.w);
            }
        }
    }
}
