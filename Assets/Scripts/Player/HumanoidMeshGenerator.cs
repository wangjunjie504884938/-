using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates a single-mesh humanoid character with proper body proportions.
/// All body parts are part of one continuous mesh — not assembled from separate primitives.
/// Produces a low-poly humanoid silhouette that actually looks like a person.
/// </summary>
public static class HumanoidMeshGenerator
{
    /// <summary>
    /// Creates a single unified humanoid mesh with body, head, arms, legs.
    /// Uses a ring-based extrusion approach — like a 3D lathe/extrude.
    /// </summary>
    public static Mesh CreateHumanoidMesh()
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        var colors = new List<Color>();

        // Define body cross-section rings: (y, radius, color)
        // This creates a humanoid silhouette from feet to head
        var rings = new List<(float y, float rx, float rz, Color c)>
        {
            // Legs (feet to knees)
            (-1.0f, 0.10f, 0.16f, new Color(0.26f, 0.15f, 0.07f)),  // feet
            (-0.95f, 0.08f, 0.12f, new Color(0.26f, 0.15f, 0.07f)),
            (-0.5f, 0.07f, 0.07f, new Color(0.35f, 0.18f, 0.10f)),  // shins
            (-0.2f, 0.08f, 0.08f, new Color(0.40f, 0.20f, 0.12f)),  // knees
            // Hips
            (0.0f, 0.14f, 0.10f, new Color(0.48f, 0.08f, 0.06f)),
            // Waist
            (0.15f, 0.12f, 0.09f, new Color(0.55f, 0.10f, 0.08f)),
            // Chest (wider)
            (0.35f, 0.18f, 0.12f, new Color(0.70f, 0.16f, 0.14f)),
            (0.55f, 0.17f, 0.11f, new Color(0.70f, 0.16f, 0.14f)),
            // Shoulders
            (0.70f, 0.22f, 0.14f, new Color(0.55f, 0.57f, 0.62f)),
            // Neck
            (0.82f, 0.06f, 0.06f, new Color(0.78f, 0.62f, 0.48f)),
            // Head base
            (0.92f, 0.13f, 0.13f, new Color(0.88f, 0.72f, 0.56f)),
            // Head top
            (1.15f, 0.14f, 0.14f, new Color(0.88f, 0.72f, 0.56f)),
            (1.25f, 0.10f, 0.10f, new Color(0.88f, 0.72f, 0.56f)),
            (1.30f, 0.05f, 0.05f, new Color(0.88f, 0.72f, 0.56f)),
        };

        int segments = 8; // low-poly ring segments

