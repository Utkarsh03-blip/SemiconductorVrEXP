using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PNJunctionLab.Editor
{
    /// <summary>
    /// Builds and persists reusable low-poly procedural Mesh assets under Assets/PNJunctionLab/Meshes/
    /// for laboratory props and controls (notched Si wafers, chamfered bezels/caps, knurled dials,
    /// domed LEDs, crystal blocks, and hex nuts) keeping Quest 2 triangle budgets minimal.
    /// </summary>
    public static class ProceduralHardwareMeshFactory
    {
        public const string MeshFolder = "Assets/PNJunctionLab/Meshes";

        public sealed class HardwareMeshSet
        {
            public Mesh SiliconWafer;
            public Mesh ChamferedBezel;
            public Mesh RoundedCap;
            public Mesh KnurledDial;
            public Mesh DomedLed;
            public Mesh RoundedBlock;
            public Mesh HexNut;
            public Mesh CrystalShard;
            public Mesh CoilRing;
        }

        /// <summary>
        /// Generates or refreshes all procedural hardware Mesh assets on disk and returns loaded references.
        /// </summary>
        public static HardwareMeshSet EnsureAllMeshes()
        {
            if (!Directory.Exists(MeshFolder))
            {
                Directory.CreateDirectory(MeshFolder);
            }

            HardwareMeshSet set = new HardwareMeshSet
            {
                SiliconWafer = SaveMesh(BuildSiliconWaferMesh(), $"{MeshFolder}/Mesh_SiliconWafer.asset"),
                ChamferedBezel = SaveMesh(BuildOctagonPrismMesh("Mesh_ChamferedBezel", 0.08f, 0.06f, 0.16f), $"{MeshFolder}/Mesh_ChamferedBezel.asset"),
                RoundedCap = SaveMesh(BuildOctagonPrismMesh("Mesh_RoundedCap", 0.11f, 0.08f, 0.22f), $"{MeshFolder}/Mesh_RoundedCap.asset"),
                KnurledDial = SaveMesh(BuildKnurledDialMesh(), $"{MeshFolder}/Mesh_KnurledDial.asset"),
                DomedLed = SaveMesh(BuildDomedLedMesh(), $"{MeshFolder}/Mesh_DomedLed.asset"),
                RoundedBlock = SaveMesh(BuildOctagonPrismMesh("Mesh_RoundedBlock", 0.07f, 0.045f, 0.10f), $"{MeshFolder}/Mesh_RoundedBlock.asset"),
                HexNut = SaveMesh(BuildHexNutMesh(), $"{MeshFolder}/Mesh_HexNut.asset"),
                CrystalShard = SaveMesh(BuildCrystalShardMesh(), $"{MeshFolder}/Mesh_CrystalShard.asset"),
                CoilRing = SaveMesh(BuildCoilRingMesh(), $"{MeshFolder}/Mesh_CoilRing.asset"),
            };

            AssetDatabase.SaveAssets();
            return set;
        }

        private static Mesh SaveMesh(Mesh mesh, string assetPath)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, assetPath);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>
        /// Normalized [-0.5..+0.5] cylindrical silicon wafer with SEMI primary orientation flat
        /// along -Z (z >= -0.43), beveled upper/lower rims, and planar X-Z cap UVs so micro-die grids stay sharp.
        /// </summary>
        private static Mesh BuildSiliconWaferMesh()
        {
            const int segs = 40;
            const float outerR = 0.50f;
            const float innerR = 0.472f;
            const float flatZ = -0.432f;

            Vector2[] ring = new Vector2[segs];
            for (int i = 0; i < segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                float x = Mathf.Sin(a) * outerR;
                float z = Mathf.Cos(a) * outerR;
                if (z < flatZ)
                {
                    z = flatZ;
                }
                ring[i] = new Vector2(x, z);
            }

            List<Vector3> verts = new List<Vector3>(segs * 10);
            List<Vector2> uvs = new List<Vector2>(segs * 10);
            List<int> tris = new List<int>(segs * 24);

            // Top flat cap (+Y = +0.50, inset innerR/outerR factor)
            float scaleIn = innerR / outerR;
            int topCenter = verts.Count;
            verts.Add(new Vector3(0f, 0.50f, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));
            int topInnerStart = verts.Count;
            for (int i = 0; i < segs; i++)
            {
                Vector2 p = ring[i] * scaleIn;
                verts.Add(new Vector3(p.x, 0.50f, p.y));
                uvs.Add(new Vector2(p.x + 0.5f, p.y + 0.5f));
            }
            for (int i = 0; i < segs; i++)
            {
                int next = (i + 1) % segs;
                tris.Add(topCenter);
                tris.Add(topInnerStart + i);
                tris.Add(topInnerStart + next);
            }

            // Bottom flat cap (-Y = -0.50)
            int botCenter = verts.Count;
            verts.Add(new Vector3(0f, -0.50f, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));
            int botInnerStart = verts.Count;
            for (int i = 0; i < segs; i++)
            {
                Vector2 p = ring[i] * scaleIn;
                verts.Add(new Vector3(p.x, -0.50f, p.y));
                uvs.Add(new Vector2(p.x + 0.5f, p.y + 0.5f));
            }
            for (int i = 0; i < segs; i++)
            {
                int next = (i + 1) % segs;
                tris.Add(botCenter);
                tris.Add(botInnerStart + next);
                tris.Add(botInnerStart + i);
            }

            // Outer bevel rings & cylindrical side wall split cleanly per quad for crisp normals
            AddProfileStrip(verts, uvs, tris, ring, segs, scaleIn, 0.50f, 1.0f, 0.34f);
            AddProfileStrip(verts, uvs, tris, ring, segs, 1.0f, 0.34f, 1.0f, -0.34f);
            AddProfileStrip(verts, uvs, tris, ring, segs, 1.0f, -0.34f, scaleIn, -0.50f);

            Mesh m = new Mesh { name = "Mesh_SiliconWafer" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        private static void AddProfileStrip(
            List<Vector3> verts, List<Vector2> uvs, List<int> tris,
            Vector2[] ring, int segs,
            float scaleTop, float yTop, float scaleBot, float yBot)
        {
            for (int i = 0; i < segs; i++)
            {
                int next = (i + 1) % segs;
                Vector2 p0Top = ring[i] * scaleTop;
                Vector2 p1Top = ring[next] * scaleTop;
                Vector2 p0Bot = ring[i] * scaleBot;
                Vector2 p1Bot = ring[next] * scaleBot;

                float u0 = i / (float)segs;
                float u1 = (i + 1) / (float)segs;

                int vIdx = verts.Count;
                verts.Add(new Vector3(p0Top.x, yTop, p0Top.y));
                verts.Add(new Vector3(p1Top.x, yTop, p1Top.y));
                verts.Add(new Vector3(p1Bot.x, yBot, p1Bot.y));
                verts.Add(new Vector3(p0Bot.x, yBot, p0Bot.y));

                uvs.Add(new Vector2(u0, 0.9f));
                uvs.Add(new Vector2(u1, 0.9f));
                uvs.Add(new Vector2(u1, 0.1f));
                uvs.Add(new Vector2(u0, 0.1f));

                tris.Add(vIdx + 0); tris.Add(vIdx + 2); tris.Add(vIdx + 1);
                tris.Add(vIdx + 0); tris.Add(vIdx + 3); tris.Add(vIdx + 2);
            }
        }

        /// <summary>
        /// Chamfered octagonal prism normalized to [-0.5..+0.5] in X, Y, Z with beveled top & bottom rims.
        /// Ideal drop-in upgrade for plain Cube housings, button caps, slider plates, and semiconductor blocks.
        /// </summary>
        private static Mesh BuildOctagonPrismMesh(string meshName, float cornerChamfer, float topInset, float topDropHeight)
        {
            float c = 0.5f;
            float e = 0.5f - cornerChamfer;
            Vector2[] outerPoly =
            {
                new Vector2(-e, +c), new Vector2(+e, +c),
                new Vector2(+c, +e), new Vector2(+c, -e),
                new Vector2(+e, -c), new Vector2(-e, -c),
                new Vector2(-c, -e), new Vector2(-c, +e)
            };

            float inScale = (0.5f - topInset) / 0.5f;
            Vector2[] innerPoly = new Vector2[8];
            for (int i = 0; i < 8; i++)
            {
                innerPoly[i] = outerPoly[i] * inScale;
            }

            List<Vector3> verts = new List<Vector3>(160);
            List<Vector2> uvs = new List<Vector2>(160);
            List<int> tris = new List<int>(240);

            // Top flat cap
            AddCapFan(verts, uvs, tris, innerPoly, 0.5f, true);
            // Bottom flat cap
            AddCapFan(verts, uvs, tris, innerPoly, -0.5f, false);

            float yTopShoulder = 0.5f - topDropHeight;
            float yBotShoulder = -0.5f + topDropHeight * 0.65f;

            // Top chamfer + vertical walls + bottom chamfer
            AddRingFaces(verts, uvs, tris, innerPoly, 0.5f, outerPoly, yTopShoulder);
            AddRingFaces(verts, uvs, tris, outerPoly, yTopShoulder, outerPoly, yBotShoulder);
            AddRingFaces(verts, uvs, tris, outerPoly, yBotShoulder, innerPoly, -0.5f);

            Mesh m = new Mesh { name = meshName };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        private static void AddCapFan(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector2[] poly, float y, bool facingUp)
        {
            int cIdx = verts.Count;
            verts.Add(new Vector3(0f, y, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));
            int start = verts.Count;
            for (int i = 0; i < poly.Length; i++)
            {
                verts.Add(new Vector3(poly[i].x, y, poly[i].y));
                uvs.Add(new Vector2(poly[i].x + 0.5f, poly[i].y + 0.5f));
            }

            for (int i = 0; i < poly.Length; i++)
            {
                int next = (i + 1) % poly.Length;
                if (facingUp)
                {
                    tris.Add(cIdx); tris.Add(start + i); tris.Add(start + next);
                }
                else
                {
                    tris.Add(cIdx); tris.Add(start + next); tris.Add(start + i);
                }
            }
        }

        private static void AddRingFaces(
            List<Vector3> verts, List<Vector2> uvs, List<int> tris,
            Vector2[] topPoly, float yTop, Vector2[] botPoly, float yBot)
        {
            int count = topPoly.Length;
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                Vector3 a = new Vector3(topPoly[i].x, yTop, topPoly[i].y);
                Vector3 b = new Vector3(topPoly[next].x, yTop, topPoly[next].y);
                Vector3 c = new Vector3(botPoly[next].x, yBot, botPoly[next].y);
                Vector3 d = new Vector3(botPoly[i].x, yBot, botPoly[i].y);

                int idx = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);

                // Clean planar face UVs based on dominant side direction so brushed metal textures tile cleanly.
                float spanX = Mathf.Abs(a.x - b.x);
                float spanZ = Mathf.Abs(a.z - b.z);
                float u0 = spanX >= spanZ ? a.x + 0.5f : a.z + 0.5f;
                float u1 = spanX >= spanZ ? b.x + 0.5f : b.z + 0.5f;
                uvs.Add(new Vector2(u0, yTop + 0.5f));
                uvs.Add(new Vector2(u1, yTop + 0.5f));
                uvs.Add(new Vector2(u1, yBot + 0.5f));
                uvs.Add(new Vector2(u0, yBot + 0.5f));

                tris.Add(idx + 0); tris.Add(idx + 2); tris.Add(idx + 1);
                tris.Add(idx + 0); tris.Add(idx + 3); tris.Add(idx + 2);
            }
        }

        /// <summary>
        /// 24-flute knurled rotary dial mesh normalized to Y in [-0.5..+0.5], outer radius 0.50 (grooves 0.455).
        /// </summary>
        private static Mesh BuildKnurledDialMesh()
        {
            const int toothCount = 24;
            int ringVerts = toothCount * 2;
            Vector2[] knurlRing = new Vector2[ringVerts];
            Vector2[] topSmoothRing = new Vector2[ringVerts];

            for (int i = 0; i < ringVerts; i++)
            {
                float a = (i / (float)ringVerts) * Mathf.PI * 2f;
                float rOuter = (i % 2 == 0) ? 0.50f : 0.452f;
                float rTop = 0.42f;
                knurlRing[i] = new Vector2(Mathf.Sin(a) * rOuter, Mathf.Cos(a) * rOuter);
                topSmoothRing[i] = new Vector2(Mathf.Sin(a) * rTop, Mathf.Cos(a) * rTop);
            }

            List<Vector3> verts = new List<Vector3>(ringVerts * 10);
            List<Vector2> uvs = new List<Vector2>(ringVerts * 10);
            List<int> tris = new List<int>(ringVerts * 24);

            // Top circular face (y = +0.5)
            AddCapFan(verts, uvs, tris, topSmoothRing, 0.50f, true);
            // Bottom base face (y = -0.5)
            AddCapFan(verts, uvs, tris, knurlRing, -0.50f, false);

            // Top bevel + vertical knurled wall (height 1.0 matches Unity Cylinder diameter/height ratio cleanly;
            // wait: Unity Cylinder height is 2.0 [-1..+1], whereas Scale in scene puts DialBody at lpos=lscale.y,
            // so matching [-1..+1] vertical bounds preserves exact visual height!)
            Mesh built = FinalizeCylinderExtentKnurled(verts, uvs, tris, topSmoothRing, knurlRing);
            return built;
        }

        private static Mesh FinalizeCylinderExtentKnurled(
            List<Vector3> verts, List<Vector2> uvs, List<int> tris,
            Vector2[] topSmoothRing, Vector2[] knurlRing)
        {
            // Adjust cap vertex Ys from [-0.5..0.5] to [-1.0..+1.0] to match Unity's built-in Cylinder height extent!
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 v = verts[i];
                v.y *= 2f;
                verts[i] = v;
            }

            AddRingFaces(verts, uvs, tris, topSmoothRing, 1.00f, knurlRing, 0.72f);
            AddRingFaces(verts, uvs, tris, knurlRing, 0.72f, knurlRing, -1.00f);

            Mesh m = new Mesh { name = "Mesh_KnurledDial" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// T-1 3/4 style flanged LED epoxy package dome normalized to radius 0.5, Y in [-0.5..+0.5].
        /// </summary>
        private static Mesh BuildDomedLedMesh()
        {
            const int segs = 20;
            const int domeRings = 6;
            List<Vector3> verts = new List<Vector3>(segs * 14);
            List<Vector2> uvs = new List<Vector2>(segs * 14);
            List<int> tris = new List<int>(segs * 30);

            Vector2[] unitCircle = new Vector2[segs];
            for (int i = 0; i < segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                unitCircle[i] = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
            }

            Vector2[] flangeRing = ScaleRing(unitCircle, 0.50f);
            Vector2[] barrelRing = ScaleRing(unitCircle, 0.40f);

            // Base disk
            AddCapFan(verts, uvs, tris, flangeRing, -0.50f, false);
            // Flange side & shoulder
            AddRingFaces(verts, uvs, tris, flangeRing, -0.34f, flangeRing, -0.50f);
            AddRingFaces(verts, uvs, tris, barrelRing, -0.34f, flangeRing, -0.34f);
            // Cylinder barrel (y = -0.34 .. +0.10)
            AddRingFaces(verts, uvs, tris, barrelRing, 0.10f, barrelRing, -0.34f);

            // Hemispherical top dome (y = +0.10 .. +0.50, radius = 0.40)
            Vector2[] prevRing = barrelRing;
            float prevY = 0.10f;
            for (int r = 1; r <= domeRings; r++)
            {
                float phi = (r / (float)domeRings) * (Mathf.PI * 0.5f);
                float rad = Mathf.Cos(phi) * 0.40f;
                float curY = 0.10f + Mathf.Sin(phi) * 0.40f;
                if (r == domeRings)
                {
                    // Pole cap fan
                    int poleIdx = verts.Count;
                    verts.Add(new Vector3(0f, 0.50f, 0f));
                    uvs.Add(new Vector2(0.5f, 0.5f));
                    int ringStart = verts.Count;
                    for (int i = 0; i < segs; i++)
                    {
                        verts.Add(new Vector3(prevRing[i].x, prevY, prevRing[i].y));
                        uvs.Add(new Vector2(prevRing[i].x + 0.5f, prevRing[i].y + 0.5f));
                    }
                    for (int i = 0; i < segs; i++)
                    {
                        int next = (i + 1) % segs;
                        tris.Add(poleIdx); tris.Add(ringStart + i); tris.Add(ringStart + next);
                    }
                }
                else
                {
                    Vector2[] curRing = ScaleRing(unitCircle, rad);
                    AddRingFaces(verts, uvs, tris, curRing, curY, prevRing, prevY);
                    prevRing = curRing;
                    prevY = curY;
                }
            }

            Mesh m = new Mesh { name = "Mesh_DomedLed" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        private static Mesh BuildHexNutMesh()
        {
            const int sides = 6;
            Vector2[] hex = new Vector2[sides];
            Vector2[] hexTop = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = (i / (float)sides) * Mathf.PI * 2f;
                hex[i] = new Vector2(Mathf.Sin(a) * 0.50f, Mathf.Cos(a) * 0.50f);
                hexTop[i] = new Vector2(Mathf.Sin(a) * 0.42f, Mathf.Cos(a) * 0.42f);
            }

            List<Vector3> verts = new List<Vector3>(48);
            List<Vector2> uvs = new List<Vector2>(48);
            List<int> tris = new List<int>(72);
            AddCapFan(verts, uvs, tris, hexTop, 0.50f, true);
            AddCapFan(verts, uvs, tris, hex, -0.50f, false);
            AddRingFaces(verts, uvs, tris, hexTop, 0.50f, hex, 0.30f);
            AddRingFaces(verts, uvs, tris, hex, 0.30f, hex, -0.50f);

            Mesh m = new Mesh { name = "Mesh_HexNut" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// Faceted hexagonal quartz crystal shard prism (Y in [0..1], base radius 0.50 tapering to a pointed apex).
        /// Flat-shaded per-facet triangles produce crisp mineral crystal reflections.
        /// </summary>
        private static Mesh BuildCrystalShardMesh()
        {
            const int sides = 6;
            Vector2[] baseRing = new Vector2[sides];
            Vector2[] shoulderRing = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = (i / (float)sides) * Mathf.PI * 2f;
                float rMul = (i % 2 == 0) ? 1.0f : 0.88f; // subtle natural asymmetric mineral profile
                baseRing[i] = new Vector2(Mathf.Sin(a) * 0.50f * rMul, Mathf.Cos(a) * 0.50f * rMul);
                shoulderRing[i] = new Vector2(Mathf.Sin(a) * 0.41f * rMul, Mathf.Cos(a) * 0.41f * rMul);
            }

            List<Vector3> verts = new List<Vector3>(80);
            List<Vector2> uvs = new List<Vector2>(80);
            List<int> tris = new List<int>(90);

            AddCapFan(verts, uvs, tris, baseRing, 0f, false);
            AddRingFaces(verts, uvs, tris, shoulderRing, 0.66f, baseRing, 0f);

            // Chiseled hexagonal pyramidal crown converging cleanly to slightly off-center crystal apex
            Vector3 apex = new Vector3(0.04f, 1.00f, -0.03f);
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                Vector3 v0 = new Vector3(shoulderRing[i].x, 0.66f, shoulderRing[i].y);
                Vector3 v1 = new Vector3(shoulderRing[next].x, 0.66f, shoulderRing[next].y);

                int idx = verts.Count;
                verts.Add(apex);
                verts.Add(v0);
                verts.Add(v1);
                uvs.Add(new Vector2(0.5f, 1f));
                uvs.Add(new Vector2(0f, 0.66f));
                uvs.Add(new Vector2(1f, 0.66f));

                // Matches top cap CW outward normal winding (apex -> i -> next)
                tris.Add(idx + 0);
                tris.Add(idx + 1);
                tris.Add(idx + 2);
            }

            Mesh m = new Mesh { name = "Mesh_CrystalShard" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// Low-poly torus heating coil ring lying in the Y-Z plane (wrapping around an X-axis process tube).
        /// Major radius = 0.50, minor tube radius = 0.068.
        /// </summary>
        private static Mesh BuildCoilRingMesh()
        {
            const int majorSegs = 20;
            const int minorSegs = 8;
            const float R = 0.50f;
            const float r = 0.068f;

            List<Vector3> verts = new List<Vector3>(majorSegs * minorSegs * 4);
            List<Vector2> uvs = new List<Vector2>(majorSegs * minorSegs * 4);
            List<int> tris = new List<int>(majorSegs * minorSegs * 6);

            Vector3[,] ringPts = new Vector3[majorSegs, minorSegs];
            for (int i = 0; i < majorSegs; i++)
            {
                float theta = (i / (float)majorSegs) * Mathf.PI * 2f;
                Vector3 radDir = new Vector3(0f, Mathf.Sin(theta), Mathf.Cos(theta));
                Vector3 center = radDir * R;
                for (int j = 0; j < minorSegs; j++)
                {
                    float phi = (j / (float)minorSegs) * Mathf.PI * 2f;
                    Vector3 offset = Vector3.right * (Mathf.Sin(phi) * r) + radDir * (Mathf.Cos(phi) * r);
                    ringPts[i, j] = center + offset;
                }
            }

            for (int i = 0; i < majorSegs; i++)
            {
                int iNext = (i + 1) % majorSegs;
                for (int j = 0; j < minorSegs; j++)
                {
                    int jNext = (j + 1) % minorSegs;
                    Vector3 a = ringPts[i, j];
                    Vector3 b = ringPts[iNext, j];
                    Vector3 c = ringPts[iNext, jNext];
                    Vector3 d = ringPts[i, jNext];

                    int idx = verts.Count;
                    verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                    uvs.Add(new Vector2(i / (float)majorSegs, j / (float)minorSegs));
                    uvs.Add(new Vector2((i + 1) / (float)majorSegs, j / (float)minorSegs));
                    uvs.Add(new Vector2((i + 1) / (float)majorSegs, (j + 1) / (float)minorSegs));
                    uvs.Add(new Vector2(i / (float)majorSegs, (j + 1) / (float)minorSegs));

                    tris.Add(idx + 0); tris.Add(idx + 2); tris.Add(idx + 1);
                    tris.Add(idx + 0); tris.Add(idx + 3); tris.Add(idx + 2);
                }
            }

            Mesh m = new Mesh { name = "Mesh_CoilRing" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        private static Vector2[] ScaleRing(Vector2[] src, float scale)
        {
            Vector2[] dst = new Vector2[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                dst[i] = src[i] * scale;
            }
            return dst;
        }
    }
}
