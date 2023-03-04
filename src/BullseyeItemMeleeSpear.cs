using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Bullseye
{
    public class BullseyeItemMeleeSpear : Item
    {
        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
        {
            return null;
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            byEntity.Attributes.SetInt("didattack", 0);

            byEntity.World.RegisterCallback((dt) =>
            {
                IPlayer byPlayer = (byEntity as EntityPlayer).Player;
                if (byPlayer == null) return;

                if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemAttack)
                {
                    var pitch = (byEntity as EntityPlayer).talkUtil.pitchModifier;
                    byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/player/strike"), byPlayer.Entity, byPlayer, pitch * 0.9f + (float)api.World.Rand.NextDouble() * 0.2f, 16, 0.35f);
                }
            }, 464);

            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldAttackCancel(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return false;
        }

        public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            float backwards = -Math.Min(0.8f, 3 * secondsPassed);
            float stab = Math.Min(1.2f, 20 * Math.Max(0, secondsPassed - 0.25f));

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                IClientWorldAccessor world = byEntity.World as IClientWorldAccessor;
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

              
                float sum = stab + backwards;
                float ztranslation = Math.Min(0.2f, 1.5f * secondsPassed);
                float easeout = Math.Max(0, 2 * (secondsPassed - 1));

                if (secondsPassed > 0.4f) sum = Math.Max(0, sum - easeout);
                ztranslation = Math.Max(0, ztranslation - easeout);

                tf.Translation.Set(-1f * sum, ztranslation * 0.4f, -sum * 0.8f * 2.6f);
                tf.Rotation.Set(-sum * 9, sum * 30, -sum*30);

                byEntity.Controls.UsingHeldItemTransformAfter = tf;
                

                if (stab > 1.15f && byEntity.Attributes.GetInt("didattack") == 0)
                {
                    world.TryAttackEntity(entitySel);
                    byEntity.Attributes.SetInt("didattack", 1);
                    world.AddCameraShake(0.25f);
                }
            } else
            {
                if (stab > 1.15f && byEntity.Attributes.GetInt("didattack") == 0 && entitySel != null)
                {
                    byEntity.Attributes.SetInt("didattack", 1);

                    bool canhackEntity =
                        entitySel.Entity.Properties.Attributes?["hackedEntity"].Exists == true
                        && slot.Itemstack.ItemAttributes.IsTrue("hacking") == true && api.ModLoader.GetModSystem<CharacterSystem>().HasTrait((byEntity as EntityPlayer).Player, "technical")
                    ;
                    ICoreServerAPI sapi = api as ICoreServerAPI;

                    if (canhackEntity)
                    {
                        sapi.World.PlaySoundAt(new AssetLocation("sounds/player/hackingspearhit.ogg"), entitySel.Entity, null);
                    }

                    if (api.World.Rand.NextDouble() < 0.15 && canhackEntity)
                    {
                        SpawnEntityInPlaceOf(entitySel.Entity, entitySel.Entity.Properties.Attributes["hackedEntity"].AsString(), byEntity);
                        sapi.World.DespawnEntity(entitySel.Entity, new EntityDespawnData() { Reason = EnumDespawnReason.Removed });
                    }
                }
            }

            return secondsPassed < 1.2f;
        }


        private void SpawnEntityInPlaceOf(Entity byEntity, string code, EntityAgent causingEntity)
        {
            AssetLocation location = AssetLocation.Create(code, byEntity.Code.Domain);
            EntityProperties type = byEntity.World.GetEntityType(location);
            if (type == null)
            {
                byEntity.World.Logger.Error("ItemCreature: No such entity - {0}", location);
                if (api.World.Side == EnumAppSide.Client)
                {
                    (api as ICoreClientAPI).TriggerIngameError(this, "nosuchentity", string.Format("No such entity loaded - '{0}'.", location));
                }
                return;
            }

            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);

            if (entity != null)
            {
                entity.ServerPos.X = byEntity.ServerPos.X;
                entity.ServerPos.Y = byEntity.ServerPos.Y;
                entity.ServerPos.Z = byEntity.ServerPos.Z;
                entity.ServerPos.Motion.X = byEntity.ServerPos.Motion.X;
                entity.ServerPos.Motion.Y = byEntity.ServerPos.Motion.Y;
                entity.ServerPos.Motion.Z = byEntity.ServerPos.Motion.Z;
                entity.ServerPos.Yaw = byEntity.ServerPos.Yaw;

                entity.Pos.SetFrom(entity.ServerPos);
                entity.PositionBeforeFalling.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);

                entity.Attributes.SetString("origin", "playerplaced");

                
                entity.WatchedAttributes.SetLong("guardedEntityId", byEntity.EntityId);
                if (causingEntity is EntityPlayer eplr)
                {
                    entity.WatchedAttributes.SetString("guardedPlayerUid", eplr.PlayerUID);
                }

                byEntity.World.SpawnEntity(entity);
            }
        }

        public override void OnHeldAttackStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {

        }
    }
}
