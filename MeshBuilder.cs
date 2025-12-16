using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KarlsonMapEditor
{
    public static class GizmoMeshBuilder
    {
        public static Shader gizmoShader;

        public static GameObject GetAxisGO(LevelEditor.GizmoMode mode)
        {
            Mesh mesh = AxisMeshes[(int)mode];
            GameObject go = new GameObject();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().material = new Material(gizmoShader);
            return go;
        }

        public static readonly Mesh[] AxisMeshes = {
            GetAxisMesh(LevelEditor.GizmoMode.Translate),
            GetAxisMesh(LevelEditor.GizmoMode.Scale, headSize:0.15f),
            GetAxisMesh(LevelEditor.GizmoMode.Rotate, headPolygon:48, headSize:0.07f)
        };

        private static Mesh GetAxisMesh(LevelEditor.GizmoMode mode, float headSize=0.2f, int headPolygon=24, float rodRadius=0.01f, int rodPolygon=8)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            
            if (mode == LevelEditor.GizmoMode.Rotate)
            {
                int maxVertex = headPolygon * 4;
                float innerRadius = 1f - headSize;
                for (int i = 0; i < headPolygon; i++)
                {
                    float angle = i * Mathf.PI * 2 / headPolygon;

                    // frontside and backside vertices so normals dont get messed up
                    for (int j = 0; j < 2; j++)
                    {
                        // outside
                        vertices.Add(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0));
                        // inside
                        vertices.Add(new Vector3(innerRadius * Mathf.Cos(angle), innerRadius * Mathf.Sin(angle), 0));
                    }
                }

                // tris
                for (int i = 0; i < headPolygon; i++)
                {
                    // tri 1
                    triangles.Add(4 * i);
                    triangles.Add((4 * i + 5) % maxVertex);
                    triangles.Add(4 * i + 1);
                    // backside
                    triangles.Add(4 * i + 2);
                    triangles.Add(4 * i + 3);
                    triangles.Add((4 * i + 7) % maxVertex);

                    // tri 2
                    triangles.Add(4 * i);
                    triangles.Add((4 * i + 4) % maxVertex);
                    triangles.Add((4 * i + 5) % maxVertex);
                    // backside
                    triangles.Add(4 * i + 2);
                    triangles.Add((4 * i + 7) % maxVertex);
                    triangles.Add((4 * i + 6) % maxVertex);
                }
            }
            else
            {
                float rodLength = 1f - headSize;
                float halfHeadSize = headSize * 0.5f;
                int headVertex = rodPolygon * 2; // index where the head vertices start

                for (int i = 0; i < rodPolygon; i++)
                {
                    float angle = i * Mathf.PI * 2 / rodPolygon;
                    // rod vertices
                    vertices.Add(new Vector3(rodRadius * Mathf.Cos(angle), rodRadius * Mathf.Sin(angle), 0));
                    vertices.Add(new Vector3(rodRadius * Mathf.Cos(angle), rodRadius * Mathf.Sin(angle), rodLength));
                }


                // create ring of quads
                for (int i = 0; i < rodPolygon; i++)
                {
                    // tri 1
                    triangles.Add(2 * i);
                    triangles.Add((2 * i + 3) % headVertex);
                    triangles.Add(2 * i + 1);

                    // tri 2
                    triangles.Add(2 * i);
                    triangles.Add((2 * i + 2) % headVertex);
                    triangles.Add((2 * i + 3) % headVertex);

                    // back tris
                    if (i >= 2)
                    {
                        triangles.Add(0);
                        triangles.Add(2 * i);
                        triangles.Add(2 * i - 2);
                    }
                }


                if (mode == LevelEditor.GizmoMode.Scale)
                {
                    // make a cube head
                    vertices.Add(new Vector3(-halfHeadSize, -halfHeadSize, rodLength));
                    vertices.Add(new Vector3(-halfHeadSize, -halfHeadSize, 1));

                    vertices.Add(new Vector3(halfHeadSize, -halfHeadSize, rodLength));
                    vertices.Add(new Vector3(halfHeadSize, -halfHeadSize, 1));

                    vertices.Add(new Vector3(halfHeadSize, halfHeadSize, rodLength));
                    vertices.Add(new Vector3(halfHeadSize, halfHeadSize, 1));

                    vertices.Add(new Vector3(-halfHeadSize, halfHeadSize, rodLength));
                    vertices.Add(new Vector3(-halfHeadSize, halfHeadSize, 1));

                    // around the side
                    for (int i = 0; i < 4; i++)
                    {
                        // tri 1
                        triangles.Add(headVertex + 2 * i);
                        triangles.Add(headVertex + (2 * i + 3) % 8);
                        triangles.Add(headVertex + 2 * i + 1);

                        // tri 2
                        triangles.Add(headVertex + 2 * i);
                        triangles.Add(headVertex + (2 * i + 2) % 8);
                        triangles.Add(headVertex + (2 * i + 3) % 8);
                    }
                    // front and back
                    triangles.Add(headVertex); triangles.Add(headVertex + 4); triangles.Add(headVertex + 2);
                    triangles.Add(headVertex); triangles.Add(headVertex + 6); triangles.Add(headVertex + 4);
                    triangles.Add(headVertex + 1); triangles.Add(headVertex + 3); triangles.Add(headVertex + 5);
                    triangles.Add(headVertex + 1); triangles.Add(headVertex + 5); triangles.Add(headVertex + 7);

                }
                else if (mode == LevelEditor.GizmoMode.Translate)
                {
                    // make a cone head

                    // vertices
                    vertices.Add(new Vector3(0, 0, 1));
                    for (int i = 0; i < headPolygon; i++)
                    {
                        float angle = i * Mathf.PI * 2 / headPolygon;
                        vertices.Add(new Vector3(halfHeadSize * Mathf.Cos(angle), halfHeadSize * Mathf.Sin(angle), rodLength));
                    }

                    // tris
                    for (int i = 0; i < headPolygon; i++)
                    {
                        // angled tris
                        triangles.Add(headVertex);
                        triangles.Add(headVertex + 1 + i);
                        triangles.Add(headVertex + 1 + ((i + 1) % headPolygon));

                        // back tris
                        if (i >= 2)
                        {
                            triangles.Add(headVertex + 1);
                            triangles.Add(headVertex + 1 + i);
                            triangles.Add(headVertex + i);
                        }
                    }
                }
            }

            Mesh mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            return mesh;
        }
    }
}
