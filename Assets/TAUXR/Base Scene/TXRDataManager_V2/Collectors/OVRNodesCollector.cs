// OVRNodesCollector.cs
// Collects legacy head pose (Euler) and device node pose/vel/validity blocks.

using System;
using System.Collections.Generic;
using UnityEngine;
using static OVRPlugin; // Node, Step, Posef, Vector3f, API calls
using static TXRData.CollectorUtils;

namespace TXRData
{
    public sealed class OVRNodesCollector : IContinuousCollector
    {
        public string CollectorName => "OVRNodesCollector";
        private const Step SampleStep = OvrSampling.StepDefault;

        // Legacy head pose block
        private int _idxHeadPosX = -1, _idxHeadHeight = -1, _idxHeadPosZ = -1;
        private int _idxGazePitch = -1, _idxGazeYaw = -1, _idxGazeRoll = -1;
        private int _idxHeadOrientValid = -1, _idxHeadPosValid = -1;
        private int _idxHeadOrientTracked = -1, _idxHeadPosTracked = -1;
        private int _idxHeadNodeTime = -1;

        private struct NodeCols
        {
            public int Present;
            public int PosX, PosY, PosZ;
            public int Qx, Qy, Qz, Qw;
            public int VelX, VelY, VelZ;
            public int AngVelX, AngVelY, AngVelZ;
            public int ValidPos, ValidOrient, TrackedPos, TrackedOrient;
            public int Time;
        }

        private readonly Dictionary<Node, NodeCols> _nodeCols = new Dictionary<Node, NodeCols>();
        private static readonly Node[] NodeOrder =
        {
            Node.EyeLeft, Node.EyeRight, Node.EyeCenter, Node.Head,
            Node.HandLeft, Node.HandRight, Node.ControllerLeft, Node.ControllerRight
        };

        public void Configure(ColumnIndex schema, RecordingOptions options)
        {
            if (schema == null) throw new ArgumentNullException(nameof(schema));

            TryIndex(schema, "Head_Position_x", out _idxHeadPosX);
            TryIndex(schema, "Head_Height", out _idxHeadHeight);
            TryIndex(schema, "Head_Position_z", out _idxHeadPosZ);

            TryIndex(schema, "Gaze_Pitch", out _idxGazePitch);
            TryIndex(schema, "Gaze_Yaw", out _idxGazeYaw);
            TryIndex(schema, "Gaze_Roll", out _idxGazeRoll);

            TryIndex(schema, "HeadNodeOrientationValid", out _idxHeadOrientValid);
            TryIndex(schema, "HeadNodePositionValid", out _idxHeadPosValid);
            TryIndex(schema, "HeadNodeOrientationTracked", out _idxHeadOrientTracked);
            TryIndex(schema, "HeadNodePositionTracked", out _idxHeadPosTracked);
            TryIndex(schema, "HeadNodeTime", out _idxHeadNodeTime);

            _nodeCols.Clear();
            foreach (Node node in NodeOrder)
            {
                string baseName = $"Node_{node}";
                NodeCols cols = new NodeCols
                {
                    Present = IndexOrMinusOne(schema, $"{baseName}_Present"),

                    PosX = IndexOrMinusOne(schema, $"{baseName}_px"),
                    PosY = IndexOrMinusOne(schema, $"{baseName}_py"),
                    PosZ = IndexOrMinusOne(schema, $"{baseName}_pz"),

                    Qx = IndexOrMinusOne(schema, $"{baseName}_qx"),
                    Qy = IndexOrMinusOne(schema, $"{baseName}_qy"),
                    Qz = IndexOrMinusOne(schema, $"{baseName}_qz"),
                    Qw = IndexOrMinusOne(schema, $"{baseName}_qw"),

                    VelX = IndexOrMinusOne(schema, $"{baseName}_Vel_x"),
                    VelY = IndexOrMinusOne(schema, $"{baseName}_Vel_y"),
                    VelZ = IndexOrMinusOne(schema, $"{baseName}_Vel_z"),

                    AngVelX = IndexOrMinusOne(schema, $"{baseName}_AngVel_x"),
                    AngVelY = IndexOrMinusOne(schema, $"{baseName}_AngVel_y"),
                    AngVelZ = IndexOrMinusOne(schema, $"{baseName}_AngVel_z"),

                    ValidPos = IndexOrMinusOne(schema, $"{baseName}_Valid_Position"),
                    ValidOrient = IndexOrMinusOne(schema, $"{baseName}_Valid_Orientation"),
                    TrackedPos = IndexOrMinusOne(schema, $"{baseName}_Tracked_Position"),
                    TrackedOrient = IndexOrMinusOne(schema, $"{baseName}_Tracked_Orientation"),

                    Time = IndexOrMinusOne(schema, $"{baseName}_Time"),
                };
                _nodeCols[node] = cols;
            }
        }

