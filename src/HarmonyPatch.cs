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
        private static ArcheryRangedWeaponStats weaponStats = new ArcheryRangedWeaponStats();

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
            defaultAimTextureId = (___game.Api as ICoreClientAPI).Render.GetOrLoadTexture(new AssetLocation("archery", "gui/aimdefault.png"));
            defaultAimTextureBlockedId = (___game.Api as ICoreClientAPI).Render.GetOrLoadTexture(new AssetLocation("archery", "gui/aimblockeddefault.png"));

            aimTextureThrowCircleId = (___game.Api as ICoreClientAPI).Render.GetOrLoadTexture(new AssetLocation("archery", "gui/throw_circle.png"));
            
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("DrawAim")]
        static bool DrawAimPrefix(int ___aimTextureId, ClientMain ___game)
        {
            if (ArcheryCore.aiming)
            {
                //___game.Render2DTexture(aimRangedTextureYellowId, ___game.Width / 2 - 16 + FreeAimCore.aimX, ___game.Height / 2 - 16 + FreeAimCore.aimY, 32, 32, 10000f);
                int textureId = readyToShoot ? currentAimTextureId : currentAimTextureBlockedId;
                
                ___game.Render2DTexture(textureId, ___game.Width / 2 - 16 + ArcheryCore.aimX + ArcheryCore.aimOffsetX, ___game.Height / 2 - 16 + ArcheryCore.aimY + ArcheryCore.aimOffsetY, 32, 32, 10000f);

                if (weaponStats.weaponType == ArcheryRangedWeaponType.Throw)
                {
                    ___game.Render2DTexture(aimTextureThrowCircleId, ___game.Width / 2 - 160, ___game.Height / 2 - 160, 320, 320, 10001f);
                }

                return false;
            }
            
            return true;
        }

        public static void SetRangedWeaponStats(ArcheryRangedWeaponStats weaponStats)
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
        private static ArcheryRangedWeaponStats weaponStats = new ArcheryRangedWeaponStats();

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
            if (ArcheryCore.aiming)
            {
                // = Aiming system #3 - simpler, Receiver-inspired =
                // Aim drift
                //ArcheryCore.aimOffsetX += ((float)noisegen.Noise(__instance.ElapsedMilliseconds * driftFrequency, 1000f) - 0.5f) * driftMagnitude * dt;
                //ArcheryCore.aimOffsetY += ((float)noisegen.Noise(-1000f, __instance.ElapsedMilliseconds * driftFrequency) - 0.5f) * driftMagnitude * dt;

                float fovRatio = __instance.Width / 1920f;

                ArcheryCore.aimOffsetX += (((float)noisegen.Noise(__instance.ElapsedMilliseconds * weaponStats.driftFrequency, 1000f) - 0.5f) - ArcheryCore.aimOffsetX / (weaponStats.driftMax * driftMultiplier)) * weaponStats.driftMagnitude * driftMultiplier * dt * fovRatio;
                ArcheryCore.aimOffsetY += (((float)noisegen.Noise(-1000f, __instance.ElapsedMilliseconds * weaponStats.driftFrequency) - 0.5f) - ArcheryCore.aimOffsetY / (weaponStats.driftMax * driftMultiplier)) * weaponStats.driftMagnitude * driftMultiplier * dt * fovRatio;

                if (__instance.Api.World.ElapsedMilliseconds > twitchLastChangeMilliseconds + weaponStats.twitchDuration)
                {
                    twitchLastChangeMilliseconds = __instance.Api.World.ElapsedMilliseconds;
                    twitchLastStepMilliseconds = __instance.Api.World.ElapsedMilliseconds;

                    if (random == null)
                    {
                        random = new Random((int)(__instance.EntityPlayer.EntityId + __instance.Api.World.ElapsedMilliseconds));
                    }

                    twitchX = (((float)random.NextDouble() - 0.5f) * 2f) * (weaponStats.twitchMax * twitchMultiplier) - ArcheryCore.aimOffsetX / (weaponStats.twitchMax * twitchMultiplier);
                    twitchY = (((float)random.NextDouble() - 0.5f) * 2f) * (weaponStats.twitchMax * twitchMultiplier) - ArcheryCore.aimOffsetY / (weaponStats.twitchMax * twitchMultiplier);

                    twitchLength = GameMath.Sqrt(twitchX * twitchX + twitchY * twitchY);

                    twitchX = twitchX / twitchLength;
                    twitchY = twitchY / twitchLength;
                }

                float lastStep = (twitchLastStepMilliseconds - twitchLastChangeMilliseconds) / (float)weaponStats.twitchDuration;
                float currentStep = (__instance.Api.World.ElapsedMilliseconds - twitchLastChangeMilliseconds) / (float)weaponStats.twitchDuration;

                float stepSize = ((1f - lastStep) * (1f - lastStep)) - ((1f - currentStep) * (1f - currentStep));

                //ArcheryCore.aimOffsetX += twitchX * stepSize * twitchMagnitude * dt;
                //ArcheryCore.aimOffsetY += twitchY * stepSize * twitchMagnitude * dt;

                ArcheryCore.aimOffsetX += twitchX * stepSize * (weaponStats.twitchMagnitude * twitchMultiplier * dt) * (weaponStats.twitchDuration / 20) * fovRatio;
                ArcheryCore.aimOffsetY += twitchY * stepSize * (weaponStats.twitchMagnitude * twitchMultiplier * dt) * (weaponStats.twitchDuration / 20) * fovRatio;

                twitchLastStepMilliseconds = __instance.Api.World.ElapsedMilliseconds;

                // Aiming itself
                float horizontalAimLimit = __instance.Width / 2f * weaponStats.horizontalLimit;
                float verticalAimLimit = __instance.Height / 2f * weaponStats.verticalLimit;
                float verticalAimOffset = __instance.Height / 2f * weaponStats.verticalOffset;

                float deltaX = (float)(___MouseDeltaX - ___DelayedMouseDeltaX);
                float deltaY = (float)(___MouseDeltaY - ___DelayedMouseDeltaY);

                if (Math.Abs(ArcheryCore.aimX + deltaX) > horizontalAimLimit)
                {
                    ArcheryCore.aimX = ArcheryCore.aimX > 0 ? horizontalAimLimit : -horizontalAimLimit;
                }
                else
                {
                    ArcheryCore.aimX += deltaX;
                    ___DelayedMouseDeltaX = ___MouseDeltaX;
                }

                if (Math.Abs(ArcheryCore.aimY + deltaY - verticalAimOffset) > verticalAimLimit)
                {
                    ArcheryCore.aimY = (ArcheryCore.aimY > 0 ? verticalAimLimit : -verticalAimLimit) + verticalAimOffset;
                }
                else
                {
                    ArcheryCore.aimY += deltaY;
                    ___DelayedMouseDeltaY = ___MouseDeltaY;
                }

                ArcheryCore.clientInstance.SetAim();
            }
            
            return true;
        }

        public static void SetRangedWeaponStats(ArcheryRangedWeaponStats weaponStats)
        {
            ClientMainPatch.weaponStats = weaponStats;
        }
    }
}