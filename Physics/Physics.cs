using System;
using System.Collections.Generic;
using UnityEngine;
using SFS.Parts;
using SFS.Parts.Modules;
using SFS.World;
using SFS.World.Drag;
using SFS.IO;
using SFS.Parsers.Json;

namespace WaterMod
{
    public class WaterBuoyancySystem : MonoBehaviour
    {
        private static WaterBuoyancySystem _main;
        
        // 海平面高度和水密度
        private double seaLevelHeight;
        private double waterDensity;

        public static WaterBuoyancySystem main
        {
            get
            {
                if (_main == null)
                {
                    GameObject go = new GameObject("WaterBuoyancySystem");
                    _main = go.AddComponent<WaterBuoyancySystem>();
                    GameObject.DontDestroyOnLoad(go);
                }
                return _main;
            }
        }

        // 获取重力加速度
        private float GetGravity(WorldLocation location)
        {
            if (location?.planet?.Value == null) return 9.8f;
            return (float)location.planet.Value.data.basics.gravity;
        }

        private void Awake()
        {
            _main = this;
            LoadBuoyancySettings();
        }

        private void LoadBuoyancySettings()
        {
            // 每次进入世界时重新加载设置文件
            WaterSettingsManager.Load();
            
            // 从当前行星的水数据获取参数（优先）
            string currentPlanet = WaterManager.GetCurrentPlanetName();
            if (!string.IsNullOrEmpty(currentPlanet))
            {
                WaterData waterData = WaterManager.GetPlanetWater(currentPlanet);
                if (waterData != null)
                {
                    // 优先从 WATER_DATA 读取参数
                    seaLevelHeight = waterData.seaLevelHeight;
                    waterDensity = waterData.waterDensity;
                    
                    if (WaterSettingsManager.settings.enableDebugLogs)
                    {
                        Debug.Log($"[WaterMod] Loading buoyancy settings...");
                        Debug.Log($"[WaterMod] Default sea level height: {WaterSettingsManager.settings.defaultSeaLevelHeight}");
                        Debug.Log($"[WaterMod] Default water density: {WaterSettingsManager.settings.defaultWaterDensity}");
                        Debug.Log($"[WaterMod] Current planet: {currentPlanet}");
                        Debug.Log($"[WaterMod] Using WATER_DATA values:");
                        Debug.Log($"[WaterMod] Sea level height: {seaLevelHeight}");
                        Debug.Log($"[WaterMod] Water density: {waterDensity}");
                    }
                }
                else
                {
                    // WaterData 不存在，从配置文件获取默认值
                    seaLevelHeight = WaterSettingsManager.settings.defaultSeaLevelHeight;
                    waterDensity = WaterSettingsManager.settings.defaultWaterDensity;
                    
                    if (WaterSettingsManager.settings.enableDebugLogs)
                    {
                        Debug.Log($"[WaterMod] Loading buoyancy settings...");
                        Debug.Log($"[WaterMod] Default sea level height: {WaterSettingsManager.settings.defaultSeaLevelHeight}");
                        Debug.Log($"[WaterMod] Default water density: {WaterSettingsManager.settings.defaultWaterDensity}");
                        Debug.Log($"[WaterMod] Current planet: {currentPlanet}");
                        Debug.Log($"[WaterMod] No WATER_DATA found, using default values:");
                        Debug.Log($"[WaterMod] Sea level height: {seaLevelHeight}");
                        Debug.Log($"[WaterMod] Water density: {waterDensity}");
                    }
                }
            }
            else
            {
                // 没有当前行星，从配置文件获取默认值
                seaLevelHeight = WaterSettingsManager.settings.defaultSeaLevelHeight;
                waterDensity = WaterSettingsManager.settings.defaultWaterDensity;
                
                if (WaterSettingsManager.settings.enableDebugLogs)
                {
                    Debug.Log($"[WaterMod] Loading buoyancy settings...");
                    Debug.Log($"[WaterMod] Default sea level height: {WaterSettingsManager.settings.defaultSeaLevelHeight}");
                    Debug.Log($"[WaterMod] Default water density: {WaterSettingsManager.settings.defaultWaterDensity}");
                    Debug.Log($"[WaterMod] No current planet, using default values:");
                    Debug.Log($"[WaterMod] Sea level height: {seaLevelHeight}");
                    Debug.Log($"[WaterMod] Water density: {waterDensity}");
                }
            }
        }

        
        public Vector2 CalculateBuoyancyForce(Part part, WorldLocation location)
        {
            if (part == null || location == null) return Vector2.zero;

            // 获取部件体积和质量
            float partVolume = GetPartVolume(part);
            float partMass = part.mass.Value;
            
            // 计算部件在水中的深度比例（基于部件实际位置）
            float partUnderwaterRatio = GetPartUnderwaterRatio(part, location);
            
            // 如果部件完全在水上，返回零浮力
            if (partUnderwaterRatio <= 0f) return Vector2.zero;
            
            // 计算浮力：F = ρ × g × V × ratio
            float gravity = GetGravity(location);
            float buoyancyForce = (float)waterDensity * gravity * partVolume * partUnderwaterRatio;
            
            // 应用浮力系数
            float buoyancyMultiplier = GetBuoyancyMultiplier(part);
            buoyancyForce *= buoyancyMultiplier * WaterSettingsManager.settings.globalBuoyancyMultiplier;
            
            // 浮力方向向上
            Vector2 buoyancyVector = Vector2.up * buoyancyForce;
            
            if (WaterSettingsManager.settings.enableDebugLogs)
            {
                Debug.Log($"[WaterMod] Buoyancy calculation for part {part.name}:");
                Debug.Log($"[WaterMod] Volume: {partVolume}m³");
                Debug.Log($"[WaterMod] Mass: {partMass}kg");
                Debug.Log($"[WaterMod] Underwater ratio: {partUnderwaterRatio}");
                Debug.Log($"[WaterMod] Buoyancy force: {buoyancyForce}N");
                Debug.Log($"[WaterMod] Buoyancy multiplier: {buoyancyMultiplier}");
            }
            
            return buoyancyVector;
        }
        
