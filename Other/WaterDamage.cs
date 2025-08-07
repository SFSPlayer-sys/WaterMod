using HarmonyLib;
using UnityEngine;
using SFS.World;
using SFS.Parts;
using SFS.WorldBase;
using System.Collections.Generic;
using System;
using SFS.IO;
using SFS.Parsers.Json;
using Random = UnityEngine.Random;

namespace WaterMod
{
    [HarmonyPatch(typeof(Rocket), "FixedUpdate")]
    public class WaterImpactExplosion
    {
        // 跟踪每个火箭的水中状态
        private static Dictionary<Rocket, bool> rocketWaterStates = new Dictionary<Rocket, bool>();

        static bool Prefix(Rocket __instance)
        {
            // 检查火箭是否有位置和速度
            if (__instance?.location?.Value == null || __instance.rb2d == null)
                return true;

            // 检查是否开启了不可破坏部件作弊
            if (SandboxSettings.main?.settings?.unbreakableParts == true)
            {
                // 如果开启了不可破坏部件作弊，完全跳过水伤害检查
                if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
                {
                    Debug.Log("[WaterMod] Unbreakable parts cheat enabled, water damage system disabled");
                }
                return true;
            }

            // 获取火箭当前是否在水中
            bool currentlyInWater = WaterBuoyancySystem.main.IsInWater(__instance.location);
            
            // 获取火箭之前的状态
            bool wasInWater = false;
            if (rocketWaterStates.ContainsKey(__instance))
            {
                wasInWater = rocketWaterStates[__instance];
            }
            
            // 更新状态
            rocketWaterStates[__instance] = currentlyInWater;

            // 检查是否刚进入水中
            if (!wasInWater && currentlyInWater)
            {
                // 计算撞击速度
                float velocityMagnitude = __instance.rb2d.velocity.magnitude;
                
                // 检查是否超过爆炸阈值
                float explosionThreshold = GetExplosionThreshold(__instance);
                if (velocityMagnitude > explosionThreshold)
                {
                    // 创建爆炸效果
                    CreateWaterExplosion(__instance);
                    
                    // 检查是否超过解体阈值
                    float disintegrationThreshold = GetDisintegrationThreshold(__instance);
                    if (velocityMagnitude > disintegrationThreshold)
                    {
                        // 解体火箭部件
                        DisintegrateRocketParts(__instance, velocityMagnitude);
                    }
                }
            }

            return true;
        }

        // 获取爆炸阈值
        private static float GetExplosionThreshold(Rocket rocket)
        {
            if (rocket == null) return 30f;
            
            // 基础阈值
            float baseThreshold = 30f;
            
            // 根据火箭大小调整
            float rocketSize = GetRocketSize(rocket);
            float sizeMultiplier = Mathf.Clamp(rocketSize / 10f, 0.5f, 2f);
            
            // 根据水密度调整
            double waterDensity = WaterBuoyancySystem.main.GetWaterDensity();
            float densityFactor = Mathf.Clamp01((float)(waterDensity / 1000.0));
            
            // 最终阈值
            return Mathf.Max(baseThreshold, 30f) * 5f * sizeMultiplier * densityFactor;
        }

        // 获取解体阈值
        private static float GetDisintegrationThreshold(Rocket rocket)
        {
            if (rocket == null) return 50f;
            
            // 基础阈值
            float baseThreshold = 50f;
            
            // 根据火箭大小调整
            float rocketSize = GetRocketSize(rocket);
            float sizeMultiplier = Mathf.Clamp(rocketSize / 10f, 0.5f, 2f);
            
            // 根据水密度调整
            double waterDensity = WaterBuoyancySystem.main.GetWaterDensity();
            float densityFactor = Mathf.Clamp01((float)(waterDensity / 1000.0));
            
            // 最终阈值（比爆炸阈值低）
            return Mathf.Max(baseThreshold, 30f) * 2.5f * sizeMultiplier * densityFactor;
        }

