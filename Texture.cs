using HarmonyLib;
using UnityEngine;
using SFS.WorldBase;
using SFS.World;
using SFS;
using SFS.IO;
using System.Collections.Generic;
using System.Linq; 

namespace WaterMod
{
    [HarmonyPatch(typeof(Planet), "SetupData")]
    public class Planet_SetupData_Patch
    {
        [HarmonyPostfix]
        public static void SetupData_Postfix(Planet __instance, string codeName, PlanetData data, Shader terrainShader, Shader atmosphereShader, I_MsgLogger log)
        {
            // 检查是否有水域数据
            WaterData waterData = WaterManager.GetPlanetWater(codeName);
            if (waterData == null)
                return;

            // 创建水纹理
            Texture2D waterTexture = CreateWaterTexture(waterData, codeName);
            if (waterTexture != null)
            {
                // 将水纹理应用到星球的地形材质
                ApplyWaterTextureToPlanet(__instance, waterTexture, waterData);
            }
        }

        private static Texture2D CreateWaterTexture(WaterData waterData, string planetName)
        {
            try
            {
                // 读取噪声图（黑白遮罩）
                Texture2D maskTexture = LoadNoiseTexture(waterData.oceanMaskTexture);
                if (maskTexture == null)
                {
                    Debug.LogWarning($"[WaterMod] Could not load mask texture: {waterData.oceanMaskTexture}");
                    return null;
                }

                // 获取原始地形纹理
                Texture2D originalTerrainTexture = GetOriginalTerrainTexture(planetName);
                if (originalTerrainTexture == null)
                {
                    Debug.LogWarning($"[WaterMod] Could not get original terrain texture for planet: {planetName}");
                    return null;
                }

                // 创建混合纹理
                int width = maskTexture.width;
                int height = maskTexture.height;
                Texture2D waterTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);

                // 混合原始地形纹理和水效果
                Color[] pixels = new Color[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        // 归一化坐标
                        float u = (float)x / (float)(width - 1);
                        float v = (float)y / (float)(height - 1);

                        // 用双线性插值采样遮罩和地形
                        float maskValue = maskTexture.GetPixelBilinear(u, v).r;
                        Color terrainColor = originalTerrainTexture.GetPixelBilinear(u, v);

                        // 在水面灰度渐变带叠加Perlin噪声扰动，生成自然波纹
                        float waterEdgeStart = 0.45f, waterEdgeEnd = 0.55f;
                        if (maskValue > waterEdgeStart && maskValue < waterEdgeEnd)
                        {
                            float noise = Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.12f - 0.06f; // 频率和幅度可调
                            maskValue += noise;
                            maskValue = Mathf.Clamp01(maskValue);
                        }

                        // 根据遮罩值和WATER_DATA参数生成水颜色
                        Color waterColor = GenerateWaterColor(maskValue, waterData, x, y, width, height);

                        // 混合地形和水颜色
                        Color finalColor = BlendTerrainAndWater(terrainColor, waterColor, maskValue, waterData);
                        pixels[y * width + x] = finalColor;
                    }
                }

                waterTexture.SetPixels(pixels);
                waterTexture.Apply();
                waterTexture.name = $"{planetName}_WaterTexture";

