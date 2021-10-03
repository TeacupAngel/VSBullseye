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

namespace Bullseye
{
    /*[HarmonyPatch(typeof(SystemRenderPlayerAimAcc))]
    class SystemRenderPlayerAimAccPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("OnRenderFrame2DOverlay")]
        static bool OnRenderFrame2DOverlayPrefix(SystemRenderPlayerAimAcc __instance)
        {
            return false;
        }
    }*/

    [HarmonyPatch(typeof(SystemRenderAim))]
    class SystemRenderAimPatch
    {
        public static BullseyeCoreClientSystem coreClientSystem;

        private static BullseyeRangedWeaponStats weaponStats = new BullseyeRangedWeaponStats();

        private static int defaultAimTexPartChargeId;
        private static int defaultAimTexFullChargeId;
        private static int defaultAimTexBlockedId;

        private static int currentAimTexPartChargeId;
        private static int currentAimTexFullChargeId;
        private static int currentAimTexBlockedId;

        private static int aimTextureThrowCircleId;

        public enum ReadinessState
        {
            Blocked,
            PartCharge,
            FullCharge
        }

        public static ReadinessState readinessState = ReadinessState.Blocked;

        [HarmonyPrefix]
        [HarmonyPatch("OnBlockTexturesLoaded")]
        static bool OnBlockTexturesLoadedPrefix(ClientMain ___game)
        {
            defaultAimTexPartChargeId = (___game.Api as ICoreClientAPI).Render.GetOrLoadTexture(new AssetLocation("bullseye", "gui/aimdefaultpart.png"));
            defaultAimTexFullChargeId = (___game.Api as ICoreClientAPI).Render.GetOrLoadTexture(new AssetLocation("bullseye", "gui/aimdefaultfull.png"));
            defaultAimTexBlockedId = (___game.Api as ICoreClientAPI).Render.GetOrLoadTexture(new AssetLocation("bullseye", "gui/aimblockeddefault.png"));

            aimTextureThrowCircleId = (___game.Api as ICoreClientAPI).Render.GetOrLoadTexture(new AssetLocation("bullseye", "gui/throw_circle.png"));
            
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("DrawAim")]
        static bool DrawAimPrefix(int ___aimTextureId, ClientMain ___game)
        {
            if (coreClientSystem == null)
            {
                return true;
            }

            if (coreClientSystem.aiming)
            {
                Vec2f currentAim = coreClientSystem.GetCurrentAim();

                int textureId = readinessState == ReadinessState.FullCharge ? currentAimTexFullChargeId : 
                                (readinessState == ReadinessState.PartCharge ? currentAimTexPartChargeId : currentAimTexBlockedId);
                
                ___game.Render2DTexture(textureId, ___game.Width / 2 - 16 + currentAim.X, ___game.Height / 2 - 16 + currentAim.Y, 32, 32, 10000f);

                if (weaponStats.weaponType == BullseyeRangedWeaponType.Throw)
                {
                    ___game.Render2DTexture(aimTextureThrowCircleId, ___game.Width / 2 - 160, ___game.Height / 2 - 160, 320, 320, 10001f);
                }

                return false;
            }
            
            return true;
        }

        public static void SetRangedWeaponStats(BullseyeRangedWeaponStats weaponStats)
        {
            SystemRenderAimPatch.weaponStats = weaponStats;
        }

        public static void SetReticleTextures(int partChargeTexId, int fullChargeTexId, int blockedTexId)
        {
            currentAimTexPartChargeId = partChargeTexId >= 0 ? partChargeTexId : defaultAimTexPartChargeId;
            currentAimTexFullChargeId = fullChargeTexId >= 0 ? fullChargeTexId : defaultAimTexFullChargeId;
            currentAimTexBlockedId = blockedTexId >= 0 ? blockedTexId : defaultAimTexBlockedId;
        }

        public static void SetShootReadinessState(ReadinessState state)
        {
            readinessState = state;
        }
    }

    [HarmonyPatch(typeof(ClientMain))]
    class ClientMainPatch
    {
        public static BullseyeCoreClientSystem coreClientSystem;

        private static BullseyeRangedWeaponStats weaponStats = new BullseyeRangedWeaponStats();

        public static float driftMultiplier = 1f;
        public static float twitchMultiplier = 1f;

        // ---

        private static NormalizedSimplexNoise noisegen = NormalizedSimplexNoise.FromDefaultOctaves(4, 1.0, 0.9, 123L);

        static long twitchLastChangeMilliseconds;
        static long twitchLastStepMilliseconds;

        public static float twitchX;
        public static float twitchY;
        public static float twitchLength;

        static Random random;

