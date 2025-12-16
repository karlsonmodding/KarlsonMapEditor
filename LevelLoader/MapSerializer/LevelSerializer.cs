using Google.Protobuf;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using static KarlsonMapEditor.LevelLoader.LevelData;

namespace KarlsonMapEditor.LevelLoader
{
    public partial class Map
    {
        public void LoadMaterials()
        {
            MaterialManager.Clear();
            foreach (MapTexture mt in Textures)
            {
                switch (mt.TextureSourceCase)
                {
                    case MapTexture.TextureSourceOneofCase.TextureIndex:
                        MaterialManager.AddTexture(Main.GameTex[mt.TextureIndex]);
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
                    RenderSettings.skybox = Main.Skybox.Default; break;
                case SkyboxOneofCase.SixSided:
                    RenderSettings.skybox = Main.Skybox.SixSided;
                    if (SixSided.FrontTextureId >= 0) Main.Skybox.SixSided.SetTexture("_FrontTex", MaterialManager.Textures[SixSided.FrontTextureId]);
                    if (SixSided.BackTextureId >= 0) Main.Skybox.SixSided.SetTexture("_BackTex", MaterialManager.Textures[SixSided.BackTextureId]);
                    if (SixSided.LeftTextureId >= 0) Main.Skybox.SixSided.SetTexture("_LeftTex", MaterialManager.Textures[SixSided.LeftTextureId]);
                    if (SixSided.RightTextureId >= 0) Main.Skybox.SixSided.SetTexture("_RightTex", MaterialManager.Textures[SixSided.RightTextureId]);
                    if (SixSided.UpTextureId >= 0) Main.Skybox.SixSided.SetTexture("_UpTex", MaterialManager.Textures[SixSided.UpTextureId]);
                    if (SixSided.DownTextureId >= 0) Main.Skybox.SixSided.SetTexture("_DownTex", MaterialManager.Textures[SixSided.DownTextureId]);
                    Main.Skybox.SixSided.SetFloat("_Rotation", SixSided.Rotation);
                    Main.Skybox.SixSided.SetFloat("_Exposure", SixSided.Exposure);
                    break;
                case SkyboxOneofCase.Procedural:
                    RenderSettings.skybox = Main.Skybox.Procedural;
                    Main.Skybox.Procedural.SetFloat("_SunSize", Procedural.SunSize);
                    Main.Skybox.Procedural.SetFloat("_SunSizeConvergence", Procedural.SunSizeConvergence);
                    Main.Skybox.Procedural.SetFloat("_AtmosphereThickness", Procedural.AtmosphereThickness);
                    Main.Skybox.Procedural.SetColor("_SkyTint", Procedural.SkyTint);
                    Main.Skybox.Procedural.SetColor("_GroundColor", Procedural.Ground);
                    Main.Skybox.Procedural.SetFloat("_Exposure", Procedural.Exposure);
                    break;
            }
        }

        // generates from the tree
        public LevelData.ObjectGroup LoadTree()
        {
            return Root.LoadLevelGroup();
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
