using System;
using System.Collections.Generic;
using Vintagestory.Common;
using Vintagestory.Client.NoObf;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using ProtoBuf;

using HarmonyLib;

using Cairo;

namespace Archery
{
    public enum ArcheryRangedWeaponType
    {
        Bow,
        Throw
    }

    public class ArcheryRangedWeaponStats
    {
        // General
        public ArcheryRangedWeaponType weaponType = ArcheryRangedWeaponType.Bow;

        // ItemBow stats
        public float cooldownTime = 0.75f;  
        public float chargeTime = 0.5f;
        public float projectileVelocity = 30f; // Vanilla arrow speed
        public float projectileSpread = 1f; // In degrees

        // Harmony client patch stats
        public float horizontalLimit = 0.125f;
        public float verticalLimit = 0.35f;
        public float verticalOffset = -0.15f;

        public float driftFrequency = 0.001f;
        public float driftMagnitude = 150f;
        public float driftMax = 150f;

        public long twitchDuration = 300;
        public float twitchMagnitude = 40f;
        public float twitchMax = 5f;

        // AimAccuracy EntityBehaviour stats
        public float accuracyStartTime = 1f;

        public float accuracyOvertimeStart = 6f;
        public float accuracyOvertimeTime = 12f;
        public float accuracyOvertime = 1f;

        public float accuracyMovePenalty = 1f;
    }
}