        private float GetBuoyancyMultiplier(Part part)
        {
            if (part == null) return WaterSettingsManager.settings.defaultBuoyancyIndex;
            
            // 根据部件类型返回不同的浮力系数
            var engineModules = part.GetModules<EngineModule>();
            if (engineModules != null && engineModules.Length > 0) return WaterSettingsManager.settings.engineBuoyancyIndex;
            
            var boosterModules = part.GetModules<BoosterModule>();
            if (boosterModules != null && boosterModules.Length > 0) return WaterSettingsManager.settings.boosterBuoyancyIndex;
            
            var resourceModules = part.GetModules<ResourceModule>();
            if (resourceModules != null && resourceModules.Length > 0) return WaterSettingsManager.settings.resourceBuoyancyIndex;
            
            var aeroModules = part.GetModules<AeroModule>();
            if (aeroModules != null && aeroModules.Length > 0) return WaterSettingsManager.settings.aeroBuoyancyIndex;
            
            var splitModules = part.GetModules<SplitModule>();
            if (splitModules != null && splitModules.Length > 0)
            {
                // 检查是否是整流罩
                foreach (var splitModule in splitModules)
                {
                    if (splitModule.fairing)
                    {
                        return WaterSettingsManager.settings.fairingBuoyancyIndex; // 整流罩使用专用浮力系数
                    }
                }
                return WaterSettingsManager.settings.separatorBuoyancyIndex; // 普通分离器
            }
            
            var dockingPortModules = part.GetModules<DockingPortModule>();
            if (dockingPortModules != null && dockingPortModules.Length > 0) return WaterSettingsManager.settings.dockingPortBuoyancyIndex;
            
            var rcsModules = part.GetModules<RcsModule>();
            if (rcsModules != null && rcsModules.Length > 0) return WaterSettingsManager.settings.rcsBuoyancyIndex;
            
            var wheelModules = part.GetModules<WheelModule>();
            if (wheelModules != null && wheelModules.Length > 0) return WaterSettingsManager.settings.wheelBuoyancyIndex;
            
            var crewModules = part.GetModules<CrewModule>();
            if (crewModules != null && crewModules.Length > 0) return WaterSettingsManager.settings.crewBuoyancyIndex;
            
            var toggleModules = part.GetModules<ToggleModule>();
            if (toggleModules != null && toggleModules.Length > 0) return WaterSettingsManager.settings.toggleBuoyancyIndex;
            
            var lesModules = part.GetModules<LES_Module>();
            if (lesModules != null && lesModules.Length > 0) return WaterSettingsManager.settings.lesBuoyancyIndex;
            
            var particleModules = part.GetModules<ParticleModule>();
            if (particleModules != null && particleModules.Length > 0) return WaterSettingsManager.settings.particleBuoyancyIndex;
            
            var interiorModules = part.GetModules<InteriorModule>();
            if (interiorModules != null && interiorModules.Length > 0) return WaterSettingsManager.settings.interiorBuoyancyIndex;
            
            var activeModules = part.GetModules<ActiveModule>();
            if (activeModules != null && activeModules.Length > 0) return WaterSettingsManager.settings.activeBuoyancyIndex;
            
            // 未知部件使用默认浮力系数
            return WaterSettingsManager.settings.defaultBuoyancyIndex;
        }


        
        // 计算平移阻力（只有形状阻力、阻尼力、部件间干扰阻力）
        public Vector2 CalculateLinearDrag(Rocket rocket, WorldLocation location)
        {
            if (rocket?.rb2d == null) return Vector2.zero;
            
            // 检查该星球是否有水
            string planetName = location?.planet?.Value?.codeName;
            if (string.IsNullOrEmpty(planetName))
            {
                return Vector2.zero; // 无法获取星球名称，返回0
            }
            
            // 检查该星球是否有水数据
            WaterData waterData = WaterManager.GetPlanetWater(planetName);
            if (waterData == null || waterData.seaLevelHeight <= 0)
            {
                return Vector2.zero; // 没有水数据，返回0
            }
            
            Vector2 velocity = rocket.rb2d.velocity;
            float velocityMagnitude = velocity.magnitude;
            
            if (velocityMagnitude < 0.1f) return Vector2.zero;
            
            Vector2 dragDirection = -velocity.normalized;
            Vector2 totalDrag = Vector2.zero;
            
            // 1. 形状阻力
            Vector2 formDrag = CalculateFormDrag(rocket, location, velocity);
            
            // 2. 阻尼力
            Vector2 dampingForce = CalculateDampingForce(rocket, location, velocity);
            
            // 3. 部件间干扰阻力
            Vector2 interferenceDrag = CalculateInterferenceDrag(rocket, location, velocity);
            
            totalDrag = formDrag + dampingForce + interferenceDrag;
            
            if (WaterSettingsManager.settings != null && WaterSettingsManager.settings.enableDebugLogs)
            {
                Debug.Log($"[WaterMod] Linear drag - Form: {formDrag.magnitude:F2}, Damping: {dampingForce.magnitude:F2}, Interference: {interferenceDrag.magnitude:F2}");
            }
            
            return totalDrag;
        }

