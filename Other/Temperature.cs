using HarmonyLib;
using UnityEngine;
using SFS.World;
using SFS.Parts;

namespace WaterMod
{
    // 水下冷却
    [HarmonyPatch(typeof(Rocket))]
    public class TemperaturePatch
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            // 用反射找到Rocket的FixedUpdate方法
            return typeof(Rocket).GetMethod("FixedUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        }

        [HarmonyPostfix]
        public static void ApplyUnderwaterCooling(Rocket __instance)
        {
            if (__instance == null || __instance.rb2d == null) return;
            
            WaterBuoyancySystem buoyancySystem = WaterBuoyancySystem.main;
            if (buoyancySystem == null) return;
            
            // 获取火箭位置
            WorldLocation rocketLocation = __instance.location;
            if (rocketLocation == null) return;
            
            // 为每个部件单独应用水下冷却
            foreach (Part part in __instance.partHolder.parts)
            {
                if (part == null) continue;
                
                // 水下快速降温
                var heatModules = part.GetModules<SFS.Parts.Modules.HeatModule>();
                if (heatModules != null && heatModules.Length > 0)
                {
                    float partHeight = 1f; 
                    float partBottomHeight = (float)(__instance.location.Value.Height - partHeight * 0.5f);
                    double currentSeaLevel = WaterManager.GetCurrentSeaLevelHeight();
                    if (partBottomHeight < currentSeaLevel) 
                    {
                        foreach (var heat in heatModules)
                        {
                            // 每帧降温
                            heat.Temperature = Mathf.Max(heat.Temperature - WaterSettingsManager.settings.underwaterCoolingRate * Time.fixedDeltaTime, 0f);
                        }
                    }
                }
            }
        }
    }
}
