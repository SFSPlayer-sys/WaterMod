using HarmonyLib;
using SFS.World.Drag;
using UnityEngine;
using SFS.World;
using System.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SFS.World.Drag;
using UnityEngine;

namespace WaterMod
{
    // 补丁AeroModule的FixedUpdate方法
    [HarmonyPatch(typeof(AeroModule))]
    public class AeroModuleWaterPatch
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            // 用反射找到AeroModule的FixedUpdate
            return typeof(AeroModule).GetMethod("FixedUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        }

        [HarmonyPrefix]
        static bool Prefix(AeroModule __instance)
        {
            // 检查是否Aero_Rocket，且有火箭和位置
            if (__instance is Aero_Rocket aeroRocket && aeroRocket.rocket?.location?.Value != null)
            {
                // 在水中时，让空气阻力系统继续工作，但我们会修改密度
                if (WaterBuoyancySystem.main.IsInWater(aeroRocket.rocket.location))
                {
                    // 返回true，让原始方法继续执行，但我们会用Postfix修改密度
                    return true;
                }
            }
            // 其他情况正常执行
            return true;
        }
    }

    // 补丁Planet的GetAtmosphericDensity方法
    [HarmonyPatch(typeof(SFS.WorldBase.Planet))]
    public class PlanetAtmosphericDensityPatch
    {
        [HarmonyPatch("GetAtmosphericDensity")]
        [HarmonyPostfix]
        static void Postfix(SFS.WorldBase.Planet __instance, double height, ref double __result)
        {
            // 检查当前控制的火箭是否在水中
            if (PlayerController.main?.player?.Value?.location?.planet?.Value == __instance)
            {
                var rocket = PlayerController.main.player.Value;
                if (rocket != null && rocket.location != null)
                {
                    // 检查火箭是否有任何部件在水中
                    WaterBuoyancySystem buoyancySystem = WaterBuoyancySystem.main;
                if (rocket is Rocket rocketObj && buoyancySystem != null && buoyancySystem.HasAnyPartInWater(rocketObj, rocket.location))
                    {
                        // 使用Settings中的密度乘数
                        float waterDensityMultiplier = WaterSettingsManager.settings.waterDensityMultiplier;
                        
                        // 修改结果
                        __result = __result * waterDensityMultiplier;
                        
                        if (WaterSettingsManager.settings.enableDebugLogs)
                        {
                            Debug.Log($"[WaterMod] Modified atmospheric density for water: {waterDensityMultiplier}x, new density: {__result}");
                        }
                    }
                }
            }
        }
    }

    // 补丁AeroModule的ApplyForce方法，让水下时空气动力部件不参与阻力计算
    [HarmonyPatch(typeof(AeroModule))]
    public class AeroModuleForcePatch
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            // 用反射找到ApplyForce方法
            return typeof(AeroModule).GetMethod("ApplyForce", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        }