        // 计算形状阻力
        private Vector2 CalculateFormDrag(Rocket rocket, WorldLocation location, Vector2 velocity)
        {
            float velocityMagnitude = velocity.magnitude;
            Vector2 dragDirection = -velocity.normalized;
            
            // 获取当前星球的水密度
            string planetName = location?.planet?.Value?.codeName;
            double currentWaterDensity = waterDensity; // 默认值
            if (!string.IsNullOrEmpty(planetName))
            {
                WaterData waterData = WaterManager.GetPlanetWater(planetName);
                if (waterData != null && waterData.waterDensity > 0)
                {
                    currentWaterDensity = waterData.waterDensity;
                }
            }
            
            float totalFormDrag = 0f;
            
            foreach (Part part in rocket.partHolder.parts)
            {
                if (part == null) continue;
                
                // 检查是否是空气动力部件
                var aeroModules = part.GetModules<AeroModule>();
                if (aeroModules != null && aeroModules.Length > 0)
                {
                    // 空气动力部件不计算形状阻力
                    continue;
                }
                
                // 计算部件的形状阻力
                float frontalArea = CalculateFrontalArea(part, velocity);
                float reynoldsNumber = CalculateReynoldsNumber(velocityMagnitude, GetCharacteristicLength(rocket));
                float dragCoefficient = GetDragCoefficientByReynolds(reynoldsNumber);
                float shapeModifier = GetShapeDragModifier(part);
                
                float partFormDrag = 0.5f * (float)currentWaterDensity * velocityMagnitude * velocityMagnitude * 
                                   frontalArea * dragCoefficient * shapeModifier * 
                                   WaterSettingsManager.settings.formDragCoefficient;
                
                totalFormDrag += partFormDrag;
            }
            
            return dragDirection * totalFormDrag;
        }