        public void Collect(RowBuffer row, float timeSinceStartup)
        {
            // ----- Legacy head pose (Euler from quaternion) -----
            Posef headPose = GetNodePose(Node.Head, SampleStep); // Posef (no time) :contentReference[oaicite:4]{index=4}
            SetIfValid(row, _idxHeadPosX, headPose.Position.x);
            SetIfValid(row, _idxHeadHeight, headPose.Position.y);
            SetIfValid(row, _idxHeadPosZ, headPose.Position.z);

            Quaternion qHead = new Quaternion(headPose.Orientation.x, headPose.Orientation.y, headPose.Orientation.z, headPose.Orientation.w);
            Vector3 euler = qHead.eulerAngles;
            SetIfValid(row, _idxGazePitch, euler.x);
            SetIfValid(row, _idxGazeYaw, euler.y);
            SetIfValid(row, _idxGazeRoll, euler.z);

            bool headPosValid = GetNodePositionValid(Node.Head);
            bool headOrientValid = GetNodeOrientationValid(Node.Head);
            bool headPosTracked = GetNodePositionTracked(Node.Head);
            bool headOrientTracked = GetNodeOrientationTracked(Node.Head);
            SetIfValid(row, _idxHeadPosValid, headPosValid ? 1 : 0);
            SetIfValid(row, _idxHeadOrientValid, headOrientValid ? 1 : 0);
            SetIfValid(row, _idxHeadPosTracked, headPosTracked ? 1 : 0);
            SetIfValid(row, _idxHeadOrientTracked, headOrientTracked ? 1 : 0);

            // Per-node precise timestamp (PoseStatef.Time) via GetNodePoseStateRaw
            PoseStatef headState = GetNodePoseStateRaw(Node.Head, SampleStep); // has .Time 
            SetIfValid(row, _idxHeadNodeTime, headState.Time);

            // ----- Per-node block -----
            foreach (KeyValuePair<Node, NodeCols> pair in _nodeCols)
            {
                Node node = pair.Key;
                NodeCols cols = pair.Value;

                bool present = GetNodePresent(node);
                SetIfValid(row, cols.Present, present ? 1 : 0);

                Posef pose = GetNodePose(node, SampleStep);
                SetIfValid(row, cols.PosX, pose.Position.x);
                SetIfValid(row, cols.PosY, pose.Position.y);
                SetIfValid(row, cols.PosZ, pose.Position.z);
                SetIfValid(row, cols.Qx, pose.Orientation.x);
                SetIfValid(row, cols.Qy, pose.Orientation.y);
                SetIfValid(row, cols.Qz, pose.Orientation.z);
                SetIfValid(row, cols.Qw, pose.Orientation.w);

                Vector3f vel = GetNodeVelocity(node, SampleStep);
                SetIfValid(row, cols.VelX, vel.x);
                SetIfValid(row, cols.VelY, vel.y);
                SetIfValid(row, cols.VelZ, vel.z);

                Vector3f angVel = GetNodeAngularVelocity(node, SampleStep);
                SetIfValid(row, cols.AngVelX, angVel.x);
                SetIfValid(row, cols.AngVelY, angVel.y);
                SetIfValid(row, cols.AngVelZ, angVel.z);

                bool validPos = GetNodePositionValid(node);
                bool validOrient = GetNodeOrientationValid(node);
                bool trackedPos = GetNodePositionTracked(node);
                bool trackedOrient = GetNodeOrientationTracked(node);
                SetIfValid(row, cols.ValidPos, validPos ? 1 : 0);
                SetIfValid(row, cols.ValidOrient, validOrient ? 1 : 0);
                SetIfValid(row, cols.TrackedPos, trackedPos ? 1 : 0);
                SetIfValid(row, cols.TrackedOrient, trackedOrient ? 1 : 0);

                // Per-node time, if that column exists
                if (cols.Time >= 0)
                {
                    PoseStatef state = GetNodePoseStateRaw(node, SampleStep);
                    row.Set(cols.Time, state.Time);
                }
            }
        }

        public void Dispose()
        {
            _nodeCols.Clear();
        }
    }
}
