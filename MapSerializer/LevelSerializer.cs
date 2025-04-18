﻿using Google.Protobuf;
using System;
using System.Collections.Generic;
using static KarlsonMapEditor.LevelEditor;
using UnityEngine;
using UnityEngine.Assertions;
using TMPro;

namespace KarlsonMapEditor
{
    public partial class Map
    {
        public void SaveMaterials()
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
                Textures.Add(mt);
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
                Materials.Add(mm);
            }

            // save skybox material
            Material skybox = RenderSettings.skybox;
            if (skybox == Main.defaultSkybox)
            {
                ClearSkybox();
            }
            else if (skybox == SixSidedSkybox)
            {
                SixSided = new MapSixSidedSkybox()
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
            else if (skybox == ProceduralSkybox)
            {
                Procedural = new MapProceduralSkybox()
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
        public void LoadMaterials()
        {
            MaterialManager.Clear();
            foreach (MapTexture mt in Textures)
            {
                switch (mt.TextureSourceCase)
                {
                    case MapTexture.TextureSourceOneofCase.TextureIndex:
                        MaterialManager.AddTexture(Main.gameTex[mt.TextureIndex]);
                        break;
                    case MapTexture.TextureSourceOneofCase.ImageData:
                        Texture2D texture = new Texture2D(1, 1);
                        texture.LoadImage(mt.ImageData.ToByteArray());
                        MaterialManager.AddTexture(texture);
                        break;
                }
            }
            foreach (MapMaterial mm in Materials)
            {
                Material material = MaterialManager.InstanceMaterial();

                

                // set properties
                MaterialManager.UpdateMode(material, (MaterialManager.ShaderBlendMode)mm.Mode);
                material.color = mm.Albedo;
                material.SetFloat("_Glossiness", mm.Smoothness);
                material.SetFloat("_Metallic", mm.Metallic);
                material.SetFloat("_BumpScale", mm.BumpScale);
                material.SetFloat("_SpecularHighlights", mm.SpecularHighlight ? 1 : 0);
                material.SetFloat("_GlossyReflections", mm.SpecularReflection ? 1 : 0);
                material.mainTextureScale = mm.Scale;
                material.mainTextureOffset = mm.Offset;
                // property keywords
                if (mm.SpecularHighlight) { material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF"); }
                else { material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); }
                if (mm.SpecularReflection) { material.DisableKeyword("_GLOSSYREFLECTIONS_OFF"); }
                else { material.EnableKeyword("_GLOSSYREFLECTIONS_OFF"); }

                // set textures
                // main
                if (mm.AlbedoTextureId >= 0)
                    material.mainTexture = MaterialManager.Textures[mm.AlbedoTextureId];
                // normal
                if (mm.NormalMapTextureId >= 0)
                {
                    material.EnableKeyword("_NORMALMAP");
                    material.SetTexture("_BumpMap", MaterialManager.Textures[mm.NormalMapTextureId]);
                }
                else { material.DisableKeyword("_NORMALMAP"); }
                // metallic and gloss
                if (mm.MetallicGlossTextureId >= 0)
                {
                    material.EnableKeyword("_METALLICGLOSSMAP");
                    material.SetTexture("_MetallicGlossMap", MaterialManager.Textures[mm.MetallicGlossTextureId]);
                }
                else { material.DisableKeyword("_METALLICGLOSSMAP"); }
            }

            // load skybox
            switch (SkyboxCase)
            {
                case SkyboxOneofCase.None:
                    RenderSettings.skybox = Main.defaultSkybox; break;
                case SkyboxOneofCase.SixSided:
                    RenderSettings.skybox = SixSidedSkybox;
                    if (SixSided.FrontTextureId >= 0) SixSidedSkybox.SetTexture("_FrontTex", MaterialManager.Textures[SixSided.FrontTextureId]);
                    if (SixSided.BackTextureId >= 0) SixSidedSkybox.SetTexture("_BackTex", MaterialManager.Textures[SixSided.BackTextureId]);
                    if (SixSided.LeftTextureId >= 0) SixSidedSkybox.SetTexture("_LeftTex", MaterialManager.Textures[SixSided.LeftTextureId]);
                    if (SixSided.RightTextureId >= 0) SixSidedSkybox.SetTexture("_RightTex", MaterialManager.Textures[SixSided.RightTextureId]);
                    if (SixSided.UpTextureId >= 0) SixSidedSkybox.SetTexture("_UpTex", MaterialManager.Textures[SixSided.UpTextureId]);
                    if (SixSided.DownTextureId >= 0) SixSidedSkybox.SetTexture("_DownTex", MaterialManager.Textures[SixSided.DownTextureId]);
                    SixSidedSkybox.SetFloat("_Rotation", SixSided.Rotation);
                    SixSidedSkybox.SetFloat("_Exposure", SixSided.Exposure);
                    break;
                case SkyboxOneofCase.Procedural:
                    RenderSettings.skybox = ProceduralSkybox;
                    ProceduralSkybox.SetFloat("_SunSize", Procedural.SunSize);
                    ProceduralSkybox.SetFloat("_SunSizeConvergence", Procedural.SunSizeConvergence);
                    ProceduralSkybox.SetFloat("_AtmosphereThickness", Procedural.AtmosphereThickness);
                    ProceduralSkybox.SetColor("_SkyTint", Procedural.SkyTint);
                    ProceduralSkybox.SetColor("_GroundColor", Procedural.Ground);
                    ProceduralSkybox.SetFloat("_Exposure", Procedural.Exposure);
                    break;
            }
        }

        // saves all the KME objects in the tree that are children of the root object
        public void SaveTree(ObjectGroup root)
        {
            Root = new MapObject();
            Root.SaveObjectGroup(root);
        }
        // generates from the tree
        public LevelData.ObjectGroup LoadTree()
        {
            return Root.LoadLevelGroup();
        }

        public void SaveGlobalLight(Light light)
        {
            GlobalLightDirection = light.transform.forward * light.intensity;
            GlobalLight = light.color;
        }
        public void LoadGlobalLight(Light light)
        {
            light.color = GlobalLight;
            light.intensity = GlobalLightDirection.magnitude;
            light.transform.rotation = Quaternion.LookRotation(GlobalLightDirection);
            light.enabled = GlobalLightDirection != Vector3.zero;
        }

        public Vector3 StartPosition
        {
            get { return LevelSerializer.ReadVector3(StartPositionVector); }
            set { LevelSerializer.WriteVector3(value, StartPositionVector); }
        }
        public Vector3 GlobalLightDirection
        {
            get { return LevelSerializer.ReadVector3(GlobalLightDirectionVector); }
            set { LevelSerializer.WriteVector3(value, GlobalLightDirectionVector); }
        }
        public Color GlobalLight
        {
            get { return LevelSerializer.ReadColor(GlobalLightColor); }
            set { LevelSerializer.WriteColor(value, GlobalLightColor); }
        }
    }

    public partial class MapMaterial
    {
        public Color Albedo
        {
            get { return LevelSerializer.ReadColor(AlbedoColor); }
            set { LevelSerializer.WriteColor(value, AlbedoColor); }
        }
        public Vector2 Scale
        {
            get { return LevelSerializer.ReadVector2(ScaleVector); }
            set { LevelSerializer.WriteVector2(value, ScaleVector); }
        }
        public Vector2 Offset
        {
            get { return LevelSerializer.ReadVector2(OffsetVector); }
            set { LevelSerializer.WriteVector2(value, OffsetVector); }
        }
    }

    public partial class MapProceduralSkybox
    {
        public Color SkyTint
        {
            get { return LevelSerializer.ReadColor(SkyTintColor); }
            set { LevelSerializer.WriteColor(value, SkyTintColor); }
        }
        public Color Ground
        {
            get { return LevelSerializer.ReadColor(GroundColor); }
            set { LevelSerializer.WriteColor(value, GroundColor); }
        }
    }

    public partial class MapLight
    {
        public Color Tint
        {
            get { return LevelSerializer.ReadColor(TintColor); }
            set { LevelSerializer.WriteColor(value, TintColor); }
        }
    }

    public partial class MapText
    {
        public Color Shade
        {
            get { return LevelSerializer.ReadColor(ShadeColor); }
            set { LevelSerializer.WriteColor(value, ShadeColor); }
        }
    }

    public partial class MapObject
    {
        // recursive function to save all children of this object group
        public void SaveObjectGroup(ObjectGroup group)
        {
            SaveBasicObject(group);
            Group = new MapGroup();
            foreach (ObjectGroup childGroup in group.objectGroups)
            {
                MapObject mo = new MapObject();
                mo.SaveObjectGroup(childGroup);
                Group.Children.Add(mo);
            }
            foreach (EditorObject childObject in group.editorObjects)
            {
                if (childObject.data.Type == ObjectType.Internal) continue;
                MapObject mo = new MapObject();
                mo.SaveEditorObject(childObject);
                Group.Children.Add(mo);
            }
        }

        public void SaveEditorObject(EditorObject obj)
        {
            SaveBasicObject(obj);
            if (obj.data.Type == ObjectType.Prefab)
            {
                Prefab = new MapPrefab
                {
                    PrefabType = (MapPrefab.Types.PrefabType)obj.data.PrefabId,
                    PrefabData = obj.data.PrefabData
                };
            }
            else if (obj.data.Type == ObjectType.Geometry)
            {
                Geometry = new MapGeometry
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
                Light = new MapLight
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
                TextDisplay = new MapText
                {
                    Text = component.text,
                    Shade = component.color,
                };
            }
        }

        public void SaveBasicObject(IBasicProperties obj)
        {
            // set the name
            Name = obj.aName;

            // set the transform
            Position = obj.aPosition;
            Rotation = obj.aRotation;
            Scale = obj.aScale;
        }

        // recursive function to load this map object and all children
        public LevelData.ObjectGroup LoadLevelGroup()
        {
            Assert.IsTrue(TypeCase == TypeOneofCase.Group);

            LevelData.ObjectGroup levelGroup = new LevelData.ObjectGroup
            {
                Name = Name,
                Position = Position,
                Rotation = Rotation,
                Scale = Scale
            };
            foreach (MapObject child in Group.Children)
                if (child.TypeCase == TypeOneofCase.Group) { levelGroup.Groups.Add(child.LoadLevelGroup()); }
                else { levelGroup.Objects.Add(child.LoadLevelObject()); }
            return levelGroup;
        }

        public LevelData.LevelObject LoadLevelObject()
        {
            Assert.IsFalse(TypeCase == TypeOneofCase.Group);

            LevelData.LevelObject levelObject = new LevelData.LevelObject
            {
                Name = Name,
                Position = Position,
                Rotation = Rotation,
                Scale = Scale
            };
            if (TypeCase == TypeOneofCase.Prefab)
            {
                levelObject.Type = ObjectType.Prefab;
                levelObject.PrefabId = (PrefabType)Prefab.PrefabType;
                levelObject.PrefabData = Prefab.PrefabData;
            }
            else if (TypeCase == TypeOneofCase.Geometry)
            {
                levelObject.Type = ObjectType.Geometry;
                levelObject.ShapeId = (GeometryShape)Geometry.Shape;
                levelObject.MaterialId = Geometry.MaterialId;
                levelObject.UVNormalizedScale = Geometry.UvNormalizedScale;
                levelObject.Bounce = Geometry.Bounce;
                levelObject.Glass = Geometry.Glass;
                levelObject.Lava = Geometry.Lava;
                levelObject.MarkAsObject = Geometry.ObjectLayer;
            }
            else if (TypeCase == TypeOneofCase.Light)
            {
                levelObject.Type = ObjectType.Light;
                levelObject.LightType = Light.SpotLight ? LightType.Spot : LightType.Point;
                levelObject.Color = Light.Tint;
                levelObject.Intensity = Light.Intensity;
                levelObject.Range = Light.Range;
                levelObject.SpotAngle = Light.SpotAngle;
            }
            else if (TypeCase == TypeOneofCase.TextDisplay)
            {
                levelObject.Type = ObjectType.Text;
                levelObject.Text = TextDisplay.Text;
                levelObject.Color = TextDisplay.Shade;
            }

            return levelObject;
        }

        public Vector3 Position
        {
            get { return LevelSerializer.ReadVector3(PositionVector); }
            set { LevelSerializer.WriteVector3(value, PositionVector); }
        }
        public Vector3 Rotation
        {
            get { return LevelSerializer.ReadVector3(RotationVector); }
            set { LevelSerializer.WriteVector3(value, RotationVector); }
        }
        public Vector3 Scale
        {
            get { return LevelSerializer.ReadVector3(ScaleVector); }
            set { LevelSerializer.WriteVector3(value, ScaleVector); }
        }
    }

    

    public static class LevelSerializer
    {
        public const int SaveVersion = 5;

        public static Vector3 ReadVector3(Google.Protobuf.Collections.RepeatedField<float> floats)
        {
            if (floats.Count != 3)
                return Vector3.zero;
            return new Vector3(floats[0], floats[1], floats[2]);
        }
        public static void WriteVector3(Vector3 value, Google.Protobuf.Collections.RepeatedField<float> destination)
        {
            destination.Clear();
            destination.Add(value.x);
            destination.Add(value.y);
            destination.Add(value.z);
        }

        public static Vector2 ReadVector2(Google.Protobuf.Collections.RepeatedField<float> floats)
        {
            if (floats.Count != 2)
                return Vector2.zero;
            return new Vector2(floats[0], floats[1]);
        }
        public static void WriteVector2(Vector2 value, Google.Protobuf.Collections.RepeatedField<float> destination)
        {
            destination.Clear();
            destination.Add(value.x);
            destination.Add(value.y);
        }

        public static Color ReadColor(Google.Protobuf.Collections.RepeatedField<float> floats)
        {
            if (floats.Count != 4)
                return new Color(0, 0, 0, 0);
            return new Color(floats[0], floats[1], floats[2], floats[3]);
        }

        public static void WriteColor(Color value, Google.Protobuf.Collections.RepeatedField<float> destination)
        {
            destination.Clear();
            destination.Add(value.r);
            destination.Add(value.g);
            destination.Add(value.b);
            destination.Add(value.a);
        }
    }
}