        // Generate ring vertices
        for (int r = 0; r < rings.Count; r++)
        {
            float y = rings[r].y;
            float rx = rings[r].rx;
            float rz = rings[r].rz;
            Color c = rings[r].c;

            for (int s = 0; s < segments; s++)
            {
                float angle = (float)s / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(angle) * rx, y, Mathf.Sin(angle) * rz));
                colors.Add(c);
            }
        }

        // Connect rings with quads (2 triangles each)
        for (int r = 0; r < rings.Count - 1; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int s2 = (s + 1) % segments;
                int a = r * segments + s;
                int b = r * segments + s2;
                int c = (r + 1) * segments + s;
                int d = (r + 1) * segments + s2;

                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        }

        // Cap bottom (feet)
        int bottomCenter = verts.Count;
        verts.Add(new Vector3(0, -1.05f, 0));
        colors.Add(rings[0].c);
        for (int s = 0; s < segments; s++)
        {
            int s2 = (s + 1) % segments;
            tris.Add(s2); tris.Add(s); tris.Add(bottomCenter);
        }

        // Cap top (head)
        int topCenter = verts.Count;
        verts.Add(new Vector3(0, 1.35f, 0));
        colors.Add(rings[rings.Count - 1].c);
        for (int s = 0; s < segments; s++)
        {
            int s2 = (s + 1) % segments;
            int idx = (rings.Count - 1) * segments;
            tris.Add(idx + s); tris.Add(idx + s2); tris.Add(topCenter);
        }

        // === Arms (separate meshes attached as children) ===
        // Left arm
        AddArm(verts, tris, colors, new Vector3(-0.25f, 0.65f, 0), segments);
        // Right arm
        AddArm(verts, tris, colors, new Vector3(0.25f, 0.65f, 0), segments);

        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetColors(colors);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddArm(List<Vector3> verts, List<int> tris, List<Color> colors, Vector3 shoulder, int segments)
    {
        float sign = shoulder.x < 0 ? -1 : 1;
        int startIdx = verts.Count;

        // Arm rings: upper arm → elbow → forearm → hand
        var armRings = new List<(float yOffset, float radius, Color c)>
        {
            (0f, 0.06f, new Color(0.55f, 0.57f, 0.62f)),     // shoulder
            (-0.15f, 0.055f, new Color(0.48f, 0.08f, 0.06f)), // upper arm
            (-0.35f, 0.05f, new Color(0.48f, 0.08f, 0.06f)),   // elbow
            (-0.55f, 0.045f, new Color(0.48f, 0.08f, 0.06f)),  // forearm
            (-0.65f, 0.06f, new Color(0.55f, 0.57f, 0.62f)),  // hand
        };

        for (int r = 0; r < armRings.Count; r++)
        {
            float y = shoulder.y + armRings[r].yOffset;
            float radius = armRings[r].radius;
            for (int s = 0; s < segments; s++)
            {
                float angle = (float)s / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(shoulder.x + Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius));
                colors.Add(armRings[r].c);
            }
        }

        for (int r = 0; r < armRings.Count - 1; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int s2 = (s + 1) % segments;
                int a = startIdx + r * segments + s;
                int b = startIdx + r * segments + s2;
                int c = startIdx + (r + 1) * segments + s;
                int d = startIdx + (r + 1) * segments + s2;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        }

        // Cap hand
        int handCap = verts.Count;
        verts.Add(new Vector3(shoulder.x, shoulder.y - 0.70f, 0));
        colors.Add(armRings[armRings.Count - 1].c);
        for (int s = 0; s < segments; s++)
        {
            int s2 = (s + 1) % segments;
            int idx = startIdx + (armRings.Count - 1) * segments;
            tris.Add(idx + s); tris.Add(idx + s2); tris.Add(handCap);
        }
    }

    /// <summary>
    /// Creates a helmet mesh that fits on the head.
    /// </summary>
    public static Mesh CreateHelmetMesh()
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        int segments = 8;

        // Helmet dome
        var rings = new List<(float y, float r)>
        {
            (0.95f, 0.15f),
            (1.05f, 0.16f),
            (1.15f, 0.15f),
            (1.22f, 0.12f),
            (1.26f, 0.08f),
        };

        for (int r = 0; r < rings.Count; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                float angle = (float)s / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(angle) * rings[r].r, rings[r].y, Mathf.Sin(angle) * rings[r].r));
            }
        }

        for (int r = 0; r < rings.Count - 1; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int s2 = (s + 1) % segments;
                int a = r * segments + s;
                int b = r * segments + s2;
                int c = (r + 1) * segments + s;
                int d = (r + 1) * segments + s2;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        }

        // Top cap
        int top = verts.Count;
        verts.Add(new Vector3(0, 1.28f, 0));
        for (int s = 0; s < segments; s++)
        {
            int s2 = (s + 1) % segments;
            int idx = (rings.Count - 1) * segments;
            tris.Add(idx + s); tris.Add(idx + s2); tris.Add(top);
        }

        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        return mesh;
    }

    /// <summary>
    /// Creates a simple sword mesh.
    /// </summary>
    public static Mesh CreateSwordMesh()
    {
        var verts = new List<Vector3>
        {
            // Blade (flat quad extruded)
            new Vector3(-0.02f, 0.0f, -0.03f),
            new Vector3(0.02f, 0.0f, -0.03f),
            new Vector3(0.02f, 0.0f, 0.03f),
            new Vector3(-0.02f, 0.0f, 0.03f),
            new Vector3(-0.015f, 0.5f, -0.02f),
            new Vector3(0.015f, 0.5f, -0.02f),
            new Vector3(0.015f, 0.5f, 0.02f),
            new Vector3(-0.015f, 0.5f, 0.02f),
            new Vector3(0.0f, 0.55f, 0.0f), // tip
            // Guard
            new Vector3(-0.08f, -0.02f, -0.02f),
            new Vector3(0.08f, -0.02f, -0.02f),
            new Vector3(0.08f, -0.02f, 0.02f),
            new Vector3(-0.08f, -0.02f, 0.02f),
            new Vector3(-0.08f, 0.0f, -0.02f),
            new Vector3(0.08f, 0.0f, -0.02f),
            new Vector3(0.08f, 0.0f, 0.02f),
            new Vector3(-0.08f, 0.0f, 0.02f),
            // Handle
            new Vector3(-0.02f, -0.02f, -0.02f),
            new Vector3(0.02f, -0.02f, -0.02f),
            new Vector3(0.02f, -0.02f, 0.02f),
            new Vector3(-0.02f, -0.02f, 0.02f),
            new Vector3(-0.015f, -0.12f, -0.015f),
            new Vector3(0.015f, -0.12f, -0.015f),
            new Vector3(0.015f, -0.12f, 0.015f),
            new Vector3(-0.015f, -0.12f, 0.015f),
        };

        var tris = new List<int>
        {
            // Blade sides
            0,4,1, 1,4,5,  1,5,2, 2,5,6,  2,6,3, 3,6,7,  3,7,0, 0,7,4,
            // Blade top (to tip)
            4,8,5, 5,8,6, 6,8,7, 7,8,4,
            // Guard
            9,13,10, 10,13,14,  10,14,11, 11,14,15,  11,15,12, 12,15,9,  12,9,13, 9,15,14,
            // Handle
            16,20,17, 17,20,21,  17,21,18, 18,21,22,  18,22,19, 19,22,23,  19,23,16, 16,23,20,
        };

        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        return mesh;
    }
}
