using MoonSharp.Interpreter;
using System;
using TMPro;
using UnityEngine;

namespace KarlsonMapEditor.Scripting_API
{
    public class ObjectProxy
    {
        [MoonSharpHidden]
        public UnityEngine.Object target;
        [MoonSharpHidden]
        public bool IsValid => target != null;

        [MoonSharpHidden]
        public ObjectProxy(UnityEngine.Object instance)
        {
            target = instance;
        }

        public ObjectProxy() 
        {
            target = new UnityEngine.Object();
        }

        // static methods
        public static Action<UnityEngine.Object> Destroy = UnityEngine.Object.Destroy;
        public static Func<UnityEngine.Object, UnityEngine.Object> Instantiate = UnityEngine.Object.Instantiate;
        public UnityEngine.Object FindObjectOfType(string type)
        { return UnityEngine.Object.FindObjectOfType(Type.GetType(type)); }
        public UnityEngine.Object[] FindObjectsOfType(string type)
        { return UnityEngine.Object.FindObjectsOfType(Type.GetType(type)); }

        // instance methods
        public override string ToString()
        {
            return target.ToString();
        }

        public override bool Equals(object obj)
        {
            return target.Equals(obj);
        }
        public override int GetHashCode()
        {
            return target.GetHashCode();
        }

        // instance properties
        public string name
        {
            get { return target.name; }
            set { target.name = value; }
        }
    }

    public class GameObjectProxy : ObjectProxy
    {
        [MoonSharpHidden]
        new public GameObject target;

        [MoonSharpHidden]
        public GameObjectProxy(GameObject instance) : base(instance)
        {
            SetActive = instance.SetActive;
            GetComponent = instance.GetComponent;
            CompareTag = instance.CompareTag;
        }

        // static methods
        public static Func<string, GameObject> Find = GameObject.Find;
        public static Func<string, GameObject> FindGameObjectWithTag = GameObject.FindGameObjectWithTag;
        public static Func<string, GameObject[]> FindGameObjectsWithTag = GameObject.FindGameObjectsWithTag;

        // instance methods
        public Action<bool> SetActive;
        public Func<string, Component> GetComponent;
        public Func<string, bool> CompareTag;

        // instance properties
        public bool activeSelf
        {
            get { return target.activeSelf; }
        }
        public bool activeInHierarchy
        {
            get { return target.activeInHierarchy; }
        }
        public int layer
        {
            get { return target.layer; }
            set { target.layer = value; }
        }
        public string tag
        {
            get { return target.tag; }
            set { target.tag = value; }
        }
        public Transform transform
        {
            get { return target.transform; }
        }
    }

    public class ComponentProxy : ObjectProxy
    {
        [MoonSharpHidden]
        new public Component target;

        [MoonSharpHidden]
        public ComponentProxy(Component instance) : base(instance)
        {
            GetComponent = instance.GetComponent;
            CompareTag = instance.CompareTag;
        }

        // instance methods
        public Func<string, Component> GetComponent;
        public Func<string, bool> CompareTag;

        // instance properties
        public GameObject gameObject
        {
            get { return target.gameObject; }
        }
        public string tag
        {
            get { return target.tag; }
            set { target.tag = value; }
        }
        public Transform transform
        {
            get { return target.transform; }
        }
    }
    

    public class MaterialProxy : ObjectProxy
    {
        [MoonSharpHidden]
        new public Material target;

        [MoonSharpHidden]
        public MaterialProxy(Material instance) : base(instance) { }

        public MaterialProxy()
        {
            target = LevelEditor.MaterialManager.InstanceMaterial();
        }
        public MaterialProxy(MaterialProxy original) : this()
        {
            target.CopyPropertiesFromMaterial(original.target);
        }

        public Color color
        {
            get { return target.color; }
            set { target.color = value; }
        }

        // textures
        public Texture mainTexture
        {
            get { return target.mainTexture; }
            set { target.mainTexture = value; }
        }
        public Texture normalTexture
        {
            get { return target.GetTexture("_BumpMap"); }
            set { target.SetTexture("_BumpMap", value); }
        }
        public Texture metalicGlossTexture
        {
            get { return target.GetTexture("_MetallicGlossMap"); }
            set { target.SetTexture("_MetallicGlossMap", value); }
        }

        public Vector2 textureOffset
        {
            get { return target.mainTextureOffset; }
            set 
            {
                target.SetTextureOffset("_MainTex", value);
                target.SetTextureOffset("_BumpMap", value);
                target.SetTextureOffset("_MetallicGlossMap", value);
            }
        }
        public Vector2 textureScale
        {
            get { return target.mainTextureOffset; }
            set
            {
                target.SetTextureScale("_MainTex", value);
                target.SetTextureScale("_BumpMap", value);
                target.SetTextureScale("_MetallicGlossMap", value);
            }
        }

        public LevelEditor.MaterialManager.ShaderBlendMode Mode
        {
            get { return (LevelEditor.MaterialManager.ShaderBlendMode)target.GetFloat("_Mode"); }
            set { LevelEditor.MaterialManager.UpdateMode(target, value); }
        }

        public float Metallic
        {
            get { return target.GetFloat("_Metallic"); }
            set { target.SetFloat("_Metallic", value); }
        }
        public float Glossiness
        {
            get { return target.GetFloat("_Glossiness"); }
            set { target.SetFloat("_Glossiness", value); }
        }
        public bool SpecularHighlight
        {
            get { return target.GetFloat("_SpecularHighlights") != 0; }
            set { target.SetFloat("_SpecularHighlights", value ? 1 : 0); }
        }
        public bool SpecularReflection
        {
            get { return target.GetFloat("_GlossyReflections") != 0; }
            set { target.SetFloat("_GlossyReflections", value ? 1 : 0); }
        }
    }

    public class TextMeshProProxy : ComponentProxy
    {
        [MoonSharpHidden]
        new public TextMeshPro target;

        [MoonSharpHidden]
        public TextMeshProProxy(TextMeshPro instance) : base(instance) { }

        public string text
        {
            get { return target.text; }
            set { target.text = value; }
        }

        public Color color
        {
            get { return target.color; }
            set { target.color = value; }
        }
    }

    public class LightProxy : ComponentProxy
    {
        [MoonSharpHidden]
        new public Light target;

        [MoonSharpHidden]
        public LightProxy(Light instance) : base(instance)
        {
            target = instance;
        }
        public Color color
        {
            get { return target.color; }
            set { target.color = value; }
        }
        public bool SpotLight
        {
            get { return target.type == LightType.Spot; }
            set { if (target.type != LightType.Directional) target.type = value ? LightType.Spot : LightType.Point; }
        }
        public float intensity
        {
            get { return target.intensity; }
            set { target.intensity = value; }
        }
        public float range
        {
            get { return target.range; }
            set { target.range = value; }
        }
        public float spotAngle
        {
            get { return target.spotAngle; }
            set { target.spotAngle = value; }
        }
    }
}
