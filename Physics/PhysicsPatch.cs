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
            
            // 检查火箭是否有任何部件在水中（浮力对所有有部件在水中的火箭都应用）
            bool hasPartsInWater = buoyancySystem.HasAnyPartInWater(rocket, rocketLocation);
            
            if (hasPartsInWater)
            {
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
            }
            
            // 只对真正在水下的火箭（中心在水下）应用水下阻力
            // 这确保水上火箭不会受到水下阻力的影响
            if (buoyancySystem.IsInWater(rocketLocation))
            {
                // 计算并应用水下平移阻力（横向/纵向阻力）
                Vector2 linearDrag = buoyancySystem.CalculateLinearDrag(rocket, rocketLocation);
                if (linearDrag != Vector2.zero)
                {
                    rocket.rb2d.AddForce(linearDrag, ForceMode2D.Force);
                    if (WaterSettingsManager.settings.enableDebugLogs)
                    {
                        Debug.Log($"[WaterMod] Applied linear water drag: {linearDrag}");
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
            }
            
            // 移除单独的浮力矩计算，因为浮力通过AddForceAtPosition已经产生了力矩
            // 额外的浮力矩会导致力矩重复计算
        }
    }
}
