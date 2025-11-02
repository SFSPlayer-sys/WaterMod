using HarmonyLib;
using Newtonsoft.Json.Linq;
using SFS;
using SFS.Parsers.Json;
using SFS.WorldBase;
using SFS.World.Legacy;
using SFS.World;
using System.Collections.Generic;
using UnityEngine;
using System.Linq; // Added for .Select()

namespace WaterMod
{
    [HarmonyPatch(typeof(LegacyConverter), "CheckAndConvert_Planet")]
    public class LegacyConverter_CheckAndConvert_Planet_Patch
    {
        [HarmonyPostfix]
        public static void CheckAndConvert_Planet_Postfix(string name, string jsonText, I_MsgLogger log, bool converted, bool success, PlanetData __result)
        {
            // 只处理成功加载的星球数据
            if (__result == null || !success)
                return;

            try
            {
                if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
                {
                    Debug.Log($"[WaterMod] Processing planet: {name}");
                    Debug.Log($"[WaterMod] JSON text length: {jsonText.Length}");
                    
                    // 检查JSON中是否包含WATER_DATA字符串
                    if (jsonText.Contains("WATER_DATA"))
                    {
                        Debug.Log($"[WaterMod] Found 'WATER_DATA' string in JSON for planet: {name}");
                    }
                    else
                    {
                        Debug.Log($"[WaterMod] No 'WATER_DATA' string found in JSON for planet: {name}");
                    }
                }

                // 解析原始JSON
                JObject jObject = JObject.Parse(jsonText);
                
                // 检查是否有WATER_DATA字段
                if (jObject.TryGetValue("WATER_DATA", out JToken waterToken))
                {
                    if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
                    {
                        Debug.Log($"[WaterMod] Found WATER_DATA token for planet: {name}");
                        Debug.Log($"[WaterMod] WATER_DATA token type: {waterToken.Type}");
                        Debug.Log($"[WaterMod] WATER_DATA JSON: {waterToken.ToString()}");
                    }

                    // 解析WATER_DATA
                    WaterData waterData = waterToken.ToObject<WaterData>();

                    // 使用文件名作为星球名称
                    string planetName = name;

                    // 保存到全局管理器
                    if (waterData != null)
                    {
                        WaterManager.RegisterPlanetWater(planetName, waterData);
                        
                        if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
                        {
                            Debug.Log($"[WaterMod] Successfully parsed WATER_DATA for planet: {name}");
                            Debug.Log($"[WaterMod] Parsed sea level height: {waterData.seaLevelHeight}");
                            Debug.Log($"[WaterMod] Parsed water density: {waterData.waterDensity}");
                            Debug.Log($"[WaterMod] All planets with water data: {string.Join(", ", WaterManager.GetAllPlanetNames())}");
                        }
                    }
                    else
                    {
                        if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
                        {
                            Debug.LogWarning($"[WaterMod] Failed to parse WATER_DATA for planet: {name}");
                            Debug.LogWarning($"[WaterMod] WATER_DATA token was null after ToObject<WaterData>()");
                        }
                    }
                }
                else
                {
                    if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
                    {
                        Debug.Log($"[WaterMod] No WATER_DATA field found in JSON for planet: {name}");
                        Debug.Log($"[WaterMod] Available JSON fields: {string.Join(", ", jObject.Properties().Select(p => p.Name))}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WaterMod] Error parsing WATER_DATA for planet {name}: {ex.Message}");
                if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
                {
                    Debug.LogError($"[WaterMod] Exception stack trace: {ex.StackTrace}");
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlanetLoader), "LoadPlanets_Public")]
    public class PlanetLoader_LoadPlanets_Public_Patch
    {
        [HarmonyPrefix]
        public static void LoadPlanets_Public_Prefix(SolarSystemReference solarSystem)
        {
            WaterManager.ClearAll();
            
            if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
            {
                Debug.Log($"[WaterMod] Cleared previous water data before loading solar system: {solarSystem.name}");
            }
        }
        
        [HarmonyPostfix]
        public static void LoadPlanets_Public_Postfix(SolarSystemReference solarSystem, Dictionary<string, PlanetData> outputPlanetData, I_MsgLogger log)
        {
            // 设置当前星系名
            WaterManager.SetCurrentSolarSystem(solarSystem.name);
            
            if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
            {
                Debug.Log($"[WaterMod] Loading planets for solar system: {solarSystem.name}");
                
                // 遍历所有加载的星球数据
                foreach (var kvp in outputPlanetData)
                {
                    string planetName = kvp.Key;
                    PlanetData planetData = kvp.Value;
                    
                    // 检查星球数据中是否有WATER_DATA
                    if (planetData != null)
                    {
                        Debug.Log($"[WaterMod] Checking planet: {planetName}");
                    }
                }
            }
        }
    }

    // 添加对文件加载的补丁
    [HarmonyPatch(typeof(PlanetLoader), "LoadTextures_Public")]
    public class PlanetLoader_LoadTextures_Public_Patch
    {
        [HarmonyPostfix]
        public static void LoadTextures_Public_Postfix(SolarSystemReference solarSystem, Dictionary<string, Texture2D> outputTextures, I_MsgLogger log)
        {

            if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
            {
                Debug.Log($"[WaterMod] Loading textures for solar system: {solarSystem.name}");
                
                // 检查是否加载了海洋纹理
                foreach (var kvp in outputTextures)
                {
                    string textureName = kvp.Key;
                    if (textureName.Contains("Ocean") || textureName.Contains("Water"))
                    {
                        Debug.Log($"[WaterMod] Found water-related texture: {textureName}");
                    }
                }
            }
        }
    }

    // 水数据
    [System.Serializable]
    public class WaterData
    {
        // 海洋遮罩纹理
        [Newtonsoft.Json.JsonProperty("ocean mask texture")]
        public string oceanMaskTexture;

        // 颜色配置
        public Color sand;
        public Color floor;
        public Color shallow;
        public Color deep;

        // 海平面高度
        [Newtonsoft.Json.JsonProperty("sea level height")]
        public double seaLevelHeight;

        // 水密度
        [Newtonsoft.Json.JsonProperty("water density")]
        public double waterDensity;

        // 水阻力系数
        [Newtonsoft.Json.JsonProperty("water drag coefficient")]
        public double waterDragCoefficient;

        // 水阻力乘数
        [Newtonsoft.Json.JsonProperty("water drag multiplier")]
        public double waterDragMultiplier;
    }

    [System.Serializable]
    public class MaskGradient
    {
        public double must;
        public double cannot;
        public double global;
    }

    // 全局水域管理器
    public static class WaterManager
    {
        private static Dictionary<string, WaterData> planetWaterDict = new Dictionary<string, WaterData>();
        private static string currentSolarSystemName = ""; // 添加当前星系名

        // 设置当前星系名
        public static void SetCurrentSolarSystem(string solarSystemName)
        {
            currentSolarSystemName = solarSystemName;

            if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
            {
                Debug.Log($"[WaterMod] Set current solar system: {solarSystemName}");
            }
        }

        // 获取当前星系名
        public static string GetCurrentSolarSystem()
        {
            return currentSolarSystemName;
        }

        // 注册星球水域数据
        public static void RegisterPlanetWater(string planetName, WaterData waterData)
        {
            planetWaterDict[planetName] = waterData;
            
            // 更新浮力系统的海平面高度和水密度
            if (waterData != null)
            {
                WaterBuoyancySystem buoyancySystem = WaterBuoyancySystem.main;
                if (buoyancySystem != null)
                {
                    if (waterData.seaLevelHeight > 0)
                    {
                        buoyancySystem.SetSeaLevelHeight(waterData.seaLevelHeight);
                        if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
                        {
                            Debug.Log($"[WaterMod] Registering water data for planet: {planetName}");
                            Debug.Log($"[WaterMod] Water data sea level height: {waterData?.seaLevelHeight}");
                            Debug.Log($"[WaterMod] Water data water density: {waterData?.waterDensity}");
                            Debug.Log($"[WaterMod] Successfully set sea level height to: {waterData.seaLevelHeight}");
                            Debug.Log($"[WaterMod] Current planet name: {GetCurrentPlanetName()}");
                            Debug.Log($"[WaterMod] Total planets with water data: {planetWaterDict.Count}");
                        }
                    }
                    else
                    {
                        if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
                        {
                            Debug.LogWarning($"[WaterMod] Invalid sea level height: {waterData.seaLevelHeight}");
                        }
                    }
                    
                    if (waterData.waterDensity > 0)
                    {
                        buoyancySystem.SetWaterDensity(waterData.waterDensity);
                        if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
                        {
                            Debug.Log($"[WaterMod] Successfully set water density to: {waterData.waterDensity}");
                        }
                    }
                    else
                    {
                        if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
                        {
                            Debug.LogWarning($"[WaterMod] Invalid water density: {waterData.waterDensity}");
                        }
                    }
                }
                else
                {
                    if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
                    {
                        Debug.LogWarning($"[WaterMod] Buoyancy system is null, cannot set water parameters");
                    }
                }
            }
        }

        // 获取星球水域数据
        public static WaterData GetPlanetWater(string planetName)
        {
            return planetWaterDict.ContainsKey(planetName) ? planetWaterDict[planetName] : null;
        }

        // 检查星球是否有水域数据
        public static bool HasPlanetWater(string planetName)
        {
            return planetWaterDict.ContainsKey(planetName);
        }

        // 获取当前星球的海平面高度
        public static double GetCurrentSeaLevelHeight()
        {
            string currentPlanet = GetCurrentPlanetName();
            if (currentPlanet != null && planetWaterDict.ContainsKey(currentPlanet))
            {
                return planetWaterDict[currentPlanet].seaLevelHeight;
            }
            return 0.0; // 如果没有配置，返回0
        }

        // 清除所有数据
        public static void ClearAll()
        {
            planetWaterDict.Clear();
            currentSolarSystemName = "";
        }

        public static string GetCurrentPlanetName()
        {
            // 只从游戏的标准方式获取当前星球名称
            if (PlayerController.main?.player?.Value?.location?.planet?.Value != null)
            {
                string gamePlanetName = PlayerController.main.player.Value.location.planet.Value.codeName;
                
                if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
                {
                    Debug.Log($"[WaterMod] Getting current planet name from game: {gamePlanetName}");
                }
                
                return gamePlanetName;
            }
            
            // 如果游戏方式获取失败，返回null
            if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
            {
                Debug.Log($"[WaterMod] Failed to get planet name from game");
            }
            
            return null;
        }
        
        public static List<string> GetAllPlanetNames()
        {
            return new List<string>(planetWaterDict.Keys);
        }
    }
} 