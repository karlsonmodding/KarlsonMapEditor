using Google.Protobuf;
using System.Collections.Generic;
using static KarlsonMapEditor.LevelEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace KarlsonMapEditor
{
    public partial class Map
    {
        public void SaveMaterials(List<Texture2D> textures, List<Material> materials)
        {
            // save textures to the map
            foreach (Texture2D texture in textures)
            {
                MapTexture mt = new MapTexture
                {
                    Name = texture.name,
                    ImageData = ByteString.CopyFrom(texture.EncodeToPNG()),
                };
                Textures.Add(mt);
            }
            // save materials to the map
            foreach (Material material in materials)
            {
                MapMaterial mm = new MapMaterial
                {
                    Name = material.name,
                    Albedo = material.color,
                    Smoothness = material.GetFloat("_Glossiness"),
                    Metallic = material.GetFloat("_Metallic"),
                    SpecularHighlight = material.GetFloat("_SpecularHighlights") != 0,
                    TextureId = textures.IndexOf((Texture2D)material.mainTexture),
                    NormalMapTextureId = textures.IndexOf((Texture2D)material.GetTexture("_BumpMap")),
                };
                Materials.Add(mm);
            }
        }
        public void LoadMaterials()
        {
            foreach (MapTexture mt in Textures)
            {
                Texture2D texture = new Texture2D(1, 1);
                texture.LoadImage(mt.ImageData.ToByteArray());
                MaterialManager.AddTexture(texture);
            }
            foreach (MapMaterial mm in Materials)
            {
                Material material = MaterialManager.InstanceMaterial();

                // set properties
                material.name = mm.Name;
                material.color = mm.Albedo;
                material.SetFloat("_Glossiness", mm.Smoothness);
                material.SetFloat("_Metallic", mm.Metallic);
                material.SetFloat("_SpecularHighlights", mm.SpecularHighlight ? 1 : 0);

                // set textures
                if (mm.TextureId > 0)
                    material.mainTexture = MaterialManager.Textures[mm.TextureId];
                if (mm.NormalMapTextureId > 0)
                    material.SetTexture("_BumpMap", MaterialManager.Textures[mm.NormalMapTextureId]);
            }
        }


        public Vector3 StartPosition
        {
            get { return LevelSerializer.ReadVector(StartPositionVector); }
            set { LevelSerializer.WriteVector(value, StartPositionVector); }
        }
        
        public Color Fog
        {
            get { return LevelSerializer.ReadColor(FogColor); }
            set { LevelSerializer.WriteColor(value, FogColor); }
        }

        // saves all the KME objects in the tree that are children of the root object
        public void SaveTree(ObjectGroup root)
        {
            Root = new MapObject();
            Root.SaveObjectGroup(root);
        }

        // generates from the tree
        public LevelPlayer.LevelData.ObjectGroup LoadTree()
        {
            return Root.LoadLevelGroup();
        }

    }

    public partial class MapMaterial
    {
        public Color Albedo
        {
            get { return LevelSerializer.ReadColor(AlbedoColor); }
            set { LevelSerializer.WriteColor(value, AlbedoColor); }
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
                MapObject mo = new MapObject();
                mo.SaveEditorObject(childObject);
                Group.Children.Add(mo);
            }
        }

        public void SaveEditorObject(EditorObject obj)
        {
            SaveBasicObject(obj);
            if (obj.data.IsPrefab) // prefab
            {
                Prefab = new MapPrefab
                {
                    PrefabId = (int)obj.data.PrefabId,
                    PrefabData = obj.data.PrefabData
                };
            }
            else // geometry
            {
                Geometry = new MapGeometry
                {
                    ShapeId = (int)obj.data.ShapeId,
                    MaterialId = obj.data.MaterialId,
                    // flags
                    Bounce = obj.data.Bounce,
                    Glass = obj.data.Glass,
                    Lava = obj.data.Lava,
                    DisableTrigger = obj.data.DisableTrigger,
                    ObjectLayer = obj.data.MarkAsObject
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
        public LevelPlayer.LevelData.ObjectGroup LoadLevelGroup()
        {
            Assert.IsTrue(TypeCase == TypeOneofCase.Group);

            LevelPlayer.LevelData.ObjectGroup levelGroup = new LevelPlayer.LevelData.ObjectGroup
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

        public LevelPlayer.LevelData.LevelObject LoadLevelObject()
        {
            Assert.IsFalse(TypeCase == TypeOneofCase.Group);

            LevelPlayer.LevelData.LevelObject levelObject = new LevelPlayer.LevelData.LevelObject
            {
                Name = Name,
                Position = Position,
                Rotation = Rotation,
                Scale = Scale
            };
            if (TypeCase == TypeOneofCase.Prefab)
            {
                levelObject.IsPrefab = true;
                levelObject.PrefabId = (PrefabType)Prefab.PrefabId;
                levelObject.PrefabData = Prefab.PrefabData;
            }
            if (TypeCase == TypeOneofCase.Geometry)
            {
                levelObject.IsPrefab = false;
                levelObject.ShapeId = (GeometryShape)Geometry.ShapeId;
                levelObject.MaterialId = Geometry.MaterialId;
                levelObject.Bounce = Geometry.Bounce;
                levelObject.Glass = Geometry.Glass;
                levelObject.Lava = Geometry.Lava;
                levelObject.DisableTrigger = Geometry.DisableTrigger;
                levelObject.MarkAsObject = Geometry.ObjectLayer;
            }
            
            return levelObject;
        }

        public Vector3 Position
        {
            get { return LevelSerializer.ReadVector(PositionVector); }
            set { LevelSerializer.WriteVector(value, PositionVector); }
        }
        public Vector3 Rotation
        {
            get { return LevelSerializer.ReadVector(RotationVector); }
            set { LevelSerializer.WriteVector(value, RotationVector); }
        }
        public Vector3 Scale
        {
            get { return LevelSerializer.ReadVector(ScaleVector); }
            set { LevelSerializer.WriteVector(value, ScaleVector); }
        }
    }

    

    public static class LevelSerializer
    {
        public const int SaveVersion = 5;

        public static Vector3 ReadVector(Google.Protobuf.Collections.RepeatedField<float> floats)
        {
            return new Vector3(floats[0], floats[1], floats[2]);
        }

        public static void WriteVector(Vector3 value, Google.Protobuf.Collections.RepeatedField<float> destination)
        {
            destination.Clear();
            destination.Add(value.x);
            destination.Add(value.y);
            destination.Add(value.z);
        }

        public static Color ReadColor(Google.Protobuf.Collections.RepeatedField<float> floats)
        {
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