        // 创建水爆炸效果
        private static void CreateWaterExplosion(Rocket rocket)
        {
            if (rocket?.location?.Value == null) return;
            
            // 创建爆炸效果 - 使用Vector3位置
            Vector3 explosionPosition = rocket.transform.position;
            EffectManager.CreateExplosion(explosionPosition, 2f);
            
            if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
            {
                Debug.Log($"[WaterMod] Created water explosion at: {rocket.location.Value}");
            }
        }

        // 解体火箭部件
        private static void DisintegrateRocketParts(Rocket rocket, float velocityMagnitude)
        {
            // 获取所有部件
            Part[] parts = rocket.partHolder.parts.ToArray();

            // 基于速度和火箭特性的科学解体计算
            float disintegrationProbability = CalculateDisintegrationProbability(rocket, velocityMagnitude);
            
            // 支持全部解体 - 移除最大解体数量限制
            int partsToDisintegrate = Mathf.RoundToInt(parts.Length * disintegrationProbability);
            
            // 确保至少解体1个部件，最多可以解体全部
            partsToDisintegrate = Mathf.Clamp(partsToDisintegrate, 1, parts.Length);

            if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
            {
                Debug.Log($"[WaterMod] Preparing to disintegrate {partsToDisintegrate}/{parts.Length} parts, probability: {disintegrationProbability:F2}");
            }

            // 随机选择要解体的部件
            List<Part> partsToDisintegrateList = new List<Part>(parts);
            for (int i = 0; i < partsToDisintegrate; i++)
            {
                if (partsToDisintegrateList.Count == 0) break;
                
                // 基于部件脆弱性选择解体部件
                int selectedIndex = SelectPartForDisintegration(partsToDisintegrateList, velocityMagnitude);
                if (selectedIndex >= 0 && selectedIndex < partsToDisintegrateList.Count)
                {
                    Part partToDisintegrate = partsToDisintegrateList[selectedIndex];
                    
                    // 使用游戏内置的解体机制
                    if (partToDisintegrate != null)
                    {
                        DisintegratePartUsingJoints(partToDisintegrate);
                        
                        if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
                        {
                            Debug.Log($"[WaterMod] Disintegrating part: {partToDisintegrate.name}");
                        }
                    }
                    
                    // 从列表中移除已解体的部件
                    partsToDisintegrateList.RemoveAt(selectedIndex);
                }
            }
        }

        // 使用游戏内置的解体机制
        private static void DisintegratePartUsingJoints(Part part)
        {
            if (part == null || part.Rocket == null) return;

            try
            {
                // 获取部件的所有连接
                List<PartJoint> connectedJoints = part.Rocket.jointsGroup.GetConnectedJoints(part);
                
                if (connectedJoints.Count > 0)
                {
                    // 破坏第一个连接，实现解体
                    bool split;
                    Rocket newRocket;
                    JointGroup.DestroyJoint(connectedJoints[0], part.Rocket, out split, out newRocket);
                    
                    if (split && newRocket != null)
                    {
                        // 启用碰撞免疫，防止立即碰撞
                        part.Rocket.EnableCollisionImmunity(1.5f);
                        newRocket.EnableCollisionImmunity(1.5f);
                        
                        // 如果原火箭是玩家控制的，设置新的控制目标
                        if (part.Rocket.isPlayer)
                        {
                            Rocket.SetPlayerToBestControllable(new Rocket[] { part.Rocket, newRocket });
                        }
                        
                        if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
                        {
                            Debug.Log($"[WaterMod] Part {part.name} disintegrated - Rocket split into 2 pieces");
                        }
                    }
                    else
                    {
                        // 如果没有成功分离，则摧毁部件
                        part.DestroyPart(true, true, DestructionReason.TerrainCollision);
                        
                        if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
                        {
                            Debug.Log($"[WaterMod] Part {part.name} destroyed (no joints to break)");
                        }
                    }
                }
                else
                {
                    // 如果没有连接，直接摧毁部件
                    part.DestroyPart(true, true, DestructionReason.TerrainCollision);
                    
                    if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
                    {
                        Debug.Log($"[WaterMod] Part {part.name} destroyed (no joints found)");
                    }
                }
            }
            catch (System.Exception ex)
            {
                if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
                {
                    Debug.LogError($"[WaterMod] Error disintegrating part {part.name}: {ex.Message}");
                }
                
                // 备用方案：使用普通的摧毁方法
                part.DestroyPart(true, true, DestructionReason.TerrainCollision);
            }
        }

