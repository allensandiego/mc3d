using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedurally builds an original 3D ghost body mesh (smooth dome + scalloped wavy skirt).
/// </summary>
public static class GhostModelMesh
{
    public static Mesh CreateGhostBody(float radius = 0.43f, int segments = 28, int domeRings = 10, int skirtRings = 8)
    {
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris = new List<int>();

        float domeBaseY = 0.08f;
        float skirtBottomBaseY = -0.46f;
        float waveAmp = 0.07f;
        int waveCount = 5;

        // 1. Top hemisphere dome (phi: 0 -> PI/2)
        for (int r = 0; r <= domeRings; r++)
        {
            float phi = Mathf.Lerp(0f, Mathf.PI * 0.5f, (float)r / domeRings);
            float y = domeBaseY + Mathf.Cos(phi) * radius;
            float ringR = Mathf.Sin(phi) * radius;

            for (int s = 0; s <= segments; s++)
            {
                float theta = (float)s / segments * Mathf.PI * 2f;
                float x = Mathf.Cos(theta) * ringR;
                float z = Mathf.Sin(theta) * ringR;
                Vector3 pos = new Vector3(x, y, z);
                Vector3 n = new Vector3(x, Mathf.Cos(phi) * radius, z);
                verts.Add(pos);
                norms.Add(n.sqrMagnitude > 0.0001f ? n.normalized : Vector3.up);
            }
        }

        // 2. Cylindrical skirt down to wavy bottom
        for (int r = 1; r <= skirtRings; r++)
        {
            float t = (float)r / skirtRings;
            for (int s = 0; s <= segments; s++)
            {
                float theta = (float)s / segments * Mathf.PI * 2f;
                float x = Mathf.Cos(theta) * radius;
                float z = Mathf.Sin(theta) * radius;
                float bottomY = skirtBottomBaseY + Mathf.Sin(theta * waveCount) * waveAmp;
                float y = Mathf.Lerp(domeBaseY, bottomY, t);

                verts.Add(new Vector3(x, y, z));
                norms.Add(new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta)));
            }
        }

        int totalRings = domeRings + skirtRings;
        int stride = segments + 1;
        for (int r = 0; r < totalRings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int i0 = r * stride + s;
                int i1 = i0 + 1;
                int i2 = (r + 1) * stride + s;
                int i3 = i2 + 1;

                // Clockwise outward winding in Unity left-handed coords
                tris.Add(i0);
                tris.Add(i1);
                tris.Add(i2);

                tris.Add(i1);
                tris.Add(i3);
                tris.Add(i2);
            }
        }

        // 3. Bottom cap so skirt never looks hollow
        int centerIdx = verts.Count;
        verts.Add(new Vector3(0f, skirtBottomBaseY, 0f));
        norms.Add(Vector3.down);

        int bottomRingStart = verts.Count;
        for (int s = 0; s <= segments; s++)
        {
            float theta = (float)s / segments * Mathf.PI * 2f;
            float x = Mathf.Cos(theta) * radius;
            float z = Mathf.Sin(theta) * radius;
            float bottomY = skirtBottomBaseY + Mathf.Sin(theta * waveCount) * waveAmp;
            verts.Add(new Vector3(x, bottomY, z));
            norms.Add(Vector3.down);
        }

        for (int s = 0; s < segments; s++)
        {
            tris.Add(centerIdx);
            tris.Add(bottomRingStart + s + 1);
            tris.Add(bottomRingStart + s);
        }

        Mesh mesh = new Mesh { name = "GhostBody3D" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
