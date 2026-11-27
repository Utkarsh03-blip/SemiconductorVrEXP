using System;
using UnityEngine;

namespace PNJunctionLab
{
    /// <summary>
    /// Generates deterministic 512x512 procedural PBR albedo and tangent-space normal textures
    /// for laboratory hardware materials (silicon wafers, brushed metal, battery wrap, granular dopants,
    /// crystalline housings, and graduated control scales). Safe in both Editor and runtime.
    /// </summary>
    public static class ProceduralLabTextures
    {
        public const int DefaultSize = 512;

        /// <summary>
        /// Synthesizes a 512x512 iridescent monocrystalline silicon wafer albedo map
        /// featuring orthogonal micro-die photolithography grid lines and subtle concentric polish rings.
        /// White-balanced enough to also multiply cleanly with material tints on StageWaferSpecimen.
        /// </summary>
        public static Texture2D CreateSiliconWaferAlbedo(int size = DefaultSize)
        {
            Texture2D tex = CreateTexture(size, "Tex_SiliconWafer_Albedo", false);
            Color[] pixels = new Color[size * size];

            int diePitch = Mathf.Max(16, size / 14);
            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float angle = Mathf.Atan2(ny, nx);

                    // Thin-film interference shimmer: cool blue-silver core with subtle violet/cyan diffraction sheen.
                    float iridescence = 0.5f + 0.5f * Mathf.Sin(nx * 5.2f + ny * 4.1f + r * 3.5f);
                    float angularSheen = 0.5f + 0.5f * Mathf.Cos(angle * 2f + 0.7f);
                    Color coolBase = new Color(0.58f, 0.70f, 0.86f, 1f);
                    Color iridShift = new Color(0.76f, 0.88f, 0.98f, 1f);
                    Color violetTint = new Color(0.68f, 0.68f, 0.92f, 1f);
                    Color col = Color.Lerp(coolBase, iridShift, iridescence * 0.65f);
                    col = Color.Lerp(col, violetTint, angularSheen * 0.18f);

                    // Subtle concentric CMP polish rings.
                    float polish = Mathf.Sin(r * 96f) * 0.022f + Mathf.Sin(r * 210f) * 0.012f;
                    col.r += polish;
                    col.g += polish;
                    col.b += polish * 1.15f;

                    // Crisp orthogonal wafer scribe / micro-die streets.
                    int gx = x % diePitch;
                    int gy = y % diePitch;
                    bool majorStreet = gx == 0 || gy == 0 || gx == 1 || gy == 1;
                    bool subCircuit = ((x % 8) == 0 && (gy > 3 && gy < diePitch - 2))
                        || ((y % 8) == 0 && (gx > 3 && gx < diePitch - 2));

                    if (majorStreet)
                    {
                        col = Color.Lerp(col, new Color(0.28f, 0.42f, 0.58f, 1f), 0.78f);
                    }
                    else if (subCircuit)
                    {
                        // Subtle lithographic active die sub-cells.
                        float cellTint = ((x / 8 + y / 8) & 1) == 0 ? 0.08f : -0.04f;
                        col = new Color(col.r + cellTint, col.g + cellTint * 1.15f, col.b + cellTint * 1.25f, 1f);
                    }

                    // Subtle chamfer highlight near wafer edge.
                    if (r > 0.90f && r < 0.98f)
                    {
                        col = Color.Lerp(col, new Color(0.84f, 0.90f, 0.97f, 1f), 0.35f);
                    }

                    col.a = 1f;
                    pixels[y * size + x] = ClampColor(col);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(true, false);
            return tex;
        }

        /// <summary>
        /// Synthesizes a 512x512 tangent-space normal map matching the silicon wafer micro-die grooves
        /// and fine concentric polish ripples.
        /// </summary>
        public static Texture2D CreateSiliconWaferNormal(int size = DefaultSize)
        {
            float[,] height = new float[size, size];
            int diePitch = Mathf.Max(16, size / 14);
            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    int gx = x % diePitch;
                    int gy = y % diePitch;
                    float groove = (gx <= 1 || gy <= 1) ? -0.45f : 0f;
                    float sub = ((x % 8) == 0 || (y % 8) == 0) ? -0.10f : 0f;
                    float polish = Mathf.Sin(r * 96f) * 0.05f;
                    height[x, y] = groove + sub + polish;
                }
            }

