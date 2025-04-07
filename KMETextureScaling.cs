using System.Collections.Generic;
using UnityEngine;

namespace KarlsonMapEditor
{
    // similar to TextureScaling, but works with any mesh, not just boxes
    internal class KMETextureScaling : MonoBehaviour
    {
        private bool _enabled = true;
        private float _scale = 1;

        public bool Enabled
        {
            get { return _enabled; }
            set { 
                if (_enabled == value) return;
                _enabled = value;
                UpdateUVs();
            }
        }
        public float Scale
        {
            get { if (_enabled) return _scale; else return 0; }
            set {
                if (value == 0) { Enabled = false; return; }
                if (_scale == value) return;
                _scale = value;
                if (_enabled) UpdateUVs();
            }
        }
        // the object's scale changes
        public void UpdateScale()
        {
            if (_enabled) UpdateUVs();
        }

        private Mesh referenceMesh;
        private Mesh localMesh;
        private List<Vector2> referenceMeshUVs = new List<Vector2>();
        private List<Vector3> referenceMeshNormals = new List<Vector3>();
        private List<Vector4> referenceMeshTangents = new List<Vector4>();

        public void Init()
        {
            referenceMesh = GetComponent<MeshFilter>().sharedMesh;
            localMesh = Instantiate(referenceMesh);
            GetComponent<MeshFilter>().mesh = localMesh;

            referenceMesh.GetUVs(0, referenceMeshUVs);
            referenceMesh.GetNormals(referenceMeshNormals);
            referenceMesh.GetTangents(referenceMeshTangents);
        }
        private void Start()
        {
            UpdateUVs();
        }

        private void UpdateUVs()
        {
            if (!Enabled)
            {
                // reset the UVs
                localMesh.SetUVs(0, referenceMeshUVs);
                return;
            }

            // normalize the UVs

            Vector3 goScale = transform.localScale;
            List<Vector2> localUVs = new List<Vector2>();
            for (int i = 0; i < referenceMeshUVs.Count; i++)
            {
                // U axis
                Vector3 tangent = (Vector3)referenceMeshTangents[i];
                // V axis (might be inverted)
                Vector3 binormal = Vector3.Cross(referenceMeshNormals[i], tangent);
                // how much textures have been stretched by the scaling applied to the GO
                Vector2 UVscale = new Vector2(
                    Vector3.Scale(tangent, goScale).magnitude,
                    Vector3.Scale(binormal, goScale).magnitude
                    );

                localUVs.Add(referenceMeshUVs[i] * UVscale / _scale);
            }
            localMesh.SetUVs(0, localUVs);
        }
    }
}