        // 计算阻尼力
        private Vector2 CalculateDampingForce(Rocket rocket, WorldLocation location, Vector2 velocity)
        {
            float velocityMagnitude = velocity.magnitude;
            Vector2 dampingDirection = -velocity.normalized;
            
            float totalDamping = 0f;
            
            foreach (Part part in rocket.partHolder.parts)
            {
                if (part == null) continue;

                // 检查是否是空气动力部件
                var aeroModules = part.GetModules<AeroModule>();
                if (aeroModules != null && aeroModules.Length > 0)
                {
                    // 空气动力部件不计算阻尼力
                    continue;
                }
                
                // 计算部件的阻尼力
                float partVolume = GetPartVolume(part);
                float underwaterRatio = GetPartUnderwaterRatio(part, location);
                
                float partDamping = velocityMagnitude * partVolume * underwaterRatio * 
                                  WaterSettingsManager.settings.dampingCoefficient;
                
                totalDamping += partDamping;
            }
            
            // 限制最大阻尼力
            totalDamping = Mathf.Min(totalDamping, WaterSettingsManager.settings.maxDampingForce);
            
            return dampingDirection * totalDamping;
        }

        // 计算部件间干扰阻力
        private Vector2 CalculateInterferenceDrag(Rocket rocket, WorldLocation location, Vector2 velocity)
        {
            float velocityMagnitude = velocity.magnitude;
            Vector2 dragDirection = -velocity.normalized;
            
            float totalInterferenceDrag = 0f;
            
            Part[] parts = rocket.partHolder.parts.ToArray();
            
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                
                // 检查是否是空气动力部件
                var aeroModules = parts[i].GetModules<AeroModule>();
                if (aeroModules != null && aeroModules.Length > 0)
                {
                    // 空气动力部件不计算干扰阻力
                    continue;
                }
                
                for (int j = i + 1; j < parts.Length; j++)
                {
                    if (parts[j] == null) continue;
                    
                    // 检查是否是空气动力部件
                    var aeroModules2 = parts[j].GetModules<AeroModule>();
                    if (aeroModules2 != null && aeroModules2.Length > 0)
                    {
                        // 空气动力部件不计算干扰阻力
                        continue;
                    }
                    
                    Vector2 interference = CalculatePartInterference(parts[i], parts[j], location, velocity);
                    totalInterferenceDrag += interference.magnitude;
                }
            }
            
            return dragDirection * totalInterferenceDrag * WaterSettingsManager.settings.interferenceDragCoefficient;
        }

