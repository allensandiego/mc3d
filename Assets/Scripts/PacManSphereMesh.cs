using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedurally builds a 3D chomping Pac-Man: a solid back half-sphere plus upper/lower front jaw wedges.
/// </summary>
public static class PacManSphereMesh
{
    public static Mesh CreateBackHalfSphere(float radius, int segments = 16, int rings = 16)
    {
        // theta in [PI/2, 3*PI/2] -> x <= 0
        return CreateSphericalSector(radius, 0f, Mathf.PI, Mathf.PI * 0.5f, Mathf.PI * 1.5f, segments, rings, "PacMan_BackDome", false, false);
    }

    public static Mesh CreateFrontQuarterJaw(float radius, bool upper, int segments = 12, int rings = 10)
    {
        // theta in [-PI/2, +PI/2] -> x >= 0
        float startPhi = upper ? 0f : Mathf.PI * 0.5f;
        float endPhi = upper ? Mathf.PI * 0.5f : Mathf.PI;
        return CreateSphericalSector(radius, startPhi, endPhi, -Mathf.PI * 0.5f, Mathf.PI * 0.5f, segments, rings,
            upper ? "PacMan_UpperJaw" : "PacMan_LowerJaw", upper, !upper);
    }

    private static Mesh CreateSphericalSector(
        float radius,
        float startPhi,
        float endPhi,
        float startTheta,
        float endTheta,
        int segments,
        int rings,
        string meshName,
        bool addBottomMouthCap,
        bool addTopMouthCap)
    {
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris = new List<int>();

        for (int r = 0; r <= rings; r++)
        {
            float phi = Mathf.Lerp(startPhi, endPhi, (float)r / rings);
            float y = Mathf.Cos(phi) * radius;
            float ringR = Mathf.Sin(phi) * radius;

            for (int s = 0; s <= segments; s++)
            {
                float theta = Mathf.Lerp(startTheta, endTheta, (float)s / segments);
                Vector3 pos = new Vector3(Mathf.Cos(theta) * ringR, y, Mathf.Sin(theta) * ringR);
                verts.Add(pos);
                norms.Add(pos.sqrMagnitude > 0.0001f ? pos.normalized : Vector3.up);
            }
        }

        int stride = segments + 1;
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int i0 = r * stride + s;
                int i1 = i0 + 1;
                int i2 = (r + 1) * stride + s;
                int i3 = i2 + 1;

                tris.Add(i0);
                tris.Add(i1);
                tris.Add(i2);

                tris.Add(i1);
                tris.Add(i3);
                tris.Add(i2);
            }
        }

        // Flat inner mouth cap at y = 0 for front jaws
        if (addBottomMouthCap || addTopMouthCap)
        {
            Vector3 capNormal = addBottomMouthCap ? Vector3.down : Vector3.up;
            int centerIdx = verts.Count;
            verts.Add(Vector3.zero);
            norms.Add(capNormal);

            int ringStart = verts.Count;
            for (int s = 0; s <= segments; s++)
            {
                float theta = Mathf.Lerp(startTheta, endTheta, (float)s / segments);
                verts.Add(new Vector3(Mathf.Cos(theta) * radius, 0f, Mathf.Sin(theta) * radius));
                norms.Add(capNormal);
            }

            for (int s = 0; s < segments; s++)
            {
                if (addBottomMouthCap)
                {
                    tris.Add(centerIdx);
                    tris.Add(ringStart + s + 1);
                    tris.Add(ringStart + s);
                }
                else
                {
                    tris.Add(centerIdx);
                    tris.Add(ringStart + s);
                    tris.Add(ringStart + s + 1);
                }
            }
        }

        Mesh mesh = new Mesh { name = meshName };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
