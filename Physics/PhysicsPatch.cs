using HarmonyLib;
using UnityEngine;
using SFS.World;
using SFS.Parts;

namespace WaterMod
{
    // 浮力系统补丁
    [HarmonyPatch(typeof(SFS.World.Physics), "FixedUpdate")]
    public class WaterBuoyancyPatch
    {
        [HarmonyPostfix]
        public static void ApplyBuoyancyForces(SFS.World.Physics __instance)
        {
            if (__instance == null || __instance.PhysicsObject == null) return;
            
            // 只有当PhysicsObject是Rocket时才计算浮力
            if (!(__instance.PhysicsObject is Rocket rocket)) return;
            
            if (rocket.rb2d == null) return;
            
            WaterBuoyancySystem buoyancySystem = WaterBuoyancySystem.main;
            if (buoyancySystem == null) return;
            
            // 获取火箭位置
            WorldLocation rocketLocation = rocket.location;
            if (rocketLocation == null) return;
            
            // 检查火箭是否有任何部件在水中
            if (!buoyancySystem.HasAnyPartInWater(rocket, rocketLocation)) return;
            
            // 为每个部件单独应用浮力
            foreach (Part part in rocket.partHolder.parts)
            {
                if (part == null) continue;
                
                // 计算并应用浮力
                Vector2 buoyancyForce = buoyancySystem.CalculateBuoyancyForce(part, rocketLocation);
                if (buoyancyForce != Vector2.zero)
                {
                    // 在部件位置施加浮力
                    rocket.rb2d.AddForceAtPosition(buoyancyForce, part.transform.position, ForceMode2D.Force);
                
                    if (WaterSettingsManager.settings.enableDebugLogs)
                    {
                        Debug.Log($"[WaterMod] Applied buoyancy force to part {part.name}: {buoyancyForce} at position {part.transform.position}");
                    }
                }
            }
            
            // 计算并应用角阻力
            float angularDrag = buoyancySystem.CalculateAngularDrag(rocket, rocketLocation);
            if (angularDrag != 0f)
            {
                rocket.rb2d.AddTorque(angularDrag, ForceMode2D.Force);
                
                if (WaterSettingsManager.settings.enableDebugLogs)
                {
                    Debug.Log($"[WaterMod] Applied angular drag: {angularDrag}");
                }
            }
            
            // 移除单独的浮力矩计算，因为浮力通过AddForceAtPosition已经产生了力矩
            // 额外的浮力矩会导致力矩重复计算
        }
    }
}
