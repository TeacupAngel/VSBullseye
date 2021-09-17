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
        private static BullseyeRangedWeaponStats weaponStats = new BullseyeRangedWeaponStats();

        private static int defaultAimTextureId;
        //private static int aimTextureYellowId;
        private static int defaultAimTextureBlockedId;

        private static int currentAimTextureId;
        private static int currentAimTextureBlockedId;

        private static int aimTextureThrowCircleId;

        public static bool readyToShoot = false;

        [HarmonyPrefix]
        [HarmonyPatch("OnBlockTexturesLoaded")]
        static bool OnBlockTexturesLoadedPrefix(ClientMain ___game)
        {
            defaultAimTextureId = (___game.Api as ICoreClientAPI).Render.GetOrLoadTexture(new AssetLocation("bullseye", "gui/aimdefault.png"));
            defaultAimTextureBlockedId = (___game.Api as ICoreClientAPI).Render.GetOrLoadTexture(new AssetLocation("bullseye", "gui/aimblockeddefault.png"));

            aimTextureThrowCircleId = (___game.Api as ICoreClientAPI).Render.GetOrLoadTexture(new AssetLocation("bullseye", "gui/throw_circle.png"));
            
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("DrawAim")]
        static bool DrawAimPrefix(int ___aimTextureId, ClientMain ___game)
        {
            if (BullseyeCore.aiming)
            {
                //___game.Render2DTexture(aimRangedTextureYellowId, ___game.Width / 2 - 16 + FreeAimCore.aimX, ___game.Height / 2 - 16 + FreeAimCore.aimY, 32, 32, 10000f);
                int textureId = readyToShoot ? currentAimTextureId : currentAimTextureBlockedId;
                
                ___game.Render2DTexture(textureId, ___game.Width / 2 - 16 + BullseyeCore.aimX + BullseyeCore.aimOffsetX, ___game.Height / 2 - 16 + BullseyeCore.aimY + BullseyeCore.aimOffsetY, 32, 32, 10000f);

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

        public static void SetReticleTextures(int textureId, int blockedTextureId)
        {
            currentAimTextureId = textureId >= 0 ? textureId : defaultAimTextureId;
            currentAimTextureBlockedId = blockedTextureId >= 0 ? blockedTextureId : defaultAimTextureBlockedId;
        }
    }

    [HarmonyPatch(typeof(ClientMain))]
    class ClientMainPatch
    {
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
            if (BullseyeCore.aiming)
            {
                // = Aiming system #3 - simpler, Receiver-inspired =
                // Aim drift
                //BullseyeCore.aimOffsetX += ((float)noisegen.Noise(__instance.ElapsedMilliseconds * driftFrequency, 1000f) - 0.5f) * driftMagnitude * dt;
                //BullseyeCore.aimOffsetY += ((float)noisegen.Noise(-1000f, __instance.ElapsedMilliseconds * driftFrequency) - 0.5f) * driftMagnitude * dt;

                float fovRatio = __instance.Width / 1920f;

                BullseyeCore.aimOffsetX += (((float)noisegen.Noise(__instance.ElapsedMilliseconds * weaponStats.driftFrequency, 1000f) - 0.5f) - BullseyeCore.aimOffsetX / (weaponStats.driftMax * driftMultiplier)) * weaponStats.driftMagnitude * driftMultiplier * dt * fovRatio;
                BullseyeCore.aimOffsetY += (((float)noisegen.Noise(-1000f, __instance.ElapsedMilliseconds * weaponStats.driftFrequency) - 0.5f) - BullseyeCore.aimOffsetY / (weaponStats.driftMax * driftMultiplier)) * weaponStats.driftMagnitude * driftMultiplier * dt * fovRatio;

                if (__instance.Api.World.ElapsedMilliseconds > twitchLastChangeMilliseconds + weaponStats.twitchDuration)
                {
                    twitchLastChangeMilliseconds = __instance.Api.World.ElapsedMilliseconds;
                    twitchLastStepMilliseconds = __instance.Api.World.ElapsedMilliseconds;

                    if (random == null)
                    {
                        random = new Random((int)(__instance.EntityPlayer.EntityId + __instance.Api.World.ElapsedMilliseconds));
                    }

                    twitchX = (((float)random.NextDouble() - 0.5f) * 2f) * (weaponStats.twitchMax * twitchMultiplier) - BullseyeCore.aimOffsetX / (weaponStats.twitchMax * twitchMultiplier);
                    twitchY = (((float)random.NextDouble() - 0.5f) * 2f) * (weaponStats.twitchMax * twitchMultiplier) - BullseyeCore.aimOffsetY / (weaponStats.twitchMax * twitchMultiplier);

                    twitchLength = GameMath.Sqrt(twitchX * twitchX + twitchY * twitchY);

                    twitchX = twitchX / twitchLength;
                    twitchY = twitchY / twitchLength;
                }

                float lastStep = (twitchLastStepMilliseconds - twitchLastChangeMilliseconds) / (float)weaponStats.twitchDuration;
                float currentStep = (__instance.Api.World.ElapsedMilliseconds - twitchLastChangeMilliseconds) / (float)weaponStats.twitchDuration;

                float stepSize = ((1f - lastStep) * (1f - lastStep)) - ((1f - currentStep) * (1f - currentStep));

                //BullseyeCore.aimOffsetX += twitchX * stepSize * twitchMagnitude * dt;
                //BullseyeCore.aimOffsetY += twitchY * stepSize * twitchMagnitude * dt;

                BullseyeCore.aimOffsetX += twitchX * stepSize * (weaponStats.twitchMagnitude * twitchMultiplier * dt) * (weaponStats.twitchDuration / 20) * fovRatio;
                BullseyeCore.aimOffsetY += twitchY * stepSize * (weaponStats.twitchMagnitude * twitchMultiplier * dt) * (weaponStats.twitchDuration / 20) * fovRatio;

                twitchLastStepMilliseconds = __instance.Api.World.ElapsedMilliseconds;

                // Aiming itself
                float horizontalAimLimit = __instance.Width / 2f * weaponStats.horizontalLimit;
                float verticalAimLimit = __instance.Height / 2f * weaponStats.verticalLimit;
                float verticalAimOffset = __instance.Height / 2f * weaponStats.verticalOffset;

                float deltaX = (float)(___MouseDeltaX - ___DelayedMouseDeltaX);
                float deltaY = (float)(___MouseDeltaY - ___DelayedMouseDeltaY);

                if (Math.Abs(BullseyeCore.aimX + deltaX) > horizontalAimLimit)
                {
                    BullseyeCore.aimX = BullseyeCore.aimX > 0 ? horizontalAimLimit : -horizontalAimLimit;
                }
                else
                {
                    BullseyeCore.aimX += deltaX;
                    ___DelayedMouseDeltaX = ___MouseDeltaX;
                }

                if (Math.Abs(BullseyeCore.aimY + deltaY - verticalAimOffset) > verticalAimLimit)
                {
                    BullseyeCore.aimY = (BullseyeCore.aimY > 0 ? verticalAimLimit : -verticalAimLimit) + verticalAimOffset;
                }
                else
                {
                    BullseyeCore.aimY += deltaY;
                    ___DelayedMouseDeltaY = ___MouseDeltaY;
                }

                BullseyeCore.clientInstance.SetAim();
            }
            
            return true;
        }

        public static void SetRangedWeaponStats(BullseyeRangedWeaponStats weaponStats)
        {
            ClientMainPatch.weaponStats = weaponStats;
        }
    }
}