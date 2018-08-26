using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TunnelBore
{
    public class TunnelBore : MachineEntity, PowerConsumerInterface
    {
        private Vector3 Forward;
        private Vector3 Up;
        private Vector3 Right;
        private int BoreDistance = 1;
        private int SleepCount = 0;
        private BoreState State;
        private string FlavorText;

        private float CurrentPower;

        public static float PowerPerBlock { get; set; } = 10;
        public static int MaxBoreDistance = 256;

        public TunnelBore(long x, long y, long z, ushort cube, byte flags, ushort value, Vector3 position,
            Segment segment) : base(eSegmentEntity.Mod, SpawnableObjectEnum.BFL9000, x, y, z, cube, flags,
            value, position, segment)
        {
            mbNeedsLowFrequencyUpdate = true;
            this.mFlags = flags;
            ResetRotation();
            State = BoreState.Initializing;
            FlavorText = _flavorTexts[Random.Range(0, _flavorTexts.Length)];
        }

        public void ResetRotation()
        {
            var rotationQuat = SegmentCustomRenderer.GetRotationQuaternion(mFlags);

            Forward = rotationQuat * Vector3.forward;
            Right = rotationQuat * Vector3.right;
            Up = rotationQuat * Vector3.up;

            // Normalize vectors so we can easily multiply
            Forward.Normalize();
            Right.Normalize();
            Up.Normalize();

            BoreDistance = 1;
        }

        public override void OnUpdateRotation(byte newFlags)
        {
            base.OnUpdateRotation(newFlags);
            this.mFlags = newFlags;
            ResetRotation();
        }

        public void WithSegments(Action<HashSet<Segment>> action)
        {
            var touchedSegments = new HashSet<Segment>();
            try
            {
                action(touchedSegments);
            }
            finally
            {
                foreach (var seg in touchedSegments)
                {
                    seg.EndProcessing();
                }
            }
        }

        public override bool ShouldNetworkUpdate()
        {
            return true;
        }

        public override void ReadNetworkUpdate(BinaryReader reader)
        {
            State = (BoreState) reader.ReadInt32();
            BoreDistance = reader.ReadInt32();
            MaxBoreDistance = reader.ReadInt32();
        }

        public override void WriteNetworkUpdate(BinaryWriter writer)
        {
            writer.Write((int)State);
            writer.Write(BoreDistance);
            writer.Write(MaxBoreDistance);
        }

        public void BoreTunnelV(HashSet<Segment> touchedSegments)
        {
            for (var layer = 0; layer < 10; layer++)
            {
                for (var rightOffset = -2 - layer; rightOffset <= 2 + layer; rightOffset++)
                {
                    var posX = (long) Forward.x * BoreDistance;
                    var posY = (long) Forward.y * BoreDistance;
                    var posZ = (long) Forward.z * BoreDistance;

                    // Adjust for "up" layers
                    posX += (long) ((double) layer * Up.x);
                    posY += (long) ((double) layer * Up.y);
                    posZ += (long) ((double) layer * Up.z);

                    // Adjust for left/right cut width
                    posX += (long) ((double) rightOffset * Right.x);
                    posY += (long) ((double) rightOffset * Right.y);
                    posZ += (long) ((double) rightOffset * Right.z);

                    // Now add our current position to get the target offset
                    posX += mnX;
                    posY += mnY;
                    posZ += mnZ;

                    // Now let's try to get the segment
                    Segment segment;
                    if (this.mFrustrum != null)
                    {
                        segment = AttemptGetSegment(posX, posY, posZ);
                        if (segment == null)
                        {
                            return;
                        }
                    }
                    else
                    {
                        segment = WorldScript.instance.GetSegment(posX, posY, posZ);
                        if (segment == null || !segment.mbInitialGenerationComplete || segment.mbDestroyed)
                        {
                            return;
                        }
                    }

                    // Diggy Diggy Hole
                    var cube = segment.GetCube(posX, posY, posZ);
                    if (!CubeHelper.IsGarbage(cube))
                        continue;

                    if (CurrentPower - PowerPerBlock < 0)
                    {
                        return;
                    }

                    if (!touchedSegments.Contains(segment))
                    {
                        segment.BeginProcessing();
                        touchedSegments.Add(segment);
                    }


                    CurrentPower -= PowerPerBlock;
                    WorldScript.instance.BuildFromEntity(segment, posX, posY, posZ, eCubeTypes.Air, 0);
                }
            }

            BoreDistance++;
        }

        public void BoreTunnelSquare(HashSet<Segment> touchedSegments)
        {
            for (var layer = 0; layer < 3; layer++)
            {
                for (var rightOffset = -1; rightOffset <= 1; rightOffset++)
                {
                    var posX = (long) Forward.x * BoreDistance;
                    var posY = (long) Forward.y * BoreDistance;
                    var posZ = (long) Forward.z * BoreDistance;

                    // Adjust for "up" layers
                    posX += (long) ((double) layer * Up.x);
                    posY += (long) ((double) layer * Up.y);
                    posZ += (long) ((double) layer * Up.z);

                    // Adjust for left/right cut width
                    posX += (long) ((double) rightOffset * Right.x);
                    posY += (long) ((double) rightOffset * Right.y);
                    posZ += (long) ((double) rightOffset * Right.z);

                    // Now add our current position to get the target offset
                    posX += mnX;
                    posY += mnY;
                    posZ += mnZ;

                    // Now let's try to get the segment
                    Segment segment;
                    if (this.mFrustrum != null)
                    {
                        segment = AttemptGetSegment(posX, posY, posZ);
                        if (segment == null)
                        {
                            return;
                        }
                    }
                    else
                    {
                        segment = WorldScript.instance.GetSegment(posX, posY, posZ);
                        if (segment == null || !segment.mbInitialGenerationComplete || segment.mbDestroyed)
                        {
                            return;
                        }
                    }

                    // Diggy Diggy Hole
                    var cube = segment.GetCube(posX, posY, posZ);
                    if (!CubeHelper.IsGarbage(cube))
                        continue;
                    if (CurrentPower - PowerPerBlock < 0)
                    {
                        return;
                    }

                    if (!touchedSegments.Contains(segment))
                    {
                        segment.BeginProcessing();
                        touchedSegments.Add(segment);
                    }

                    CurrentPower -= PowerPerBlock;
                    WorldScript.instance.BuildFromEntity(segment, posX, posY, posZ, eCubeTypes.Air, 0);
                }
            }

            BoreDistance++;
        }

        public override void LowFrequencyUpdate()
        {
            // If we're a network client, we don't need to process anything
            if (!WorldScript.mbIsServer)
                return;

            base.LowFrequencyUpdate();

            if (State == BoreState.Initializing)
                State = BoreState.LowPower;

            if (BoreDistance > MaxBoreDistance)
            {
                State = BoreState.Finished;
                return;
            }

            State = CurrentPower > PowerPerBlock ? BoreState.Boring : BoreState.LowPower;
            if (State == BoreState.LowPower)
                return;

            // We're going to sleep this many ticks between dig attempts
            SleepCount++;
            if (SleepCount < 4)
                return;
            SleepCount = 0;

            switch (mValue)
            {
                case 0:
                    WithSegments(BoreTunnelV);
                    break;
                case 1:
                    WithSegments(BoreTunnelSquare);
                    break;
            }
        }

        public float GetMaxPower()
        {
            return 5000f;
        }

        public float GetRemainingPowerCapacity()
        {
            return GetMaxPower() - CurrentPower;
        }

        public float GetMaximumDeliveryRate()
        {
            return 5000f;
        }

        public bool DeliverPower(float amount)
        {
            if (CurrentPower + amount > GetMaxPower())
            {
                return false;
            }

            CurrentPower += amount;
            return true;
        }

        public bool WantsPowerFromEntity(SegmentEntity entity)
        {
            return CurrentPower / GetMaxPower() < 0.99f;
        }

        public string Name()
        {
            switch (mValue)
            {
                case 0:
                    return "Tunnel Bore (Stair Case)";
                case 1:
                    return "Tunnel Bore (3x3 Square)";
            }

            return "Unknown";
        }

        public override string GetPopupText()
        {
            var builder = new StringBuilder();

            // Name of block
            builder.AppendLine(Name());

            // Current Power
            builder.AppendLine($"Power: {(int) CurrentPower}/{(int) GetMaxPower()}");

            // Current State
            builder.AppendLine($"State: {State}");

            // Current Distance
            if (State == BoreState.Boring)
            {
                builder.AppendLine($"Current Boring Distance: {BoreDistance}");
            }

            // Because lulz
            builder.AppendLine("");
            builder.AppendLine(FlavorText);
            return builder.ToString();
        }

        private static string[] _flavorTexts =
        {
            "It's all just so incredibly boring.",
            "Are you bored yet?",
            "Un-bore-lievable.",
            "These puns are un-bore-able.",
            "If you are bored, put on a cape, then you can be Super Bored!",
            "Boring, no matter the direction.",
        };

        public enum BoreState
        {
            Initializing,
            LowPower,
            Boring,
            Finished,
        }
    }
}