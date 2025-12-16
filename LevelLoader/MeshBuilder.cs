using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KarlsonMapEditor.LevelLoader
{
    public static class MeshBuilder
    {
        public static GameObject GetGeometryGO(GeometryShape shape)
        {
            Mesh mesh = GeometryMeshes[(int)shape];
            GameObject go = new GameObject();
            go.layer = LayerMask.NameToLayer("Ground");
            go.AddComponent<MeshFilter>().mesh = mesh;
            go.AddComponent<MeshRenderer>();
            go.AddComponent<KMETextureScaling>().Init();

            switch (shape)
            {
                case GeometryShape.Cube:
                    go.AddComponent<BoxCollider>();
                    break;
                case GeometryShape.Sphere:
                    go.AddComponent<SphereCollider>();
                    break;
                case GeometryShape.Plane:
                    go.AddComponent<BoxCollider>().size = new Vector3(1, 1, 0);
                    break;
                default:
                    MeshCollider collider = go.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh;
                    collider.convex = shape != GeometryShape.QuarterPipe;
                    break;
            }
            return go;
        }

        private static readonly Mesh[] GeometryMeshes = {
            GetCubeMesh(),
            GetSphereMesh(),
            GetCylinderMesh(),
            GetPlaneMesh(),
            GetSquarePyramidMesh(),
            GetTrianglePrismMesh(),
            GetQuarterSquarePyramidMesh(),
            GetQuarterPipeMesh(),
        };

        private static Mesh GetCubeMesh()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            // fix uvs
            Vector2[] uvs = mesh.uv;
            // taken from TextureScaling for reverse compatibility
            uvs[2] = new Vector2(0, 1);
            uvs[3] = new Vector2(1, 1);
            uvs[0] = new Vector2(0, 0);
            uvs[1] = new Vector2(1, 0);
            uvs[7] = new Vector2(0, 0);
            uvs[6] = new Vector2(1, 0);
            uvs[11] = new Vector2(0, 1);
            uvs[10] = new Vector2(1, 1);
            uvs[19] = new Vector2(1, 0);
            uvs[17] = new Vector2(0, 1);
            uvs[16] = new Vector2(0, 0);
            uvs[18] = new Vector2(1, 1);
            uvs[23] = new Vector2(1, 0);
            uvs[21] = new Vector2(0, 1);
            uvs[20] = new Vector2(0, 0);
            uvs[22] = new Vector2(1, 1);
            uvs[4] = new Vector2(1, 0);
            uvs[5] = new Vector2(0, 0);
            uvs[8] = new Vector2(1, 1);
            uvs[9] = new Vector2(0, 1);
            uvs[13] = new Vector2(1, 0);
            uvs[14] = new Vector2(0, 0);
            uvs[12] = new Vector2(1, 1);
            uvs[15] = new Vector2(0, 1);

            mesh.uv = uvs;

            return mesh;
        }
        private static Mesh GetSphereMesh()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            return mesh;
        }
        private static Mesh GetCylinderMesh()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);

            Vector3[] verts = mesh.vertices;
            // clamp vertices to a 1x1x1 space
            mesh.vertices = verts.Select((v, index) =>
                      Vector3.Max(Vector3.Min(v, Vector3.one * 0.5f), Vector3.one * -0.5f)
                      ).ToArray();

            return mesh;
        }
        private static Mesh GetPlaneMesh()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            return mesh;
        }
        private static Mesh GetTrianglePrismMesh()
        {
            Mesh mesh = new Mesh();

            mesh.SetVertices((new Vector3[]
            {
                // stretched face
                new Vector3(0, 0, 1),
                new Vector3(1, 0, 1),
                new Vector3(1, 1, 0),
                new Vector3(0, 1, 0),
                // triangle left
                new Vector3(0, 0, 1),
                new Vector3(0, 1, 0),
                new Vector3(0, 0, 0),
                // triangle right
                new Vector3(1, 0, 1),
                new Vector3(1, 0, 0),
                new Vector3(1, 1, 0),
                // base
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(1, 0, 1),
                new Vector3(0, 0, 1),
                // back
                new Vector3(0, 0, 0),
                new Vector3(0, 1, 0),
                new Vector3(1, 1, 0),
                new Vector3(1, 0, 0),
            }).Select(v => v - new Vector3(0.5f, 0.5f, 0.5f)).ToList());

            mesh.SetUVs(0, new List<Vector2>
            {
                // stretched face
                new Vector2(1, 0),
                new Vector2(0, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),
                // triangle left
                new Vector2(0, 0),
                new Vector2(1, 1),
                new Vector2(1, 0),
                // triangle right
                new Vector2(1, 0),
                new Vector2(0, 0),
                new Vector2(0, 1),
                // base
                new Vector2(1, 0),
                new Vector2(0, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),
                // back
                new Vector2(0, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(1, 0),
            });
            mesh.SetTriangles(new int[]
            {
                // stretched face
                0, 1, 2,
                0, 2, 3,
                // triangle left
                4, 5, 6,
                // triangle right
                7, 8, 9,
                // base
                10, 11, 12,
                10, 12, 13,
                // back
                14, 15, 16,
                14, 16, 17,
            }, 0);

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;

        }
        private static Mesh GetSquarePyramidMesh()
        {
            Mesh mesh = new Mesh();

            mesh.SetVertices((new Vector3[]
            {
                // triangle front
                new Vector3(0, 0, 1),
                new Vector3(1, 0, 1),
                new Vector3(0.5f, 1, 0.5f),
                // triangle back
                new Vector3(1, 0, 0),
                new Vector3(0, 0, 0),
                new Vector3(0.5f, 1, 0.5f),
                // triangle left
                new Vector3(0, 0, 0),
                new Vector3(0, 0, 1),
                new Vector3(0.5f, 1, 0.5f),
                // triangle right
                new Vector3(1, 0, 1),
                new Vector3(1, 0, 0),
                new Vector3(0.5f, 1, 0.5f),
                // base
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(1, 0, 1),
                new Vector3(0, 0, 1),

            }).Select(v => v - new Vector3(0.5f, 0.5f, 0.5f)).ToList());

            mesh.SetUVs(0, new List<Vector2>
            {
                // triangle front
                new Vector2(1, 0),
                new Vector2(0, 0),
                new Vector2(0.5f, 1),
                // triangle back
                new Vector2(1, 0),
                new Vector2(0, 0),
                new Vector2(0.5f, 1),
                // triangle left
                new Vector2(1, 0),
                new Vector2(0, 0),
                new Vector2(0.5f, 1),
                // triangle right
                new Vector2(1, 0),
                new Vector2(0, 0),
                new Vector2(0.5f, 1),
                // base
                new Vector2(1, 0),
                new Vector2(0, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),
            });
            mesh.SetTriangles(new int[]
            {
                // triangle front
                0, 1, 2,
                // triangle back
                3, 4, 5,
                // triangle left
                6, 7, 8,
                // triangle right
                9, 10, 11,
                // base
                12, 13, 14,
                12, 14, 15,
            }, 0);

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
        private static Mesh GetQuarterSquarePyramidMesh()
        {
            Mesh mesh = new Mesh();

            mesh.SetVertices((new Vector3[]
            {
                // triangle front
                new Vector3(0, 0, 1),
                new Vector3(1, 0, 1),
                new Vector3(0, 1, 0),
                // triangle back
                new Vector3(1, 0, 0),
                new Vector3(0, 0, 0),
                new Vector3(0, 1, 0),
                // triangle left
                new Vector3(0, 0, 0),
                new Vector3(0, 0, 1),
                new Vector3(0, 1, 0),
                // triangle right
                new Vector3(1, 0, 1),
                new Vector3(1, 0, 0),
                new Vector3(0, 1, 0),
                // base
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(1, 0, 1),
                new Vector3(0, 0, 1),

            }).Select(v => v - new Vector3(0.5f, 0.5f, 0.5f)).ToList());

            mesh.SetUVs(0, new List<Vector2>
            {
                // triangle front
                new Vector2(1, 0),
                new Vector2(0, 0),
                new Vector2(1, 1),
                // triangle back
                new Vector2(1, 0),
                new Vector2(0, 0),
                new Vector2(0, 1),
                // triangle left
                new Vector2(1, 0),
                new Vector2(0, 0),
                new Vector2(1, 1),
                // triangle right
                new Vector2(1, 0),
                new Vector2(0, 0),
                new Vector2(0, 1),
                // base
                new Vector2(1, 0),
                new Vector2(0, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),
            });
            mesh.SetTriangles(new int[]
            {
                // triangle front
                0, 1, 2,
                // triangle back
                3, 4, 5,
                // triangle left
                6, 7, 8,
                // triangle right
                9, 10, 11,
                // base
                12, 13, 14,
                12, 14, 15,
            }, 0);

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
        private static Mesh GetQuarterPipeMesh()
        {
            const int curvature = 12;
            Mesh mesh = new Mesh();

            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            // left corner
            verts.Add(new Vector3(0, 0, 0));
            uvs.Add(new Vector2(1, 0));
            // right corner
            verts.Add(new Vector3(1, 0, 0));
            uvs.Add(new Vector2(0, 0));

            for (int i = 0; i < curvature + 1; i++)
            {
                float angle = i * (Mathf.PI / 2) / curvature;

                // vertices
                // center
                verts.Add(new Vector3(0, 1 - Mathf.Sin(angle), 1 - Mathf.Cos(angle)));
                verts.Add(new Vector3(1, 1 - Mathf.Sin(angle), 1 - Mathf.Cos(angle)));
                // left
                verts.Add(new Vector3(0, 1 - Mathf.Sin(angle), 1 - Mathf.Cos(angle)));
                // right
                verts.Add(new Vector3(1, 1 - Mathf.Sin(angle), 1 - Mathf.Cos(angle)));

                // vertex uvs
                // center
                uvs.Add(new Vector2(1, 1 - (float)i / curvature));
                uvs.Add(new Vector2(0, 1 - (float)i / curvature));
                // left
                uvs.Add(new Vector2(Mathf.Cos(angle), 1 - Mathf.Sin(angle)));
                // right
                uvs.Add(new Vector2(1 - Mathf.Cos(angle), 1 - Mathf.Sin(angle)));

                // triangles
                if (i > 0)
                {
                    int o = 4 * i - 2;
                    // center
                    tris.Add(o); tris.Add(o + 5); tris.Add(o + 1);
                    tris.Add(o); tris.Add(o + 4); tris.Add(o + 5);
                    // left
                    tris.Add(o + 6); tris.Add(o + 2); tris.Add(0);
                    // right
                    tris.Add(o + 3); tris.Add(o + 7); tris.Add(1);
                }
            }
            int vo = verts.Count;

            // base
            verts.Add(new Vector3(0, 0, 0));
            verts.Add(new Vector3(1, 0, 0));
            verts.Add(new Vector3(1, 0, 1));
            verts.Add(new Vector3(0, 0, 1));
            // back
            verts.Add(new Vector3(0, 0, 0));
            verts.Add(new Vector3(0, 1, 0));
            verts.Add(new Vector3(1, 1, 0));
            verts.Add(new Vector3(1, 0, 0));

            // base
            uvs.Add(new Vector2(1, 0));
            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(0, 1));
            uvs.Add(new Vector2(1, 1));
            // back
            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(0, 1));
            uvs.Add(new Vector2(1, 1));
            uvs.Add(new Vector2(1, 0));

            // base
            tris.Add(vo); tris.Add(vo + 1); tris.Add(vo + 2);
            tris.Add(vo); tris.Add(vo + 2); tris.Add(vo + 3);
            // back
            tris.Add(vo + 4); tris.Add(vo + 5); tris.Add(vo + 6);
            tris.Add(vo + 4); tris.Add(vo + 6); tris.Add(vo + 7);


            mesh.SetVertices(verts.Select(v => v - new Vector3(0.5f, 0.5f, 0.5f)).ToList());
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);

            mesh.RecalculateNormals();
            // fix the slope normals
            Vector3[] normals = mesh.normals;
            for (int i = 0; i < curvature + 1; i++)
            {
                int index = (4 * i) + 2;
                float angle = i * (Mathf.PI / 2) / curvature;

                normals[index] = new Vector3(0, Mathf.Cos(angle), Mathf.Sin(angle));
                normals[index + 1] = new Vector3(0, Mathf.Cos(angle), Mathf.Sin(angle));
            }
            mesh.normals = normals;

            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
