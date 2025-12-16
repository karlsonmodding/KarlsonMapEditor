using Google.Protobuf;
using KarlsonMapEditor.LevelLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
using static Google.Protobuf.Reflection.SourceCodeInfo.Types;
using static KarlsonMapEditor.LevelEditor;

namespace KarlsonMapEditor
{
    public static class LevelSerializer
    {
        public static void SaveMaterials(this Map map)
        {
            // save textures to the map
            foreach (Texture2D texture in MaterialManager.Textures)
            {
                MapTexture mt = new MapTexture();

                int internalTextureIndex = Array.IndexOf(Main.gameTex, texture);
                if (internalTextureIndex == -1)
                {
                    mt.ImageData = ByteString.CopyFrom(texture.EncodeToPNG());
                }
                else
                {
                    mt.TextureIndex = internalTextureIndex;
                }
                map.Textures.Add(mt);
            }
            // save materials to the map
            foreach (Material material in MaterialManager.Materials)
            {
                MapMaterial mm = new MapMaterial
                {
                    Mode = (MapMaterial.Types.RenderingMode)material.GetFloat("_Mode"),
                    Albedo = material.color,
                    Smoothness = material.GetFloat("_Glossiness"),
                    Metallic = material.GetFloat("_Metallic"),
                    BumpScale = material.GetFloat("_BumpScale"),
                    SpecularHighlight = material.GetFloat("_SpecularHighlights") != 0,
                    SpecularReflection = material.GetFloat("_GlossyReflections") != 0,
                    AlbedoTextureId = MaterialManager.Textures.IndexOf((Texture2D)material.mainTexture),
                    NormalMapTextureId = MaterialManager.Textures.IndexOf((Texture2D)material.GetTexture("_BumpMap")),
                    MetallicGlossTextureId = MaterialManager.Textures.IndexOf((Texture2D)material.GetTexture("_MetallicGlossMap")),
                    Scale = material.mainTextureScale,
                    Offset = material.mainTextureOffset,
                };
                map.Materials.Add(mm);
            }

            // save skybox material
            Material skybox = RenderSettings.skybox;
            if (skybox == LevelLoader.Main.Skybox.Default)
            {
                map.ClearSkybox();
            }
            else if (skybox == LevelLoader.Main.Skybox.SixSided)
            {
                map.SixSided = new MapSixSidedSkybox()
                {
                    FrontTextureId = MaterialManager.Textures.IndexOf((Texture2D)skybox.GetTexture("_FrontTex")),
                    BackTextureId = MaterialManager.Textures.IndexOf((Texture2D)skybox.GetTexture("_BackTex")),
                    LeftTextureId = MaterialManager.Textures.IndexOf((Texture2D)skybox.GetTexture("_LeftTex")),
                    RightTextureId = MaterialManager.Textures.IndexOf((Texture2D)skybox.GetTexture("_RightTex")),
                    UpTextureId = MaterialManager.Textures.IndexOf((Texture2D)skybox.GetTexture("_UpTex")),
                    DownTextureId = MaterialManager.Textures.IndexOf((Texture2D)skybox.GetTexture("_DownTex")),
                    Rotation = skybox.GetFloat("_Rotation"),
                    Exposure = skybox.GetFloat("_Exposure"),
                };
            }
            else if (skybox == LevelLoader.Main.Skybox.Procedural)
            {
                map.Procedural = new MapProceduralSkybox()
                {
                    SunSize = skybox.GetFloat("_SunSize"),
                    SunSizeConvergence = skybox.GetFloat("_SunSizeConvergence"),
                    AtmosphereThickness = skybox.GetFloat("_AtmosphereThickness"),
                    SkyTint = skybox.GetColor("_SkyTint"),
                    Ground = skybox.GetColor("_GroundColor"),
                    Exposure = skybox.GetFloat("_Exposure"),
                };
            }
        }

        // saves all the KME objects in the tree that are children of the root object
        public static void SaveTree(this Map map, ObjectGroup root, bool kmp_export)
        {
            map.Root = new MapObject();
            map.Root.SaveObjectGroup(root, kmp_export);
        }

        public static void SaveGlobalLight(this Map map, Light light)
        {
            map.GlobalLightDirection = light.transform.forward * light.intensity;
            map.GlobalLight = light.color;
        }

        // recursive function to save all children of this object group
        public static void SaveObjectGroup(this MapObject mapObject, ObjectGroup group, bool kmp_export)
        {
            mapObject.SaveBasicObject(group, kmp_export);
            mapObject.Group = new MapGroup();
            foreach (ObjectGroup childGroup in group.objectGroups)
            {
                MapObject mo = new MapObject();
                mo.SaveObjectGroup(childGroup, kmp_export);
                mapObject.Group.Children.Add(mo);
            }
            foreach (EditorObject childObject in group.editorObjects)
            {
                if (childObject.data.Type == ObjectType.Internal) continue;
                if (kmp_export && childObject.aName.StartsWith("!KMP")) continue;
                if (kmp_export && childObject.data.Type == ObjectType.Prefab) continue;
                MapObject mo = new MapObject();
                mo.SaveEditorObject(childObject, kmp_export);
                mapObject.Group.Children.Add(mo);
            }
        }

        public static void SaveEditorObject(this MapObject mapObject, EditorObject obj, bool kmp_export)
        {
            mapObject.SaveBasicObject(obj, kmp_export);
            if (obj.data.Type == ObjectType.Prefab)
            {
                mapObject.Prefab = new MapPrefab
                {
                    PrefabType = (MapPrefab.Types.PrefabType)obj.data.PrefabId,
                    PrefabData = obj.data.PrefabData
                };
            }
            else if (obj.data.Type == ObjectType.Geometry)
            {
                mapObject.Geometry = new MapGeometry
                {
                    Shape = (MapGeometry.Types.Shape)obj.data.ShapeId,
                    MaterialId = obj.data.MaterialId,
                    UvNormalizedScale = obj.data.UVNormalizedScale,
                    // flags
                    Bounce = obj.data.Bounce,
                    Glass = obj.data.Glass,
                    Lava = obj.data.Lava,
                    ObjectLayer = obj.data.MarkAsObject
                };
            }
            else if (obj.data.Type == ObjectType.Light)
            {
                Light component = obj.go.GetComponent<Light>();
                mapObject.Light = new MapLight
                {
                    SpotLight = component.type == LightType.Spot,
                    Tint = component.color,
                    Intensity = component.intensity,
                    Range = component.range,
                    SpotAngle = component.spotAngle,
                };
            }
            else if (obj.data.Type == ObjectType.Text)
            {
                TextMeshPro component = obj.go.GetComponent<TextMeshPro>();
                mapObject.TextDisplay = new MapText
                {
                    Text = component.text,
                    Shade = component.color,
                };
            }
        }

        public static void SaveBasicObject(this MapObject mapObject, IBasicProperties obj, bool kmp_export)
        {
            // set the name
            if(kmp_export)
                mapObject.Name = "";
            else
                mapObject.Name = obj.aName;

            // set the transform
            mapObject.Position = obj.aPosition;
            mapObject.Rotation = obj.aRotation;
            mapObject.Scale = obj.aScale;
        }
    }
}