        // 计算角阻力（所有部件都计算）
        public float CalculateAngularDrag(Rocket rocket, WorldLocation location)
        {
            if (rocket?.rb2d == null) return 0f;
            
            float angularVelocity = rocket.rb2d.angularVelocity;
            if (Mathf.Abs(angularVelocity) < 0.1f) return 0f;
            
            float totalAngularDrag = 0f;
            
            foreach (Part part in rocket.partHolder.parts)
            {
                if (part == null) continue;
                
                // 所有部件都计算角阻力，但系数不同
                float angularDragCoefficient = WaterSettingsManager.settings.angularDragCoefficient;
                
                // 检查是否是空气动力部件
                var aeroModules = part.GetModules<AeroModule>();
                if (aeroModules != null && aeroModules.Length > 0)
                {
                    // 空气动力部件使用不同的角阻力系数
                    angularDragCoefficient = WaterSettingsManager.settings.aeroAngularDragCoefficient;
                }
                
                float partVolume = GetPartVolume(part);
                float underwaterRatio = GetPartUnderwaterRatio(part, location);
                float distanceFromCenter = Vector2.Distance(part.transform.position, rocket.transform.position);
                
                float partAngularDrag = Mathf.Abs(angularVelocity) * partVolume * underwaterRatio * 
                                      distanceFromCenter * angularDragCoefficient;
                
                totalAngularDrag += partAngularDrag;
            }
            
            // 限制最大角阻力
            totalAngularDrag = Mathf.Min(totalAngularDrag, WaterSettingsManager.settings.maxAngularDrag);
            
            return -Mathf.Sign(angularVelocity) * totalAngularDrag;
        }

        // 计算浮力矩（所有部件都计算）
        public float CalculateBuoyancyTorque(Rocket rocket, WorldLocation location)
        {
            if (rocket?.rb2d == null) return 0f;
            
            float totalBuoyancyTorque = 0f;
            Vector2 rocketCenter = rocket.rb2d.worldCenterOfMass;
            
            foreach (Part part in rocket.partHolder.parts)
            {
                if (part == null) continue;
                
                Vector2 buoyancyForce = CalculateBuoyancyForce(part, location);
                if (buoyancyForce.magnitude < 0.01f) continue;
                
                Vector2 partPosition = part.transform.position;
                Vector2 relativePosition = partPosition - rocketCenter;
                
                // 计算力矩：τ = r × F (2D叉积)
                // 浮力向上，所以只考虑x方向的偏移产生的力矩
                float torque = relativePosition.x * buoyancyForce.y;
                
                // 添加稳定性修正：让船倾向于保持水平
                // 如果右侧浮力更大，应该产生负力矩（顺时针）来平衡
                if (relativePosition.x > 0f && buoyancyForce.y > 0f)
                {
                    torque = -torque; // 反转力矩方向
                }
                
                // 检查是否是空气动力部件
                var aeroModules = part.GetModules<AeroModule>();
                if (aeroModules != null && aeroModules.Length > 0)
                {
                    // 空气动力部件使用不同的浮力矩系数
                    torque *= WaterSettingsManager.settings.aeroBuoyancyTorqueCoefficient;
                }
                else
                {
                    // 普通部件使用全局浮力矩系数
                    torque *= WaterSettingsManager.settings.globalBuoyancyTorqueCoefficient;
                }
                
                totalBuoyancyTorque += torque;
            }
            
            return totalBuoyancyTorque;
        }


        // 检查是否在水中
        public bool IsInWater(WorldLocation location)
        {
            if (location?.planet?.Value == null) return false;
            
            // 获取星球名称
            string planetName = location.planet.Value.codeName;
            
            // 直接检查该星球是否有水数据
            WaterData waterData = WaterManager.GetPlanetWater(planetName);
            if (waterData == null || waterData.seaLevelHeight <= 0)
            {
                // 如果没有水数据或海平面高度为0，返回false
                return false;
            }
            
            // 获取当前高度
            double currentHeight = location.Value.Height;
            
            // 如果当前高度低于海平面，则在水下
            return currentHeight < waterData.seaLevelHeight;
        }
        
        // 检查火箭是否有任何部件在水中
        public bool HasAnyPartInWater(Rocket rocket, WorldLocation location)
        {
            if (rocket?.partHolder?.parts == null || location == null) return false;
            
            foreach (Part part in rocket.partHolder.parts)
            {
                if (part == null) continue;
                
                float underwaterRatio = GetPartUnderwaterRatio(part, location);
                if (underwaterRatio > 0f)
                {
                    return true;
                }
            }
            
            return false;
        }

