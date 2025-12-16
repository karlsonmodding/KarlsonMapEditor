using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KarlsonMapEditor.LevelLoader
{
    public static class MaterialManager
    {
        private static readonly string[] ordinalDirecitons = new string[] { "Front", "Back", "Left", "Right", "Up", "Down" };

        public static List<Texture2D> Textures { get => textures; }
        private static List<Texture2D> textures = new List<Texture2D>();
        public static List<Material> Materials { get => materials; }
        private static List<Material> materials = new List<Material>();

        public static Texture2D SelectedTexture; // for choosing textures in a context menu
        public static Action<Texture2D> UpdateSelectedTexture;

        public static Shader defaultShader;

        public static Shader lightBillboardShader;
        public static Texture2D lightbulbTransparent;
        public static Texture2D lightbulbTransparentColor;

        public enum ShaderBlendMode
        {
            Opaque,
            Cutout,
            Fade,   // Old school alpha-blending mode, fresnel does not affect amount of transparency
            Transparent // Physically plausible transparency mode, implemented as alpha pre-multiply
        };

        public static void InitInternalTextures()
        {
            Clear();
            foreach (Texture2D tex in Main.GameTex)
            {
                AddTexture(tex);
            }
        }

        public static void Clear()
        {
            textures.Clear();
            materials.Clear();
        }

        public static void AddTexture(Texture2D tex)
        {
            tex.wrapMode = TextureWrapMode.Repeat;
            textures.Add(tex);
        }

        public static bool[] TexturesInUse()
        {
            bool[] used = new bool[textures.Count];
            for (int i = 0; i < Main.GameTex.Length; i++)
            {
                used[i] = true;
            }
            foreach (Material mat in materials)
            {
                if (mat.mainTexture)
                    used[textures.IndexOf((Texture2D)mat.mainTexture)] = true;
                if (mat.GetTexture("_BumpMap"))
                    used[textures.IndexOf((Texture2D)mat.GetTexture("_BumpMap"))] = true;
                if (mat.GetTexture("_MetallicGlossMap"))
                    used[textures.IndexOf((Texture2D)mat.GetTexture("_MetallicGlossMap"))] = true;
            }
            foreach (string direction in ordinalDirecitons)
            {
                Texture t = Main.Skybox.SixSided.GetTexture("_" + direction + "Tex");
                if (t)
                    used[textures.IndexOf((Texture2D)t)] = true;
            }
            return used;
        }
        public static void RemoveTexture(Texture2D tex)
        {
            textures.Remove(tex);
        }

        public static Material InstanceMaterial()
        {
            Material mat = new Material(defaultShader)
            {
                mainTexture = textures[6]
            };
            materials.Add(mat);
            return mat;
        }
        // instancing materials for save versions without material data
        public static int InstanceMaterial(int TextureId, Color color, bool transparent)
        {
            Material mat = new Material(defaultShader)
            {
                mainTexture = textures[TextureId],
                color = color
            };
            if (transparent)
            {
                UpdateMode(mat, ShaderBlendMode.Transparent);
                // values used glass in the prefab
                mat.SetFloat("_Metallic", 0.171f);
                mat.SetFloat("_Glossiness", 0.453f);
            }

            materials.Add(mat);
            return materials.Count - 1;
        }

        public static Material InstanceLightMaterial(Color color)
        {
            Material mat = new Material(lightBillboardShader);
            mat.mainTexture = lightbulbTransparent;
            mat.SetTexture("_ColoredTex", lightbulbTransparentColor);
            mat.color = color;
            return mat;
        }

        public static int GetMainTextureIndex(int materialId)
        {
            return textures.IndexOf((Texture2D)materials[materialId].mainTexture);
        }
        public static List<Texture2D> GetExternalTextures()
        {
            return textures.Skip(Main.GameTex.Length).ToList();
        }

        public static void UpdateMode(Material mat, ShaderBlendMode mode)
        {
            mat.SetFloat("_Mode", (int)mode);
            switch (mode)
            {
                case ShaderBlendMode.Opaque:
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.DisableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = -1;
                    break;
                case ShaderBlendMode.Cutout:
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.DisableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    break;
                case ShaderBlendMode.Fade:
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    break;
                case ShaderBlendMode.Transparent:
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.DisableKeyword("_ALPHABLEND_ON");
                    mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    break;
            }
        }
    }
}
