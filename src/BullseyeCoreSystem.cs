using System;
using System.Collections.Generic;
using Vintagestory.Common;
using Vintagestory.API.Common;

using HarmonyLib;

namespace Bullseye
{
    public class BullseyeCoreSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            ClassRegistry classRegistry = Traverse.Create(api.ClassRegistry).Field<ClassRegistry>("registry").Value;
            //ClassRegistry classRegistry = api.ClassRegistry as ClassRegistry;

            RegisterItems(classRegistry);
            RegisterEntityBehaviors(classRegistry);
        }

        private void RegisterItems(ClassRegistry classRegistry)
        {
            classRegistry.ItemClassToTypeMapping["ItemBow"] = typeof(Bullseye.ItemBow);
            classRegistry.ItemClassToTypeMapping["ItemSpear"] = typeof(Bullseye.ItemSpear);
        }

        private void RegisterEntityBehaviors(ClassRegistry classRegistry)
        {
            //classRegistry.entityBehaviorClassNameToTypeMapping["aimingaccuracy"] = typeof(Bullseye.EntityBehaviorAimingAccuracy);
            //classRegistry.entityBehaviorTypeToClassNameMapping[typeof(Bullseye.EntityBehaviorAimingAccuracy)] = "aimingaccuracy";

            // Not replacing the vanilla AimingAccuracy behaviour for compatibility
            classRegistry.RegisterentityBehavior("bullseye.aimingaccuracy", typeof(Bullseye.EntityBehaviorAimingAccuracy));
        }
    }
}