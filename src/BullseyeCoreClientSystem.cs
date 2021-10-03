using System;
using System.Collections.Generic;
using Vintagestory.Client.NoObf;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using ProtoBuf;
using System.Reflection;

using HarmonyLib;

namespace Bullseye
{
    public class BullseyeCoreClientSystem : ModSystem
    {
        public BullseyeConfigSystem configSystem;

        public bool aiming = false;

        public float aimX;
        public float aimY;

        public float aimOffsetX;
        public float aimOffsetY;

        public Vec3d targetVec;

        private ClientMain client;

        private Unproject unproject;
        private double[] viewport;
        private double[] rayStart;
        private double[] rayEnd;

        StackMatrix4 mvMatrix;
        StackMatrix4 pMatrix;

        Harmony harmony;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            harmony = new Harmony("vs.bullseye");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            client = capi.World as ClientMain;
            unproject = new Unproject();
            viewport = new double[4];
            rayStart = new double[4];
            rayEnd = new double[4];
            
            targetVec = new Vec3d();

            mvMatrix = Traverse.Create(client).Field<StackMatrix4>("MvMatrix").Value;
            pMatrix = Traverse.Create(client).Field<StackMatrix4>("PMatrix").Value;

            configSystem = capi.ModLoader.GetModSystem<BullseyeConfigSystem>();

            SystemRenderAimPatch.coreClientSystem = this;
            ClientMainPatch.coreClientSystem = this;
        }

        public Vec2f GetCurrentAim()
        {
            float offsetMagnitude = configSystem.serverConfig.globalAccuracy;

            if (client.EntityPlayer != null)
            {
                offsetMagnitude /= client.EntityPlayer.Stats.GetBlended("rangedWeaponsAcc");
            }

            return new Vec2f(aimX + aimOffsetX * offsetMagnitude, aimY + aimOffsetY * offsetMagnitude);
        }

        public void SetAim()
		{
            Vec2f currentAim = GetCurrentAim();

			int mouseCurrentX = (int)currentAim.X + client.Width / 2;
			int mouseCurrentY = (int)currentAim.Y + client.Height / 2;
			viewport[0] = 0.0;
			viewport[1] = 0.0;
			viewport[2] = client.Width;
			viewport[3] = client.Height;
			unproject.UnProject(mouseCurrentX, client.Height - mouseCurrentY, 1, mvMatrix.Top, pMatrix.Top, viewport, rayEnd);
			unproject.UnProject(mouseCurrentX, client.Height - mouseCurrentY, 0, mvMatrix.Top, pMatrix.Top, viewport, rayStart);
			double offsetX = rayEnd[0] - rayStart[0];
			double offsetY = rayEnd[1] - rayStart[1];
			double offsetZ = rayEnd[2] - rayStart[2];
			float length = (float)Math.Sqrt(offsetX * offsetX + offsetY * offsetY + offsetZ * offsetZ);
			offsetX /= length;
			offsetY /= length;
			offsetZ /= length;

            targetVec.X = offsetX;
            targetVec.Y = offsetY;
            targetVec.Z = offsetZ;
		}
        
        public void SetClientRangedWeaponStats(BullseyeRangedWeaponStats weaponStats)
        {
            SystemRenderAimPatch.SetRangedWeaponStats(weaponStats);
            ClientMainPatch.SetRangedWeaponStats(weaponStats);
        }

        public void SetClientRangedWeaponReticleTextures(int aimPartChargeTexId, int aimFullChargeTexId, int aimTexBlockedId)
        {
            SystemRenderAimPatch.SetReticleTextures(aimPartChargeTexId, aimFullChargeTexId, aimTexBlockedId);
        }

        public override void Dispose()
        {
            configSystem = null;

            client = null;
            unproject = null;
            pMatrix = null;
            mvMatrix = null;

            //harmony.UnpatchAll("vs.bullseye");
            harmony = null;
        }
    }
}