        // 获取部件体积
        private float GetPartVolume(Part part)
        {
            if (part == null) return 0f;
            
            // 获取部件的多边形数据
            var polygonModules = part.GetModules<PolygonData>();
            float totalVolume = 0f;
            
            foreach (var polygonData in polygonModules)
            {
                if (polygonData.BuildCollider)
                {
                    float area = CalculatePolygonArea(polygonData.polygon.vertices);
                    float height = GetPartHeight(part);
                    totalVolume += area * height;
                }
            }
            
            // 如果没有找到体积，尝试从碰撞器获取
            if (totalVolume <= 0f)
            {
                Collider2D collider = part.GetComponent<Collider2D>();
                if (collider != null)
                {
                    // 对于BoxCollider2D
                    if (collider is BoxCollider2D boxCollider)
                    {
                        totalVolume = boxCollider.size.x * boxCollider.size.y * GetPartHeight(part);
                    }
                    // 对于CircleCollider2D
                    else if (collider is CircleCollider2D circleCollider)
                    {
                        float radius = circleCollider.radius;
                        totalVolume = Mathf.PI * radius * radius * GetPartHeight(part);
                    }
                    // 对于其他类型的碰撞器
                    else
                    {
                        // 使用碰撞器的边界框
                        Bounds bounds = collider.bounds;
                        totalVolume = bounds.size.x * bounds.size.y * bounds.size.z;
                    }
                }
                else
                {
                    // 最后的备选方案：使用部件的变换尺寸
                    Vector3 partScale = part.transform.localScale;
                    totalVolume = Mathf.Max(partScale.x * partScale.y * partScale.z, 0.1f);
                }
            }
            
            return totalVolume;
        }
        
        // 获取部件高度
        private float GetPartHeight(Part part)
        {
            if (part == null) return 1f;

            // 获取部件的多边形数据
            var polygonModules = part.GetModules<PolygonData>();
            float maxHeight = 1f;

            foreach (var polygonData in polygonModules)
            {
                if (polygonData.BuildCollider)
                {
                    Vector2[] vertices = polygonData.polygon.vertices;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        maxHeight = Mathf.Max(maxHeight, Mathf.Abs(vertices[i].y));
                    }
                }
            }

