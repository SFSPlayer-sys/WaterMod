using System;
using UnityEngine;
using SFS.IO;
using SFS.Parsers.Json;

namespace WaterMod
{
    [Serializable]
    public class WaterSettings
    {
        public bool enableDebugLogs = false;
        
        // 浮力相关参数
        public float globalBuoyancyMultiplier = 0.002f;      
        public float defaultBuoyancyIndex = 1.02f;         
        public float engineBuoyancyIndex = 0.05f;
        public float boosterBuoyancyIndex = 0.1f;
        public float resourceBuoyancyIndex = 1.2f;
        public float aeroBuoyancyIndex = 1.2f;  
        public float fairingBuoyancyIndex = 1.2f;  
        public float separatorBuoyancyIndex = 1.15f;
        public float dockingPortBuoyancyIndex = 1.1f;
        public float rcsBuoyancyIndex = 1.1f;
        public float wheelBuoyancyIndex = 1.02f;
        public float crewBuoyancyIndex = 1.15f;
        public float toggleBuoyancyIndex = 1.1f;
        public float lesBuoyancyIndex = 1.05f;
        public float particleBuoyancyIndex = 1.3f;
        public float interiorBuoyancyIndex = 1.02f;
        public float activeBuoyancyIndex = 1.02f;

        // 水面高度与密度
        public double defaultSeaLevelHeight = 50.0;
        public double defaultWaterDensity = 75.0;

        // 空气阻力密度乘数（保留用于兼容性）
        public float waterDensityMultiplier = 800.0f;        // 水密度相对于空气的倍数

        // 水撞击爆炸/解体相关
        public bool enableWaterExplosion = true;          // 是否启用撞击水爆炸
        public bool enableWaterDisintegration = true;     // 是否启用撞击水解体
        public float waterExplosionThreshold = 200f;      // 水撞击爆炸阈值 (m/s)
        public float waterDisintegrationThreshold = 50f; // 水撞击解体阈值 (m/s)
        public float baseDetachmentProbability = 0.2f;    // 部件基础脱离概率 (0-1)

        // 浮力矩相关
        public float globalBuoyancyTorqueCoefficient = 0.8f; // 浮力矩全局系数

        public float formDragCoefficient = 0.1f;          // 形状阻力系数（降低横向纵向阻力）
        public float dampingCoefficient = 3f;           // 阻尼系数（提高）
        public float interferenceDragCoefficient = 1.5f;  // 部件间干扰阻力系数（提高）
        
        // 角阻力
        public float angularDragCoefficient = 1f;       // 角阻力系数
        public float maxAngularDrag = 100f;                // 最大角阻力
        
        // 形状阻力详细设置
        public float streamlinedDragCoefficient = 0.2f;   // 流线型阻力系数
        public float surfaceRoughnessFactor = 1.0f;       // 表面粗糙度因子
        public float reynoldsNumberThreshold = 1000f;     // 雷诺数阈值
        public float laminarDragCoefficient = 0.3f;       // 层流阻力系数
        public float turbulentDragCoefficient = 0.8f;     // 湍流阻力系数
        public float transitionReynoldsNumber = 2000f;    // 转捩雷诺数
        
        // 阻尼详细设置
        public float criticalDampingRatio = 0.7f;         // 临界阻尼比
        public float maxDampingForce = 20f;               // 最大阻尼力
        
        // 部件间干扰阻力详细设置
        public float wakeEffectFactor = 0.4f;             // 尾流效应因子
        public float shieldingFactor = 0.7f;              // 遮挡因子
        
        public float aeroAngularDragCoefficient = 0.2f;   // 空气动力部件角阻力系数
        public float aeroBuoyancyTorqueCoefficient = 0.8f; // 空气动力部件浮力矩系数
        
    }

    // 部件强度配置类
    [Serializable]
    public class PartStrengthSettings
    {
        // 基于游戏默认强度设置的部件脆弱性
        public float defaultStrength = 0.5f;
        public float engineStrength = 0.7f;
        public float aeroStrength = 0.8f;
        public float boosterStrength = 0.6f;
        public float separatorStrength = 0.5f;
        public float dockingPortStrength = 0.4f;
        public float rcsStrength = 0.3f;
        public float resourceStrength = 0.2f;
        public float crewStrength = 0.1f;
        public float heatStrength = 0.4f;
        public float parachuteStrength = 0.9f;            
        public float wheelStrength = 0.1f;
        public float toggleStrength = 0.5f;
        public float lesStrength = 0.6f;
        public float particleStrength = 0.3f;
        public float interiorStrength = 0.2f;
        public float activeStrength = 0.5f;
    }

    public static class WaterSettingsManager
    {
        public static readonly FilePath Path = new FolderPath("Mods/WaterMod").ExtendToFile("settings.txt");
        public static readonly FilePath StrengthPath = new FolderPath("Mods/WaterMod").ExtendToFile("strength.txt");
        public static WaterSettings settings;
        public static PartStrengthSettings strengthSettings;

        public static void Load()
        {
            // 重新读取最新的设置文件
            if (!JsonWrapper.TryLoadJson(Path, out settings))
            {
                // 如果文件不存在或读取失败，使用默认设置
                settings = new WaterSettings();
                // 只在首次创建时保存默认设置
                Save();
            }

            // 加载强度配置文件
            LoadStrengthSettings();
        }

        public static void LoadStrengthSettings()
        {
            // 读取强度配置文件
            if (!JsonWrapper.TryLoadJson(StrengthPath, out strengthSettings))
            {
                // 如果文件不存在或读取失败，使用默认设置
                strengthSettings = new PartStrengthSettings();
                // 只在首次创建时保存默认设置
                SaveStrengthSettings();
            }
        }

        public static void Save()
        {
            Path.WriteText(JsonWrapper.ToJson(settings, true));
        }

        public static void SaveStrengthSettings()
        {
            StrengthPath.WriteText(JsonWrapper.ToJson(strengthSettings, true));
        }
    }
} 