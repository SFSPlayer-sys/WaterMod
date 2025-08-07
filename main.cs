using HarmonyLib;
using UnityEngine;
using SFS;
using SFS.World;
using UnityEngine.SceneManagement;

namespace WaterMod
{
    public class WaterMod : ModLoader.Mod
    {
        public override string ModNameID => "WaterMod";
        public override string DisplayName => "WaterMod";
        public override string Author => "SFSGamer";
        public override string MinimumGameVersionNecessary => "1.5.10.2";
        public override string ModVersion => "1.0.0";
        public override string Description => "Adds water system to planets";

        private Harmony patcher;
        private GameObject configObject;

        public override void Load()
        {
            // 加载配置文件
            WaterSettingsManager.Load();
            
            // 创建配置对象
            CreateConfigObject();
            
            // 初始化浮力系统
            InitializeBuoyancySystem();
            
            // 检查是否在Hub场景
            CheckWorldScene();
        }

        private void CreateConfigObject()
        {
            // 创建配置对象
            configObject = new GameObject("WaterMod_Config");
            GameObject.DontDestroyOnLoad(configObject);
        }

        private void InitializeBuoyancySystem()
        {
            // 初始化浮力系统
            WaterBuoyancySystem buoyancySystem = WaterBuoyancySystem.main;
        }

        private void CheckWorldScene()
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }

        public override void Early_Load()
        {
            patcher = new Harmony("com.sfsgamer.water");
            patcher.PatchAll();
        }
    }
} 