                return waterTexture;
            }
            catch
            {
                Debug.LogError($"[WaterMod] Error creating water texture");
                return null;
            }
        }

        // 根据遮罩值和WATER_DATA参数生成水颜色
        private static Color GenerateWaterColor(float maskValue, WaterData waterData, int x, int y, int width, int height)
        {
            // 使用遮罩值来确定水的深度和类型
            float depth = maskValue;
            
            // 根据深度应用不同的颜色
            Color waterColor;
            
            if (depth < 0.2f)
            {
                // 沙滩区域
                waterColor = waterData.sand;
            }
            else if (depth < 0.4f)
            {
                // 浅水区域
                float t = (depth - 0.2f) / 0.2f;
                waterColor = Color.Lerp(waterData.sand, waterData.shallow, t);
            }
            else if (depth < 0.7f)
            {
                // 中等深度水域
                float t = (depth - 0.4f) / 0.3f;
                waterColor = Color.Lerp(waterData.shallow, waterData.deep, t);
            }
            else if (depth < 0.9f)
            {
                // 深水区域
                float t = (depth - 0.7f) / 0.2f;
                waterColor = Color.Lerp(waterData.deep, waterData.floor, t);
            }
            else
            {
                // 海底区域
                waterColor = waterData.floor;
            }

            // 应用透明度设置
            float opacity = Mathf.Lerp(0.8f, 0.3f, depth); 
            waterColor.a = opacity;

            float noise = Mathf.PerlinNoise(x * 0.01f, y * 0.01f) * 0.1f;
            waterColor.r = Mathf.Clamp01(waterColor.r + noise);
            waterColor.g = Mathf.Clamp01(waterColor.g + noise);
            waterColor.b = Mathf.Clamp01(waterColor.b + noise);

            return waterColor;
        }

        // 加载噪声图
        public static Texture2D LoadNoiseTexture(string texturePath)
        {
            if (string.IsNullOrEmpty(texturePath))
                return null;

            try
            {
                // 输出所有已加载的纹理名
                Texture2D[] allTextures = UnityEngine.Resources.FindObjectsOfTypeAll<Texture2D>();

                foreach (Texture2D texture in allTextures)
                {
                    if (texture.name == texturePath || texture.name.Contains(texturePath))
                    {
                        return texture;
                    }
                }

                // 尝试从Resources加载
                Texture2D resourceTexture = UnityEngine.Resources.Load<Texture2D>($"Planet_Textures/{texturePath}");
                if (resourceTexture != null)
                {
                    return resourceTexture;
                }

                // 获取当前星系名
                string currentSolarSystem = WaterManager.GetCurrentSolarSystem();

                // 尝试不同的文件扩展名
                string[] extensions = { ".png", ".jpg", ".jpeg" };
                foreach (string ext in extensions)
                {
                    string fullTexturePath = texturePath + ext;
                    
                    // 使用当前星系名构建路径
                    FilePath textureFile;
                    if (!string.IsNullOrEmpty(currentSolarSystem))
                    {
                        textureFile = FileLocations.SolarSystemsFolder.Extend(currentSolarSystem).Extend("Texture Data").ExtendToFile(fullTexturePath);
                    }
                    else
                    {
                        textureFile = FileLocations.SolarSystemsFolder.Extend("Texture Data").ExtendToFile(fullTexturePath);
                    }

                    if (textureFile.FileExists())
                    {
                        Texture2D texture = TextureUtility.FromFile(textureFile, false);
                        if (texture != null)
                        {
                            texture.wrapMode = TextureWrapMode.Repeat;
                            texture.filterMode = FilterMode.Bilinear;
                            texture.name = texturePath;
                            return texture;
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        // 获取原始地形纹理
        private static Texture2D GetOriginalTerrainTexture(string planetName)
        {
            try
            {
                // 首先尝试从当前星球的terrainMaterial获取已经处理过的纹理
                Planet currentPlanet = FindPlanetByName(planetName);
                if (currentPlanet != null && currentPlanet.terrainMaterial != null)
                {
                    Texture2D processedTexture = currentPlanet.terrainMaterial.GetTexture("_PlanetTexture") as Texture2D;
                    if (processedTexture != null)
                    {
                        return processedTexture;
                    }
                }

                // 尝试从Resources加载
                Texture2D terrainTexture = UnityEngine.Resources.Load<Texture2D>(planetName);
                if (terrainTexture != null)
                {
                    return terrainTexture;
                }

                // 尝试从Planet_Textures文件夹加载
                terrainTexture = UnityEngine.Resources.Load<Texture2D>($"Planet_Textures/{planetName}");
                if (terrainTexture != null)
                {
                    return terrainTexture;
                }

                // 尝试从Textures文件夹加载
                terrainTexture = UnityEngine.Resources.Load<Texture2D>($"Textures/{planetName}");
                if (terrainTexture != null)
                {
                    return terrainTexture;
                }

                // 尝试从所有已加载的纹理中查找
                Texture2D[] allTextures = UnityEngine.Resources.FindObjectsOfTypeAll<Texture2D>();
                foreach (Texture2D texture in allTextures)
                {
                    if (texture.name.Equals(planetName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return texture;
                    }
                }

                // 尝试从Custom Solar Systems加载自定义纹理
                string currentSolarSystem = WaterManager.GetCurrentSolarSystem();
                if (!string.IsNullOrEmpty(currentSolarSystem))
                {
                    string[] extensions = { ".png", ".jpg", ".jpeg" };
                    foreach (string ext in extensions)
                    {
                        string fullTexturePath = planetName + ext;
                        FilePath textureFile = FileLocations.SolarSystemsFolder.Extend(currentSolarSystem).Extend("Texture Data").ExtendToFile(fullTexturePath);
                        
                        if (textureFile.FileExists())
                        {
                            Texture2D texture = TextureUtility.FromFile(textureFile, false);
                            if (texture != null)
                            {
                                texture.wrapMode = TextureWrapMode.Repeat;
                                texture.filterMode = FilterMode.Bilinear;
                                texture.name = planetName;
                                return texture;
                            }
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        // 根据星球名称查找星球对象
        private static Planet FindPlanetByName(string planetName)
        {
            try
            {
                if (SFS.Base.planetLoader != null && SFS.Base.planetLoader.planets != null)
                {
                    foreach (Planet planet in SFS.Base.planetLoader.planets.Values)
                    {
                        if (planet != null && planet.name.Equals(planetName, System.StringComparison.OrdinalIgnoreCase))
                        {
                            return planet;
                        }
                    }
                }
                return null;
            }
            catch
            {
                Debug.LogError($"[WaterMod] Error finding planet");
                return null;
            }
        }

        // 获取地形纹理在指定位置的颜色（已优化为双线性采样）
        private static Color GetTerrainColorAt(Texture2D terrainTexture, int x, int y, int targetWidth, int targetHeight)
        {
            if (terrainTexture == null)
                return Color.gray;

            float u = (float)x / (float)(targetWidth - 1);
            float v = (float)y / (float)(targetHeight - 1);
            return terrainTexture.GetPixelBilinear(u, v);
        }

        // 混合地形和水颜色（极宽沙滩区间，便于观察）
        private static Color BlendTerrainAndWater(Color terrainColor, Color waterColor, float maskValue, WaterData waterData)
        {
            float landEnd = 0.10f;
            float sandEnd = 0.70f; // 沙滩区间极宽
            float shallowEnd = 0.85f;

            if (maskValue < landEnd)
                return terrainColor;
            else if (maskValue < sandEnd)
                return Color.Lerp(terrainColor, waterData.sand, (maskValue - landEnd) / (sandEnd - landEnd));
            else if (maskValue < shallowEnd)
                return Color.Lerp(waterData.sand, waterData.shallow, (maskValue - sandEnd) / (shallowEnd - sandEnd));
            else
                return Color.Lerp(waterData.shallow, waterData.deep, (maskValue - shallowEnd) / (1f - shallowEnd));
        }

        private static void ApplyWaterTextureToPlanet(Planet planet, Texture2D waterTexture, WaterData waterData)
        {
            try
            {
                // 将水纹理直接应用到地表材质的主纹理
                if (planet.terrainMaterial != null)
                {
                    // 记录原始纹理
                    Texture2D originalTexture = planet.terrainMaterial.GetTexture("_PlanetTexture") as Texture2D;
                    // 设置SFS Terrain shader的_PlanetTexture属性
                    planet.terrainMaterial.SetTexture("_PlanetTexture", waterTexture);
                }
                else
                {
                    // 尝试查找其他可能的材质
                    var renderer = planet.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        for (int i = 0; i < renderer.materials.Length; i++)
                        {
                            var material = renderer.materials[i];
                            material.SetTexture("_PlanetTexture", waterTexture);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static Material CreateWaterMaterial(Texture2D waterTexture, WaterData waterData)
        {
            try
            {
                // 创建水材质
                Material waterMaterial = new Material(Shader.Find("Standard"));
                waterMaterial.name = "WaterMaterial";
                waterMaterial.mainTexture = waterTexture;
                waterMaterial.color = Color.white;

                // 设置材质属性
                waterMaterial.SetFloat("_Glossiness", 0.1f);
                waterMaterial.SetFloat("_Metallic", 0.0f);
                waterMaterial.SetFloat("_Mode", 3); // 
                waterMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                waterMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                waterMaterial.SetInt("_ZWrite", 0);
                waterMaterial.DisableKeyword("_ALPHATEST_ON");
                waterMaterial.EnableKeyword("_ALPHABLEND_ON");
                waterMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                waterMaterial.renderQueue = 3000;

                return waterMaterial;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WaterMod] Error creating water material: {ex.Message}");
                return null;
            }
        }
    }
} 