        // 计算解体概率
        private static float CalculateDisintegrationProbability(Rocket rocket, float velocityMagnitude)
        {
            if (rocket == null) return 0.5f;
            
            // 基础概率
            float baseProbability = 0.3f;
            
            // 速度因子
            float velocityFactor = Mathf.Clamp01(velocityMagnitude / 100f);
            
            // 火箭复杂度因子
            float complexityFactor = GetRocketComplexity(rocket);
            
            // 部件强度因子
            float strengthFactor = GetAveragePartStrength(rocket);
            
            // 最终概率
            float finalProbability = baseProbability * velocityFactor * complexityFactor * strengthFactor;
            
            return Mathf.Clamp01(finalProbability);
        }

        // 获取火箭复杂度
        private static float GetRocketComplexity(Rocket rocket)
        {
            if (rocket?.partHolder?.parts == null) return 1f;
            
            // 部件数量
            float partCount = rocket.partHolder.parts.Count;
            
            // 连接数量
            float jointCount = rocket.jointsGroup.joints.Count;
            
            // 复杂度 = 部件数量 * 连接密度
            float complexity = partCount * (jointCount / Mathf.Max(partCount, 1f));
            
            return Mathf.Clamp(complexity / 10f, 0.5f, 2f);
        }

        // 获取平均部件强度
        private static float GetAveragePartStrength(Rocket rocket)
        {
            if (rocket?.partHolder?.parts == null) return 1f;
            
            float totalStrength = 0f;
            int validParts = 0;
            
            foreach (Part part in rocket.partHolder.parts)
            {
                if (part != null)
                {
                    float partStrength = GetPartTypeVulnerability(part);
                    totalStrength += partStrength;
                    validParts++;
                }
            }
            
            return validParts > 0 ? totalStrength / validParts : 1f;
        }