            return HeightFieldToNormalMap(height, size, 2.2f, "Tex_SiliconWafer_Normal");
        }

        /// <summary>
        /// Synthesizes a 512x512 directional brushed anodized metal albedo texture.
        /// </summary>
        public static Texture2D CreateBrushedMetalAlbedo(int size = DefaultSize)
        {
            Texture2D tex = CreateTexture(size, "Tex_BrushedMetal_Albedo", false);
            Color[] pixels = new Color[size * size];

            float[] rowBias = new float[size];
            for (int y = 0; y < size; y++)
            {
                rowBias[y] = (Hash01(19, y, 7) - 0.5f) * 0.09f + Mathf.Sin(y * 0.37f) * 0.015f;
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float streak = (Hash01(x / 12, y, 13) - 0.5f) * 0.055f;
                    float micro = (Hash01(x, y, 31) - 0.5f) * 0.020f;
                    float v = Mathf.Clamp01(0.76f + rowBias[y] + streak + micro);
                    pixels[y * size + x] = new Color(v * 0.96f, v * 0.98f, v * 1.02f, 1f);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(true, false);
            return tex;
        }

        /// <summary>
        /// Synthesizes a 512x512 tangent-space brushed aluminum grain normal map.
        /// </summary>
        public static Texture2D CreateBrushedMetalNormal(int size = DefaultSize)
        {
            float[,] height = new float[size, size];
            for (int y = 0; y < size; y++)
            {
                float rb = (Hash01(43, y, 17) - 0.5f) * 0.38f;
                for (int x = 0; x < size; x++)
                {
                    float streak = (Hash01(x / 10, y, 91) - 0.5f) * 0.22f;
                    height[x, y] = rb + streak;
                }
            }

            return HeightFieldToNormalMap(height, size, 1.5f, "Tex_BrushedMetal_Normal");
        }

        /// <summary>
        /// Synthesizes a 512x512 industrial lab battery casing albedo map featuring a metallic copper-gold
        /// upper band, dark charcoal body, crisp + / - polarity markings, and voltage specification strip.
        /// </summary>
        public static Texture2D CreateBatteryCasingAlbedo(int size = DefaultSize)
        {
            Texture2D tex = CreateTexture(size, "Tex_BatteryCasing_Albedo", false);
            Color[] pixels = new Color[size * size];

            Color darkBody = new Color(0.11f, 0.13f, 0.17f, 1f);
            Color copperBand = new Color(0.82f, 0.54f, 0.22f, 1f);
            Color copperHighlight = new Color(0.95f, 0.72f, 0.38f, 1f);
            Color chromeTrim = new Color(0.76f, 0.80f, 0.86f, 1f);
            Color dangerAmber = new Color(0.94f, 0.64f, 0.14f, 1f);
            Color plusRed = new Color(0.95f, 0.22f, 0.22f, 1f);
            Color minusCyan = new Color(0.28f, 0.78f, 0.96f, 1f);

            int copperTopY = Mathf.RoundToInt(size * 0.72f);
            int trimTopY = Mathf.RoundToInt(size * 0.695f);
            int stripeY0 = Mathf.RoundToInt(size * 0.16f);
            int stripeY1 = Mathf.RoundToInt(size * 0.22f);

            for (int y = 0; y < size; y++)
            {
                float rowGrain = (Hash01(5, y, 101) - 0.5f) * 0.03f;
                for (int x = 0; x < size; x++)
                {
                    float v = y / (float)(size - 1);
                    float u = x / (float)(size - 1);
                    Color c;

                    if (y >= copperTopY)
                    {
                        float grad = Mathf.Sin((u * 4f + v * 2f) * Mathf.PI) * 0.25f + 0.5f;
                        c = Color.Lerp(copperBand, copperHighlight, grad * 0.45f + rowGrain);
                    }
                    else if (y >= trimTopY)
                    {
                        c = chromeTrim;
                    }
                    else if (y >= stripeY0 && y <= stripeY1)
                    {
                        // Subtle industrial hazard / spec band near base.
                        bool diag = ((x + y) / 14) % 2 == 0;
                        c = diag ? dangerAmber : new Color(0.16f, 0.17f, 0.20f, 1f);
                    }
                    else
                    {
                        float panelEdge = (Mathf.Abs(u - 0.04f) < 0.008f || Mathf.Abs(u - 0.96f) < 0.008f) ? 0.08f : 0f;
                        c = new Color(
                            darkBody.r + rowGrain * 0.5f + panelEdge,
                            darkBody.g + rowGrain * 0.5f + panelEdge,
                            darkBody.b + rowGrain * 0.6f + panelEdge,
                            1f);
                    }

                    // Crisp '+' polarity badge around u = 0.22, v = 0.46 and '-' around u = 0.78, v = 0.46 (repeatable on faces).
                    DrawPolarityMark(u, v, 0.22f, 0.46f, true, plusRed, ref c);
                    DrawPolarityMark(u, v, 0.78f, 0.46f, false, minusCyan, ref c);

                    // Clean voltage spec badge box centered at (0.50, 0.46) with crisp "0-60V DC" pixel glyphs.
                    DrawBatteryVoltageBadge(x, y, size, ref c);

                    pixels[y * size + x] = ClampColor(c);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(true, false);
            return tex;
        }

        /// <summary>
        /// Synthesizes a 512x512 tangent-space normal map with ribbed casing seams and copper band bevels.
        /// </summary>
        public static Texture2D CreateBatteryCasingNormal(int size = DefaultSize)
        {
            float[,] height = new float[size, size];
            int copperTopY = Mathf.RoundToInt(size * 0.72f);
            int trimTopY = Mathf.RoundToInt(size * 0.695f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1);
                    float v = y / (float)(size - 1);
                    float h = (Hash01(x / 4, y, 73) - 0.5f) * 0.08f;

                    if (y >= copperTopY) h += 0.22f;
                    else if (y >= trimTopY) h += 0.35f;

                    // Vertical molded grip ribs along side zones.
                    if (v > 0.25f && v < 0.66f && (Mathf.Abs(u - 0.08f) < 0.03f || Mathf.Abs(u - 0.92f) < 0.03f))
                    {
                        h += Mathf.Sin(v * 140f) * 0.14f;
                    }

                    height[x, y] = h;
                }
            }

            return HeightFieldToNormalMap(height, size, 2.0f, "Tex_BatteryCasing_Normal");
        }

        /// <summary>
        /// Synthesizes a 512x512 granular crystalline dopant powder/pellet albedo map.
        /// Designed white-balanced around bright crystal facets so material _BaseColor cleanly tints each dopant.
        /// </summary>
        public static Texture2D CreateDopantGranularAlbedo(int size = DefaultSize)
        {
            Texture2D tex = CreateTexture(size, "Tex_DopantGranular_Albedo", false);
            Color[] pixels = new Color[size * size];

            const int grid = 14;
            for (int y = 0; y < size; y++)
            {
                float fy = (y / (float)size) * grid;
                int cy = Mathf.FloorToInt(fy);
                for (int x = 0; x < size; x++)
                {
                    float fx = (x / (float)size) * grid;
                    int cx = Mathf.FloorToInt(fx);

                    float bestDist = 99f;
                    float secondDist = 99f;
                    float cellTone = 0.75f;

                    for (int oy = -1; oy <= 1; oy++)
                    {
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int nx = (cx + ox + grid) % grid;
                            int ny = (cy + oy + grid) % grid;
                            float px = cx + ox + 0.15f + 0.70f * Hash01(nx, ny, 11);
                            float py = cy + oy + 0.15f + 0.70f * Hash01(nx, ny, 29);
                            float d = Mathf.Sqrt((fx - px) * (fx - px) + (fy - py) * (fy - py));
                            if (d < bestDist)
                            {
                                secondDist = bestDist;
                                bestDist = d;
                                cellTone = 0.56f + 0.42f * Hash01(nx, ny, 53);
                            }
                            else if (d < secondDist)
                            {
                                secondDist = d;
                            }
                        }
                    }

                    float edgeCrevice = Mathf.Clamp01((secondDist - bestDist) * 4.5f);
                    float facetHighlight = Hash01(x / 3, y / 3, 97) > 0.965f ? 0.28f : 0f;
                    float shade = Mathf.Clamp01(cellTone * (0.55f + 0.45f * edgeCrevice) + facetHighlight);
                    pixels[y * size + x] = new Color(shade, shade, shade, 1f);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(true, false);
            return tex;
        }

        /// <summary>
        /// Synthesizes a 512x512 crystalline pellet normal map matching the Voronoi facets.
        /// </summary>
        public static Texture2D CreateDopantGranularNormal(int size = DefaultSize)
        {
            float[,] height = new float[size, size];
            const int grid = 14;
            for (int y = 0; y < size; y++)
            {
                float fy = (y / (float)size) * grid;
                int cy = Mathf.FloorToInt(fy);
                for (int x = 0; x < size; x++)
                {
                    float fx = (x / (float)size) * grid;
                    int cx = Mathf.FloorToInt(fx);
                    float bestDist = 99f;
                    float secondDist = 99f;

                    for (int oy = -1; oy <= 1; oy++)
                    {
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int nx = (cx + ox + grid) % grid;
                            int ny = (cy + oy + grid) % grid;
                            float px = cx + ox + 0.15f + 0.70f * Hash01(nx, ny, 11);
                            float py = cy + oy + 0.15f + 0.70f * Hash01(nx, ny, 29);
                            float d = Mathf.Sqrt((fx - px) * (fx - px) + (fy - py) * (fy - py));
                            if (d < bestDist)
                            {
                                secondDist = bestDist;
                                bestDist = d;
                            }
                            else if (d < secondDist)
                            {
                                secondDist = d;
                            }
                        }
                    }

                    height[x, y] = Mathf.Clamp01((secondDist - bestDist) * 2.2f) * 0.75f;
                }
            }

            return HeightFieldToNormalMap(height, size, 2.8f, "Tex_DopantGranular_Normal");
        }

        /// <summary>
        /// Synthesizes a 512x512 translucent crystal-lattice albedo texture with subtle geometric atomic lines
        /// for P-Type and N-Type semiconductor blocks.
        /// </summary>
        public static Texture2D CreateCrystalLatticeAlbedo(int size = DefaultSize)
        {
            Texture2D tex = CreateTexture(size, "Tex_CrystalLattice_Albedo", false);
            Color[] pixels = new Color[size * size];

            int step = Mathf.Max(16, size / 8);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int mx = x % step;
                    int my = y % step;
                    float dx = Mathf.Abs(mx - step * 0.5f);
                    float dy = Mathf.Abs(my - step * 0.5f);
                    float diag = Mathf.Abs(dx - dy);
                    bool latticeLine = mx <= 1 || my <= 1 || diag < 1.3f;
                    bool atomNode = (dx * dx + dy * dy) < (step * 0.12f) * (step * 0.12f);

                    float lum = atomNode ? 1.0f : (latticeLine ? 0.88f : 0.66f);
                    pixels[y * size + x] = new Color(lum, lum, lum, 1f);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(true, false);
            return tex;
        }

        /// <summary>
        /// Synthesizes a 512x512 tangent-space normal map with etched crystalline facet ridges.
        /// </summary>
        public static Texture2D CreateCrystalLatticeNormal(int size = DefaultSize)
        {
            float[,] height = new float[size, size];
            int step = Mathf.Max(16, size / 8);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int mx = x % step;
                    int my = y % step;
                    float dx = Mathf.Abs(mx - step * 0.5f);
                    float dy = Mathf.Abs(my - step * 0.5f);
                    float diag = Mathf.Abs(dx - dy);
                    bool ridge = mx <= 2 || my <= 2 || diag < 1.8f;
                    height[x, y] = ridge ? 0.30f : 0f;
                }
            }

            return HeightFieldToNormalMap(height, size, 1.8f, "Tex_CrystalLattice_Normal");
        }

        /// <summary>
        /// Synthesizes a 512x512 circular graduated dial scale plate covering the -135 to +135 degree arc
        /// of RotaryKnobControl dials.
        /// </summary>
        public static Texture2D CreateScaleDialPlateAlbedo(int size = DefaultSize)
        {
            Texture2D tex = CreateTexture(size, "Tex_ScaleDialPlate_Albedo", false);
            Color[] pixels = new Color[size * size];

            Color plateDark = new Color(0.11f, 0.14f, 0.19f, 1f);
            Color rimMetal = new Color(0.56f, 0.63f, 0.72f, 1f);
            Color arcCyan = new Color(0.24f, 0.78f, 0.96f, 1f);
            Color tickBright = new Color(0.92f, 0.96f, 1.0f, 1f);
            Color minAmber = new Color(0.32f, 0.85f, 0.56f, 1f);
            Color maxAmber = new Color(0.96f, 0.52f, 0.20f, 1f);

            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    // UV y=1 -> +Z forward in Unity cylinder top UV; angle 0 deg at +Y (nx=0, ny>0), +clockwise.
                    float angleDeg = Mathf.Atan2(nx, ny) * Mathf.Rad2Deg;

                    Color c = plateDark;
                    if (r > 0.92f)
                    {
                        c = rimMetal;
                    }
                    else if (Mathf.Abs(angleDeg) <= 137f)
                    {
                        if (r >= 0.72f && r <= 0.755f)
                        {
                            float t = Mathf.InverseLerp(-135f, 135f, angleDeg);
                            c = Color.Lerp(arcCyan, maxAmber, Mathf.Clamp01((t - 0.5f) * 2f));
                        }

                        // 21 tick marks across -135 .. +135 (major ticks every 27 deg = 11 major ticks).
                        float stepMinor = 270f / 20f;
                        float nearestTick = Mathf.Round(angleDeg / stepMinor) * stepMinor;
                        if (Mathf.Abs(nearestTick) <= 136f && Mathf.Abs(angleDeg - nearestTick) <= 2.1f)
                        {
                            int idx = Mathf.RoundToInt((nearestTick + 135f) / stepMinor);
                            bool isMajor = (idx % 2) == 0;
                            float rMin = isMajor ? 0.76f : 0.81f;
                            float rMax = isMajor ? 0.90f : 0.87f;
                            if (r >= rMin && r <= rMax)
                            {
                                c = idx == 0 ? minAmber : (idx == 20 ? maxAmber : tickBright);
                            }
                        }
                    }

                    pixels[y * size + x] = ClampColor(c);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(true, false);
            return tex;
        }

        /// <summary>
        /// Synthesizes a 512x512 crisp linear graduated tick-mark scale strip for UI slider backplates.
        /// </summary>
        public static Texture2D CreateLinearScaleTicksAlbedo(int size = DefaultSize)
        {
            Texture2D tex = CreateTexture(size, "Tex_LinearScaleTicks_Albedo", true);
            Color[] pixels = new Color[size * size];

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color tickMajor = new Color(0.86f, 0.93f, 1.0f, 0.92f);
            Color tickMinor = new Color(0.55f, 0.68f, 0.82f, 0.68f);

            int padX = Mathf.RoundToInt(size * 0.04f);
            int spanX = size - padX * 2;
            const int subDivs = 20;

            for (int y = 0; y < size; y++)
            {
                float v = y / (float)(size - 1);
                for (int x = 0; x < size; x++)
                {
                    Color c = clear;
                    if (x >= padX - 2 && x <= size - padX + 2)
                    {
                        // Top and bottom ruler rail ticks outside the central bar slot.
                        bool inTopTicks = v >= 0.70f && v <= 0.95f;
                        bool inBotTicks = v >= 0.05f && v <= 0.30f;

                        if (inTopTicks || inBotTicks)
                        {
                            float localU = Mathf.Clamp01((x - padX) / (float)spanX);
                            float scaled = localU * subDivs;
                            int nearest = Mathf.RoundToInt(scaled);
                            float distPx = Mathf.Abs(scaled - nearest) * (spanX / (float)subDivs);
                            bool major = (nearest % 2) == 0;
                            float halfW = major ? 2.2f : 1.2f;
                            bool heightOk = major
                                || (inTopTicks && v <= 0.84f)
                                || (inBotTicks && v >= 0.16f);

                            if (distPx <= halfW && heightOk)
                            {
                                c = major ? tickMajor : tickMinor;
                            }
                        }
                    }

                    pixels[y * size + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(true, false);
            return tex;
        }

        /// <summary>
        /// Synthesizes a 512x512 dark-anodized optical breadboard albedo map with machined M6 grid holes
        /// and fine brushed metallic micro-grain.
        /// </summary>
        public static Texture2D CreateOpticalBreadboardAlbedo(int size = DefaultSize)
        {
            Texture2D tex = CreateTexture(size, "Tex_OpticalBreadboard_Albedo", false);
            Color[] pixels = new Color[size * size];

            const int grid = 16;
            float cellPx = size / (float)grid;
            for (int y = 0; y < size; y++)
            {
                float rowGrain = (Hash01(37, y, 19) - 0.5f) * 0.035f;
                for (int x = 0; x < size; x++)
                {
                    float fx = (x % cellPx) - cellPx * 0.5f;
                    float fy = (y % cellPx) - cellPx * 0.5f;
                    float r = Mathf.Sqrt(fx * fx + fy * fy);
                    float boreR = cellPx * 0.16f;
                    float chamferR = cellPx * 0.24f;

                    float lum;
                    if (r < boreR)
                    {
                        lum = 0.04f; // Deep dark tapped bore
                    }
                    else if (r < chamferR)
                    {
                        lum = 0.38f; // Machined countersink rim
                    }
                    else
                    {
                        float streak = (Hash01(x / 8, y, 41) - 0.5f) * 0.025f;
                        lum = Mathf.Clamp01(0.16f + rowGrain + streak);
                    }

                    pixels[y * size + x] = new Color(lum * 0.92f, lum * 0.97f, lum * 1.06f, 1f);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(true, false);
            return tex;
        }

        /// <summary>
        /// Synthesizes a 512x512 optical breadboard tangent-space normal map matching the countersunk grid holes.
        /// </summary>
        public static Texture2D CreateOpticalBreadboardNormal(int size = DefaultSize)
        {
            float[,] height = new float[size, size];
            const int grid = 16;
            float cellPx = size / (float)grid;

            for (int y = 0; y < size; y++)
            {
                float rb = (Hash01(11, y, 7) - 0.5f) * 0.05f;
                for (int x = 0; x < size; x++)
                {
                    float fx = (x % cellPx) - cellPx * 0.5f;
                    float fy = (y % cellPx) - cellPx * 0.5f;
                    float r = Mathf.Sqrt(fx * fx + fy * fy);
                    float boreR = cellPx * 0.16f;
                    float chamferR = cellPx * 0.24f;

                    float h = rb;
                    if (r < boreR)
                    {
                        h = -0.70f;
                    }
                    else if (r < chamferR)
                    {
                        float t = (r - boreR) / (chamferR - boreR);
                        h = Mathf.Lerp(-0.70f, 0f, t);
                    }

                    height[x, y] = h;
                }
            }

            return HeightFieldToNormalMap(height, size, 2.2f, "Tex_OpticalBreadboard_Normal");
        }

        private static Texture2D CreateTexture(int size, string name, bool alphaChannel)
        {
            TextureFormat fmt = alphaChannel ? TextureFormat.RGBA32 : TextureFormat.RGB24;
            Texture2D tex = new Texture2D(size, size, fmt, true, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };
            return tex;
        }

        private static Texture2D HeightFieldToNormalMap(float[,] h, int size, float strength, string name)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, true, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                int ym = (y - 1 + size) % size;
                int yp = (y + 1) % size;
                for (int x = 0; x < size; x++)
                {
                    int xm = (x - 1 + size) % size;
                    int xp = (x + 1) % size;

                    float dx = (h[xp, y] - h[xm, y]) * strength;
                    float dy = (h[x, yp] - h[x, ym]) * strength;
                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                    pixels[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(true, false);
            return tex;
        }

        private static void DrawPolarityMark(float u, float v, float cx, float cy, bool isPlus, Color markColor, ref Color dst)
        {
            float dx = Mathf.Abs(u - cx);
            float dy = Mathf.Abs(v - cy);
            float r2 = dx * dx + dy * dy;

            if (r2 < 0.056f * 0.056f)
            {
                // Circular dark emblem behind + / -.
                dst = Color.Lerp(dst, new Color(0.06f, 0.07f, 0.09f, 1f), 0.90f);
                bool hBar = dx < 0.034f && dy < 0.008f;
                bool vBar = isPlus && dy < 0.034f && dx < 0.008f;
                if (hBar || vBar)
                {
                    dst = markColor;
                }
                else if (r2 > 0.048f * 0.048f)
                {
                    dst = Color.Lerp(dst, markColor, 0.72f);
                }
            }
        }

        private static void DrawBatteryVoltageBadge(int x, int y, int size, ref Color dst)
        {
            int bx0 = Mathf.RoundToInt(size * 0.36f);
            int bx1 = Mathf.RoundToInt(size * 0.64f);
            int by0 = Mathf.RoundToInt(size * 0.34f);
            int by1 = Mathf.RoundToInt(size * 0.58f);

            if (x < bx0 || x > bx1 || y < by0 || y > by1)
            {
                return;
            }

            bool border = x <= bx0 + 2 || x >= bx1 - 2 || y <= by0 + 2 || y >= by1 - 2;
            dst = border ? new Color(0.55f, 0.66f, 0.80f, 1f) : new Color(0.07f, 0.10f, 0.14f, 1f);

            // Draw pixel 3x5 digits "60V" cleanly centered inside badge box.
            int gx0 = bx0 + Mathf.RoundToInt((bx1 - bx0) * 0.16f);
            int gy0 = by0 + Mathf.RoundToInt((by1 - by0) * 0.25f);
            int cell = Mathf.Max(2, (bx1 - bx0) / 18);

            if (SampleGlyph3x5('6', x - gx0, y - gy0, cell)
                || SampleGlyph3x5('0', x - (gx0 + cell * 4), y - gy0, cell)
                || SampleGlyph3x5('V', x - (gx0 + cell * 8), y - gy0, cell))
            {
                dst = new Color(0.95f, 0.82f, 0.32f, 1f);
            }
        }

        private static bool SampleGlyph3x5(char ch, int px, int py, int cell)
        {
            if (px < 0 || py < 0) return false;
            int col = px / cell;
            int row = py / cell;
            if (col >= 3 || row >= 5) return false;

            int[] rows;
            switch (ch)
            {
                case '6':
                    rows = new[] { 0b111, 0b101, 0b111, 0b100, 0b111 }; // bottom->top
                    break;
                case '0':
                    rows = new[] { 0b111, 0b101, 0b101, 0b101, 0b111 };
                    break;
                case 'V':
                    rows = new[] { 0b010, 0b101, 0b101, 0b101, 0b101 };
                    break;
                default:
                    return false;
            }

            return (rows[row] & (1 << (2 - col))) != 0;
        }

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0x00FFFFFFu) / 16777215f;
            }
        }

        private static Color ClampColor(Color c)
        {
            return new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), Mathf.Clamp01(c.a));
        }
    }
}
