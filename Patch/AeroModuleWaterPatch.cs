using HarmonyLib;
using SFS.World.Drag;
using UnityEngine;
using SFS.World;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace WaterMod
{
    [HarmonyPatch(typeof(AeroModule))]
    public class AeroModuleWaterPatch
    {
        static System.Reflection.MethodBase TargetMethod()
        {

            return typeof(AeroModule).GetMethod("FixedUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        }

        [HarmonyPrefix]
        static bool Prefix(AeroModule __instance)
        {
            if (__instance is Aero_Rocket aeroRocket && aeroRocket.rocket?.location?.Value != null)
            {
                if (WaterBuoyancySystem.main.IsInWater(aeroRocket.rocket.location))
                {
                    return true;
                }
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(AeroModule))]
    public class AeroModuleForcePatch
    {
        static System.Reflection.MethodBase TargetMethod()
        {

            return typeof(AeroModule).GetMethod("ApplyForce", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        }

        [HarmonyPrefix]
        static bool Prefix(AeroModule __instance, List<Surface> exposedSurfaces, Location location, Matrix2x2 localToWorld, out float g_ForSound)
        {
            if (__instance is Aero_Rocket aeroRocket && aeroRocket.rocket?.location?.Value != null)
            {
                WaterBuoyancySystem buoyancySystem = WaterBuoyancySystem.main;
                if (buoyancySystem != null && buoyancySystem.IsInWater(aeroRocket.rocket.location))
                {
                    // 在水下时，跳过空气动力部件的阻力计算
                    g_ForSound = 0f;
                    return false;
                }
            }
            g_ForSound = 0f;
            return true;
        }
    }
}