            return maxHeight * 2f; // 高度是y坐标范围的两倍
        }

        // 计算多边形面积
        private float CalculatePolygonArea(Vector2[] vertices)
        {
            if (vertices == null || vertices.Length < 3) return 0f;
            
            float area = 0f;
            for (int i = 0; i < vertices.Length; i++)
            {
                int j = (i + 1) % vertices.Length;
                area += vertices[i].x * vertices[j].y;
                area -= vertices[j].x * vertices[i].y;
            }
            
            return Mathf.Abs(area) * 0.5f;
        }

        // 计算多边形周长
        private float CalculatePolygonPerimeter(Vector2[] vertices)
        {
            if (vertices == null || vertices.Length < 2) return 0f;
            
            float perimeter = 0f;
            for (int i = 0; i < vertices.Length; i++)
            {
                int j = (i + 1) % vertices.Length;
                perimeter += Vector2.Distance(vertices[i], vertices[j]);
            }
            
            return perimeter;
        }

        
        public void SetSeaLevelHeight(double height)
        {
            seaLevelHeight = height;
        }
        
        public void SetWaterDensity(double density)
        {
            waterDensity = density;
        }
        
        public double GetWaterDensity()
        {
            // 获取当前星球名称
            string currentPlanet = WaterManager.GetCurrentPlanetName();
            if (string.IsNullOrEmpty(currentPlanet))
            {
                return waterDensity; // 如果无法获取星球名称，返回默认值
            }
            
            // 获取该星球的水数据
            WaterData waterData = WaterManager.GetPlanetWater(currentPlanet);
            if (waterData != null && waterData.waterDensity > 0)
            {
                return waterData.waterDensity; // 返回该星球的水密度
            }
            
            return waterDensity; // 如果没有配置，返回默认值
        }
        
        public void ReloadBuoyancySettings()
        {
            LoadBuoyancySettings();
        }
        
        public Dictionary<string, float> GetBuoyancyConfig()
        {
            return new Dictionary<string, float>
            {
                ["globalBuoyancyMultiplier"] = WaterSettingsManager.settings.globalBuoyancyMultiplier,
                ["defaultBuoyancyIndex"] = WaterSettingsManager.settings.defaultBuoyancyIndex,
                ["waterDensity"] = (float)waterDensity,
                ["seaLevelHeight"] = (float)seaLevelHeight
            };
        }

        // 计算迎风面积
        private float CalculateFrontalArea(Part part, Vector2 velocity)
        {
            if (part == null || velocity.magnitude < 0.1f) return 0f;
            
            // 获取部件的碰撞器
            Collider2D collider = part.GetComponent<Collider2D>();
            if (collider == null) return 0f;
            
            // 计算迎风面积
            float frontalArea = 0f;
            
            if (collider is BoxCollider2D boxCollider)
            {
                Vector2 size = boxCollider.size;
                // 简化的迎风面积计算
                frontalArea = size.x * size.y * 0.5f;
            }
            else if (collider is CircleCollider2D circleCollider)
            {
                float radius = circleCollider.radius;
                frontalArea = Mathf.PI * radius * radius;
                    }
                    else
                    {
                // 使用边界框
                Bounds bounds = collider.bounds;
                frontalArea = bounds.size.x * bounds.size.y;
            }
            
            return frontalArea;
        }

        // 计算雷诺数
        private float CalculateReynoldsNumber(float velocity, float characteristicLength)
        {
            if (characteristicLength <= 0f) return 0f;
            
            // 水的运动粘度约为1e-6 m²/s
            float kinematicViscosity = 1e-6f;
            
            return velocity * characteristicLength / kinematicViscosity;
        }

        // 获取特征长度
        private float GetCharacteristicLength(Rocket rocket)
        {
            if (rocket?.partHolder?.parts == null) return 1f;
            
            float maxLength = 0f;
            foreach (Part part in rocket.partHolder.parts)
            {
                if (part != null)
                {
                    Vector3 partScale = part.transform.localScale;
                    maxLength = Mathf.Max(maxLength, partScale.magnitude);
                }
            }
            
            return Mathf.Max(maxLength, 1f);
        }

        // 根据雷诺数获取阻力系数
        private float GetDragCoefficientByReynolds(float reynoldsNumber)
        {
            if (reynoldsNumber < WaterSettingsManager.settings.transitionReynoldsNumber)
            {
                return WaterSettingsManager.settings.laminarDragCoefficient;
            }
            else
            {
                return WaterSettingsManager.settings.turbulentDragCoefficient;
            }
        }

        // 获取形状阻力修正系数
        private float GetShapeDragModifier(Part part)
        {
            // 根据部件类型返回不同的形状修正系数
            var aeroModules = part.GetModules<AeroModule>();
            if (aeroModules != null && aeroModules.Length > 0) return WaterSettingsManager.settings.streamlinedDragCoefficient;
            
            // 其他部件使用默认系数
            return 1.0f;
        }

        // 获取部件水下深度比例
        public float GetPartUnderwaterRatio(Part part, WorldLocation location)
        {
            if (part == null || location == null) return 0f;
            
            // 检查该星球是否有水数据
            string planetName = location.planet?.Value?.codeName;
            if (string.IsNullOrEmpty(planetName))
            {
                return 0f; // 无法获取星球名称，返回0
            }
            
            // 直接检查该星球是否有水数据
            WaterData waterData = WaterManager.GetPlanetWater(planetName);
            if (waterData == null || waterData.seaLevelHeight <= 0)
            {
                return 0f; // 没有水数据，返回0
            }
            
            // 获取部件在火箭中的相对位置
            Vector3 partLocalPosition = part.transform.localPosition;
            
            // 计算部件底部相对于火箭中心的高度
            float partHeight = GetPartHeight(part);
            float partBottomOffset = partLocalPosition.y - partHeight * 0.5f;
            
            // 计算部件底部在世界中的绝对高度
            float partBottomWorldHeight = (float)(location.Value.Height + partBottomOffset);
            
            // 计算部件在水中的深度
            float partUnderwaterDepth = Mathf.Max(0f, (float)waterData.seaLevelHeight - partBottomWorldHeight);
            
            // 计算水下比例
            float underwaterRatio = Mathf.Clamp01(partUnderwaterDepth / partHeight);
            
            if (WaterSettingsManager.settings.enableDebugLogs)
            {
                Debug.Log($"[WaterMod] Part {part.name} underwater calculation:");
                Debug.Log($"[WaterMod] Part local position: {partLocalPosition}");
                Debug.Log($"[WaterMod] Part height: {partHeight}");
                Debug.Log($"[WaterMod] Part bottom offset: {partBottomOffset}");
                Debug.Log($"[WaterMod] Part bottom world height: {partBottomWorldHeight}");
                Debug.Log($"[WaterMod] Sea level height: {waterData.seaLevelHeight}");
                Debug.Log($"[WaterMod] Part underwater depth: {partUnderwaterDepth}");
                Debug.Log($"[WaterMod] Underwater ratio: {underwaterRatio}");
            }
            
            return underwaterRatio;
        }

        // 计算部件间干扰
        private Vector2 CalculatePartInterference(Part part1, Part part2, WorldLocation location, Vector2 velocity)
        {
            if (part1 == null || part2 == null) return Vector2.zero;
            
            // 计算部件间距离
            Vector3 pos1 = part1.transform.position;
            Vector3 pos2 = part2.transform.position;
            float distance = Vector3.Distance(pos1, pos2);
            
            // 如果距离太远，没有干扰
            if (distance > 10f) return Vector2.zero;
            
            // 简化的干扰计算
            float interferenceFactor = 1f / (1f + distance);
            float interferenceForce = velocity.magnitude * interferenceFactor * 
                             WaterSettingsManager.settings.wakeEffectFactor;
            
            return -velocity.normalized * interferenceForce;
        }

        // 计算浮力与重力的合力
        public Vector2 CalculateNetForce(Rocket rocket, WorldLocation location)
        {
            if (rocket?.partHolder?.parts == null) return Vector2.zero;
            
            // 1. 计算总浮力
            Vector2 totalBuoyancy = Vector2.zero;
            float totalMass = 0f;
            
            foreach (Part part in rocket.partHolder.parts)
            {
                if (part == null) continue;
                
                Vector2 partBuoyancy = CalculateBuoyancyForce(part, location);
                totalBuoyancy += partBuoyancy;
                totalMass += part.mass.Value;
            }
            
            // 2. 计算重力
            float gravity = GetGravity(location);
            Vector2 totalWeight = Vector2.down * (totalMass * gravity);
            
            // 3. 计算合力：浮力 + 重力（重力向下为负）
            Vector2 netForce = totalBuoyancy + totalWeight;
            
            if (WaterSettingsManager.settings.enableDebugLogs)
            {
                Debug.Log($"[WaterMod] Net force - Buoyancy: {totalBuoyancy.magnitude:F2}N, Weight: {totalWeight.magnitude:F2}N, Net: {netForce.magnitude:F2}N");
            }
            
            return netForce;
        }

        // 修改主循环中的浮力应用
        public void ApplyWaterForces(Rocket rocket, WorldLocation location)
        {
            if (rocket?.rb2d == null) return;
            
            // 计算并应用净力（浮力 + 重力）
            Vector2 netForce = CalculateNetForce(rocket, location);
            rocket.rb2d.AddForce(netForce);
            
            // 计算并应用角阻力
            float angularDrag = CalculateAngularDrag(rocket, location);
            rocket.rb2d.AddTorque(angularDrag);
            
            // 计算并应用浮力矩
            float buoyancyTorque = CalculateBuoyancyTorque(rocket, location);
            rocket.rb2d.AddTorque(buoyancyTorque);
        }
    }
}