        // 选择要解体的部件
        private static int SelectPartForDisintegration(List<Part> parts, float velocityMagnitude)
        {
            if (parts == null || parts.Count == 0) return -1;
            
            // 计算每个部件的解体权重
            float[] weights = new float[parts.Count];
            float totalWeight = 0f;
            
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] != null)
                {
                    // 基于部件脆弱性计算权重
                    float vulnerability = GetPartTypeVulnerability(parts[i]);
                    
                    // 基于部件位置计算权重（外部部件更容易解体）
                    float positionWeight = GetPartPositionWeight(parts[i]);
                    
                    // 基于部件大小计算权重（大部件更容易解体）
                    float sizeWeight = GetPartSizeWeight(parts[i]);
                    
                    weights[i] = vulnerability * positionWeight * sizeWeight;
                    totalWeight += weights[i];
                }
            }
            
            // 如果没有有效权重，随机选择
            if (totalWeight <= 0f)
            {
                return Random.Range(0, parts.Count);
            }
            
            // 基于权重随机选择
            float randomValue = Random.Range(0f, totalWeight);
            float currentWeight = 0f;
            
            for (int i = 0; i < parts.Count; i++)
            {
                currentWeight += weights[i];
                if (randomValue <= currentWeight)
                {
                    return i;
                }
            }
            
            return parts.Count - 1;
        }

        // 获取部件位置权重
        private static float GetPartPositionWeight(Part part)
        {
            if (part == null) return 1f;
            
            // 获取部件在火箭中的相对位置
            Vector3 partPosition = part.transform.position;
            Vector3 rocketCenter = part.Rocket.transform.position;
            Vector3 relativePosition = partPosition - rocketCenter;
            
            // 距离中心的距离
            float distanceFromCenter = relativePosition.magnitude;
            
            // 外部部件权重更高
            return Mathf.Clamp(distanceFromCenter / 5f, 0.5f, 2f);
        }

        // 获取部件大小权重
        private static float GetPartSizeWeight(Part part)
        {
            if (part == null) return 1f;
            
            // 获取部件大小
            Vector3 partScale = part.transform.localScale;
            float partSize = partScale.magnitude;
            
            // 大部件权重更高
            return Mathf.Clamp(partSize / 2f, 0.5f, 2f);
        }

        // 获取部件类型脆弱性
        private static float GetPartTypeVulnerability(Part part)
        {
            if (part == null) return WaterSettingsManager.strengthSettings.defaultStrength;
            
            // 获取部件的所有模块类型
            var modules = part.GetModules<object>();
            
            // 如果没有模块，使用默认强度
            if (modules == null || modules.Length == 0)
            {
                return WaterSettingsManager.strengthSettings.defaultStrength;
            }
            
            float maxVulnerability = WaterSettingsManager.strengthSettings.defaultStrength;
            
            foreach (var module in modules)
            {
                if (module == null) continue;
                
                // 获取模块的完整类型名称
                string moduleTypeName = module.GetType().Name;
                
                // 根据模块类型获取强度设置
                float strength = GetStrengthByModuleType(moduleTypeName);
                float vulnerability = 1f - strength; // 强度越高，脆弱性越低
                maxVulnerability = Mathf.Max(maxVulnerability, vulnerability);
            }
            
            return maxVulnerability;
        }

        // 根据模块类型获取强度
        private static float GetStrengthByModuleType(string moduleTypeName)
        {
            switch (moduleTypeName)
            {
                case "EngineModule":
                    return WaterSettingsManager.strengthSettings.engineStrength;
                case "AeroModule":
                    return WaterSettingsManager.strengthSettings.aeroStrength;
                case "BoosterModule":
                    return WaterSettingsManager.strengthSettings.boosterStrength;
                case "SplitModule":
                    return WaterSettingsManager.strengthSettings.separatorStrength;
                case "DockingPortModule":
                    return WaterSettingsManager.strengthSettings.dockingPortStrength;
                case "RcsModule":
                    return WaterSettingsManager.strengthSettings.rcsStrength;
                case "ResourceModule":
                    return WaterSettingsManager.strengthSettings.resourceStrength;
                case "CrewModule":
                    return WaterSettingsManager.strengthSettings.crewStrength;
                case "HeatModule":
                    return WaterSettingsManager.strengthSettings.heatStrength;
                case "ParachuteModule":
                    return WaterSettingsManager.strengthSettings.parachuteStrength;
                case "WheelModule":
                    return WaterSettingsManager.strengthSettings.wheelStrength;
                case "ToggleModule":
                    return WaterSettingsManager.strengthSettings.toggleStrength;
                case "LES_Module":
                    return WaterSettingsManager.strengthSettings.lesStrength;
                case "ParticleModule":
                    return WaterSettingsManager.strengthSettings.particleStrength;
                case "InteriorModule":
                    return WaterSettingsManager.strengthSettings.interiorStrength;
                case "ActiveModule":
                    return WaterSettingsManager.strengthSettings.activeStrength;
                default:
                    // 未知部件类型直接使用默认强度
                    return WaterSettingsManager.strengthSettings.defaultStrength;
            }
        }

        // 获取火箭大小
        private static float GetRocketSize(Rocket rocket)
        {
            if (rocket?.partHolder?.parts == null) return 1f;
            
            float totalSize = 0f;
            foreach (Part part in rocket.partHolder.parts)
            {
                if (part != null)
                {
                    Vector3 partScale = part.transform.localScale;
                    totalSize += partScale.magnitude;
                }
            }
            
            return totalSize;
        }
        
        // 清理火箭状态（当火箭被销毁时）
        public static void CleanupRocketState(Rocket rocket)
        {
            if (rocketWaterStates.ContainsKey(rocket))
            {
                rocketWaterStates.Remove(rocket);
            }
        }
    }
} 