        [HarmonyPrefix]
        [HarmonyPatch("UpdateCameraYawPitch")]
        static bool UpdateCameraYawPitchPrefix(ClientMain __instance, 
            ref double ___MouseDeltaX, ref double ___MouseDeltaY, 
            ref double ___DelayedMouseDeltaX, ref double ___DelayedMouseDeltaY,
            ClientPlatformAbstract ___Platform,
            float dt)
        {
            if (coreClientSystem == null)
            {
                return true;
            }

            if (coreClientSystem.aiming)
            {
                // = Aiming system #3 - simpler, Receiver-inspired =
                float fovRatio = __instance.Width / 1920f;

                coreClientSystem.aimOffsetX += (((float)noisegen.Noise(__instance.ElapsedMilliseconds * weaponStats.driftFrequency, 1000f) - 0.5f) - coreClientSystem.aimOffsetX / (weaponStats.driftMax * driftMultiplier)) * weaponStats.driftMagnitude * driftMultiplier * dt * fovRatio;
                coreClientSystem.aimOffsetY += (((float)noisegen.Noise(-1000f, __instance.ElapsedMilliseconds * weaponStats.driftFrequency) - 0.5f) - coreClientSystem.aimOffsetY / (weaponStats.driftMax * driftMultiplier)) * weaponStats.driftMagnitude * driftMultiplier * dt * fovRatio;

                if (__instance.Api.World.ElapsedMilliseconds > twitchLastChangeMilliseconds + weaponStats.twitchDuration)
                {
                    twitchLastChangeMilliseconds = __instance.Api.World.ElapsedMilliseconds;
                    twitchLastStepMilliseconds = __instance.Api.World.ElapsedMilliseconds;

                    if (random == null)
                    {
                        random = new Random((int)(__instance.EntityPlayer.EntityId + __instance.Api.World.ElapsedMilliseconds));
                    }

                    twitchX = (((float)random.NextDouble() - 0.5f) * 2f) * (weaponStats.twitchMax * twitchMultiplier) - coreClientSystem.aimOffsetX / (weaponStats.twitchMax * twitchMultiplier);
                    twitchY = (((float)random.NextDouble() - 0.5f) * 2f) * (weaponStats.twitchMax * twitchMultiplier) - coreClientSystem.aimOffsetY / (weaponStats.twitchMax * twitchMultiplier);

                    twitchLength = GameMath.Sqrt(twitchX * twitchX + twitchY * twitchY);

                    twitchX = twitchX / twitchLength;
                    twitchY = twitchY / twitchLength;
                }

                float lastStep = (twitchLastStepMilliseconds - twitchLastChangeMilliseconds) / (float)weaponStats.twitchDuration;
                float currentStep = (__instance.Api.World.ElapsedMilliseconds - twitchLastChangeMilliseconds) / (float)weaponStats.twitchDuration;

                float stepSize = ((1f - lastStep) * (1f - lastStep)) - ((1f - currentStep) * (1f - currentStep));

                //BullseyeCore.aimOffsetX += twitchX * stepSize * twitchMagnitude * dt;
                //BullseyeCore.aimOffsetY += twitchY * stepSize * twitchMagnitude * dt;

                coreClientSystem.aimOffsetX += twitchX * stepSize * (weaponStats.twitchMagnitude * twitchMultiplier * dt) * (weaponStats.twitchDuration / 20) * fovRatio;
                coreClientSystem.aimOffsetY += twitchY * stepSize * (weaponStats.twitchMagnitude * twitchMultiplier * dt) * (weaponStats.twitchDuration / 20) * fovRatio;

                twitchLastStepMilliseconds = __instance.Api.World.ElapsedMilliseconds;

                // Aiming itself
                float horizontalAimLimit = __instance.Width / 2f * weaponStats.horizontalLimit;
                float verticalAimLimit = __instance.Height / 2f * weaponStats.verticalLimit;
                float verticalAimOffset = __instance.Height / 2f * weaponStats.verticalOffset;

                float deltaX = (float)(___MouseDeltaX - ___DelayedMouseDeltaX);
                float deltaY = (float)(___MouseDeltaY - ___DelayedMouseDeltaY);

                if (Math.Abs(coreClientSystem.aimX + deltaX) > horizontalAimLimit)
                {
                    coreClientSystem.aimX = coreClientSystem.aimX > 0 ? horizontalAimLimit : -horizontalAimLimit;
                }
                else
                {
                    coreClientSystem.aimX += deltaX;
                    ___DelayedMouseDeltaX = ___MouseDeltaX;
                }

                if (Math.Abs(coreClientSystem.aimY + deltaY - verticalAimOffset) > verticalAimLimit)
                {
                    coreClientSystem.aimY = (coreClientSystem.aimY > 0 ? verticalAimLimit : -verticalAimLimit) + verticalAimOffset;
                }
                else
                {
                    coreClientSystem.aimY += deltaY;
                    ___DelayedMouseDeltaY = ___MouseDeltaY;
                }

                coreClientSystem.SetAim();
            }
            
            return true;
        }

        public static void SetRangedWeaponStats(BullseyeRangedWeaponStats weaponStats)
        {
            ClientMainPatch.weaponStats = weaponStats;
        }
    }
}