        [HarmonyPrefix]
        static bool Prefix(AeroModule __instance, List<Surface> exposedSurfaces, Location location, Matrix2x2 localToWorld, out float g_ForSound)
        {
            // 检查是否在水中
            if (__instance is Aero_Rocket aeroRocket && aeroRocket.rocket?.location?.Value != null)
            {
                WaterBuoyancySystem buoyancySystem = WaterBuoyancySystem.main;
                if (buoyancySystem != null && buoyancySystem.HasAnyPartInWater(aeroRocket.rocket, aeroRocket.rocket.location))
                {
                    // 在水中时，跳过空气动力部件的阻力计算
                    g_ForSound = 0f;
                    return false; // 不执行原始方法
                }
            }
            
            // 不在水中时正常执行
            g_ForSound = 0f; // 这个值会在原始方法中被正确设置
            return true;
        }
    }

    // 多线程补丁AeroModule再入计算
    [HarmonyPatch(typeof(AeroModule), "FixedUpdate")]
    public class AeroModuleReentryThreadPatch
    {
        private static List<AeroModule> modules = new List<AeroModule>();
        private static int lastFrame = -1;
        private static int counter = 0;
        private static object locker = new object();

        // Prefix: 收集AeroModule，不做重计算
        static bool Prefix(AeroModule __instance)
        {
            int frame = Time.frameCount;
            lock (locker)
            {
                if (lastFrame != frame)
                {
                    modules.Clear();
                    counter = 0;
                    lastFrame = frame;
                }
                modules.Add(__instance);
                counter++;
            }
            return false; // 跳过原始FixedUpdate
        }

        // Harmony Finalizer: 本帧最后一个AeroModule时，批量多线程处理
        [HarmonyFinalizer]
        static Exception Finalizer(AeroModule __instance)
        {
            int frame = Time.frameCount;
            bool isLast = false;
            lock (locker)
            {
                if (counter == modules.Count)
                {
                    isLast = true;
                }
            }
            if (isLast)
            {
                int threadCount = Math.Max(Environment.ProcessorCount - 1, 1);
                int total = modules.Count;
                var tasks = new List<Task>();
                for (int t = 0; t < threadCount; t++)
                {
                    int start = t * total / threadCount;
                    int end = (t + 1) * total / threadCount;
                    tasks.Add(Task.Run(() =>
                    {
                        for (int i = start; i < end; i++)
                        {
                            try
                            {
                                AeroModule module = modules[i];
                                // 复制AeroModule.FixedUpdate的再入相关重计算逻辑
                                int frameIndex = module.GetType().GetField("frameIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(module) is int fi ? fi + 1 : 0;
                                module.GetType().GetField("frameIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(module, frameIndex);
                                bool flag = false;
                                Location location = (Location)module.GetType().GetMethod("GetLocation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).Invoke(module, null);
                                if ((bool)typeof(AeroModule).GetMethod("IsInsideAtmosphereAndIsMoving", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { location }))
                                {
                                    float num, num2, num3;
                                    object[] tempArgs = new object[] { location, null, null, null };
                                    typeof(AeroModule).GetMethod("GetTemperatureAndShockwave", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Invoke(null, tempArgs);
                                    num = (float)tempArgs[1];
                                    num2 = (float)tempArgs[2];
                                    num3 = (float)tempArgs[3];
                                    bool flag2 = (bool)module.GetType().GetProperty("PhysicsMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).GetValue(module) && !SandboxSettings.main.settings.noAtmosphericDrag;
                                    bool flag3 = num3 > 0f;
                                    float num4 = 0f;
                                    if (flag2 || flag3)
                                    {
                                        float num5 = (float)location.velocity.AngleRadians - 1.5707964f;
                                        Matrix2x2 rotate = Matrix2x2.Angle(-num5);
                                        Matrix2x2 localToWorld = Matrix2x2.Angle(num5);
                                        List<Surface> exposedSurfaces = (List<Surface>)typeof(AeroModule).GetMethod("GetExposedSurfaces", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).Invoke(null, new object[] { module.GetType().GetMethod("GetDragSurfaces", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).Invoke(module, new object[] { rotate }) });
                                        if (flag2)
                                        {
                                            module.GetType().GetMethod("ApplyForce", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(module, new object[] { exposedSurfaces, location, localToWorld, num4 });
                                        }
                                        if (flag3)
                                        {
                                            object[] reentryArgs = new object[] { num3, exposedSurfaces, num5, localToWorld, flag };
                                            module.GetType().GetMethod("FixedUpdate_Reentry_And_Heating", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(module, reentryArgs);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"[AeroModuleReentryThreadPatch] Exception: {ex}");
                            }
                        }
                    }));
                }
                Task.WaitAll(tasks.ToArray());
                lock (locker)
                {
                    modules.Clear();
                    counter = 0;
                }
            }
            return null;
        }
    }
}
