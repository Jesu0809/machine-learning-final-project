using System.Collections.Generic;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>Base color and normal map of a surface.</summary>
    public struct Surface
    {
        public Texture2D Albedo;
        public Texture2D Normal;
    }

    /// <summary>
    /// Textures generated in code: tiled floors with grout, concrete, planks, ceiling tiles,
    /// wallpaper, arcade carpet, posters, pixel-font signs and UI shapes. Each surface comes with a
    /// normal map computed from its height, so the lights catch the grout, cracks and seams.
    /// </summary>
    public static class ProceduralTextures
    {
        private const int Size = 256;

        // ------------------------------------------------------------------ surfaces

        /// <summary>Square tiles with dark grout, checkered (a/b) or plain (a), with grime.</summary>
        public static Surface Tiles(Color a, Color b, int cells, bool checker, float dirt = 0.3f)
        {
            var color = new Color[Size * Size];
            var height = new float[Size * Size];
            int cell = Size / cells;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int cx = x % cell;
                    int cy = y % cell;
                    bool grout = cx < 2 || cy < 2;
                    bool even = ((x / cell) + (y / cell)) % 2 == 0;
                    Color tile = checker && !even ? b : a;
                    // Each tile slightly different, plus stains and chips.
                    float variation = (Hash(x / cell, y / cell) - 0.5f) * 0.08f;
                    float stains = Fbm(x, y, 0.03f) * dirt;
                    Color c = grout ? new Color(0.12f, 0.11f, 0.1f) : tile * (1f + variation);
                    color[y * Size + x] = Color.Lerp(c, new Color(0.18f, 0.14f, 0.09f), stains);
                    height[y * Size + x] = grout ? 0f : 1f - Fbm(x, y, 0.2f) * 0.15f;
                }
            }
            return Build(color, height, 2.5f);
        }

        /// <summary>Running-bond brick wall with mortar and soot (the outside of the pizzeria).</summary>
        public static Surface Bricks(Color brick, Color mortar)
        {
            var color = new Color[Size * Size];
            var height = new float[Size * Size];
            const int rows = 8;
            const int columns = 4;
            int rowHeight = Size / rows;
            int brickWidth = Size / columns;
            for (int y = 0; y < Size; y++)
            {
                int row = y / rowHeight;
                for (int x = 0; x < Size; x++)
                {
                    int shifted = x + (row % 2) * brickWidth / 2;
                    int column = shifted / brickWidth % columns;
                    bool joint = y % rowHeight < 3 || shifted % brickWidth < 3;
                    float variation = (Hash(column, row) - 0.5f) * 0.3f;
                    float soot = Fbm(x, y, 0.02f) * 0.5f;
                    Color c = joint ? mortar : brick * (1f + variation) * (0.85f + Fbm(x, y, 0.3f) * 0.3f);
                    color[y * Size + x] = Color.Lerp(c, new Color(0.05f, 0.05f, 0.05f), soot * (y / (float)Size) * 0.8f);
                    height[y * Size + x] = joint ? 0f : 1f - Fbm(x, y, 0.25f) * 0.2f;
                }
            }
            return Build(color, height, 3f);
        }

        /// <summary>Wet asphalt: dark, fine gravel, cracks.</summary>
        public static Surface Asphalt()
        {
            var color = new Color[Size * Size];
            var height = new float[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float gravel = Hash(x, y);
                    float patch = Fbm(x, y, 0.025f);
                    float crack = Mathf.Abs(Mathf.PerlinNoise(x * 0.02f + 5f, y * 0.02f + 2f) - 0.5f) < 0.005f ? 1f : 0f;
                    float shade = 0.035f + gravel * 0.035f + patch * 0.03f - crack * 0.02f;
                    color[y * Size + x] = new Color(shade, shade, shade * 1.05f);
                    height[y * Size + x] = gravel * 0.3f - crack;
                }
            }
            return Build(color, height, 2f);
        }

        public static Surface Concrete(Color baseColor)
        {
            var color = new Color[Size * Size];
            var height = new float[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float n = Fbm(x, y, 0.04f);
                    float crack = Mathf.Abs(Mathf.PerlinNoise(x * 0.015f + 3f, y * 0.015f) - 0.5f) < 0.006f ? 1f : 0f;
                    float shade = 0.8f + n * 0.35f - crack * 0.4f + (Hash(x, y) - 0.5f) * 0.06f;
                    color[y * Size + x] = baseColor * shade;
                    height[y * Size + x] = n * 0.6f - crack;
                }
            }
            return Build(color, height, 3f);
        }

        public static Surface Planks(Color baseColor)
        {
            var color = new Color[Size * Size];
            var height = new float[Size * Size];
            const int plank = 32;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int row = y / plank;
                    int offset = (int)(Hash(row, 7) * Size);
                    bool seam = y % plank < 2 || (x + offset) % 128 < 2;
                    float grain = Mathf.PerlinNoise(x * 0.01f + row * 5f, y * 0.3f) * 0.3f;
                    float shade = 0.75f + grain + (Hash(row, (x + offset) / 128) - 0.5f) * 0.2f;
                    color[y * Size + x] = seam ? baseColor * 0.3f : baseColor * shade;
                    height[y * Size + x] = seam ? 0f : 1f - grain * 0.3f;
                }
            }
            return Build(color, height, 2f);
        }

        public static Surface CeilingTiles()
        {
            var color = new Color[Size * Size];
            var height = new float[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    bool frame = x % 128 < 4 || y % 128 < 4;
                    float pits = Hash(x, y) > 0.93f ? 0.3f : 0f;
                    float water = Mathf.Clamp01(Mathf.PerlinNoise(x * 0.012f, y * 0.012f + 9f) - 0.55f) * 2.5f;
                    Color c = frame ? new Color(0.35f, 0.35f, 0.33f) : new Color(0.62f, 0.6f, 0.55f) * (1f - pits * 0.5f);
                    color[y * Size + x] = Color.Lerp(c, new Color(0.35f, 0.25f, 0.12f), water);
                    height[y * Size + x] = frame ? 1f : 0.6f - pits;
                }
            }
            return Build(color, height, 2f);
        }

        /// <summary>Old diner wallpaper: vertical stripes with small diamonds, water stains and drips.</summary>
        public static Surface Wallpaper(Color background, Color stripe)
        {
            var color = new Color[Size * Size];
            var height = new float[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int sx = x % 32;
                    bool line = sx >= 14 && sx < 18;
                    int dx = Mathf.Abs(sx - 16) + Mathf.Abs(y % 32 - 16);
                    bool diamond = dx < 4 && !line;
                    Color c = line || diamond ? stripe : background;
                    float stain = Mathf.Clamp01(Fbm(x, y, 0.02f) - 0.35f) * 1.4f;
                    float drip = Mathf.PerlinNoise(x * 0.25f, 3f) > 0.75f ? Mathf.Clamp01((y - 90f) / 160f) * 0.5f : 0f;
                    float peel = Fbm(x + 500, y, 0.05f) > 0.72f ? 1f : 0f;
                    c = Color.Lerp(c, new Color(0.25f, 0.2f, 0.12f), stain * 0.6f + drip);
                    if (peel > 0f)
                    {
                        c = new Color(0.32f, 0.3f, 0.27f);
                    }
                    color[y * Size + x] = c;
                    height[y * Size + x] = peel > 0f ? 0f : 0.7f + (line ? 0.1f : 0f);
                }
            }
            return Build(color, height, 1.5f);
        }

        /// <summary>Dark arcade carpet with neon confetti shapes.</summary>
        public static Surface Carpet()
        {
            var color = new Color[Size * Size];
            var height = new float[Size * Size];
            Color[] neon = { new Color(0.1f, 0.8f, 0.9f), new Color(0.9f, 0.2f, 0.7f), new Color(0.95f, 0.8f, 0.1f), new Color(0.3f, 0.9f, 0.3f) };
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int bx = x / 20;
                    int by = y / 20;
                    float h = Hash(bx, by);
                    var center = new Vector2(bx * 20 + 10 + (Hash(by, bx) - 0.5f) * 8f, by * 20 + 10);
                    bool shape = h > 0.4f && Mathf.Abs(x - center.x) + Mathf.Abs(y - center.y) < 4f;
                    Color c = new Color(0.06f, 0.05f, 0.1f) * (0.8f + Hash(x, y) * 0.4f);
                    if (shape)
                    {
                        c = neon[(int)(h * 37f) % neon.Length] * 0.7f;
                    }
                    color[y * Size + x] = c;
                    height[y * Size + x] = Hash(x, y) * 0.5f;
                }
            }
            return Build(color, height, 1f);
        }

        /// <summary>Flat grimy color (metal, plastic, painted wood).</summary>
        public static Surface Grimy(Color baseColor, float scratches = 0.3f)
        {
            var color = new Color[Size * Size];
            var height = new float[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float n = Fbm(x, y, 0.03f);
                    bool scratch = Mathf.Abs(Mathf.PerlinNoise(x * 0.5f, y * 0.02f) - 0.5f) < 0.01f * scratches;
                    float shade = 1f - n * 0.4f + (scratch ? 0.25f : 0f);
                    color[y * Size + x] = baseColor * shade;
                    height[y * Size + x] = n * 0.3f + (scratch ? -0.4f : 0f);
                }
            }
            return Build(color, height, 1f);
        }

        /// <summary>An irregular dark blotch with soft edges (alpha), for stains on the floor.</summary>
        public static Texture2D Stain(int seed)
        {
            const int s = 128;
            var texture = New(s, s, TextureWrapMode.Clamp);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = (x - s * 0.5f) / (s * 0.5f);
                    float dy = (y - s * 0.5f) / (s * 0.5f);
                    float edge = 0.55f + Mathf.PerlinNoise(x * 0.05f + seed, y * 0.05f) * 0.45f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / edge;
                    float a = Mathf.Clamp01((1f - d) * 3f) * (0.6f + Fbm(x + seed * 13, y, 0.08f) * 0.4f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            texture.Apply();
            return texture;
        }

        // ------------------------------------------------------------------ posters and signs

        /// <summary>A faded show poster: a round animatronic face over stripes, with a title.</summary>
        public static Texture2D Poster(Color background, Color figure, string title)
        {
            const int w = 128;
            const int h = 176;
            var texture = New(w, h, TextureWrapMode.Clamp);
            var pixels = new Color[w * h];
            var head = new Vector2(64f, 100f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = (y / 10) % 2 == 0 ? background : background * 0.82f;
                    var p = new Vector2(x, y);
                    bool face = Vector2.Distance(p, head) < 34f
                                || Vector2.Distance(p, head + new Vector2(-30f, 30f)) < 12f
                                || Vector2.Distance(p, head + new Vector2(30f, 30f)) < 12f;
                    if (face)
                    {
                        c = figure;
                    }
                    if (Vector2.Distance(p, head + new Vector2(-12f, 8f)) < 6f || Vector2.Distance(p, head + new Vector2(12f, 8f)) < 6f)
                    {
                        c = Color.white;
                    }
                    if (Vector2.Distance(p, head + new Vector2(-12f, 8f)) < 2.5f || Vector2.Distance(p, head + new Vector2(12f, 8f)) < 2.5f)
                    {
                        c = Color.black;
                    }
                    if (y > head.y - 22 && y < head.y - 16 && Mathf.Abs(x - head.x) < 16)
                    {
                        c = Color.black; // the smile
                    }
                    float fade = Fbm(x, y, 0.05f) * 0.45f;
                    pixels[y * w + x] = Color.Lerp(c, new Color(0.55f, 0.48f, 0.36f), fade);
                }
            }
            texture.SetPixels(pixels);
            DrawText(texture, title, 6, 14, Color.white, 2);
            texture.Apply();
            return texture;
        }

        /// <summary>A sign or banner with pixel-font text.</summary>
        public static Texture2D Sign(string text, Color foreground, Color background, int scale = 4, int padding = 6)
        {
            int width = text.Length * 6 * scale - scale + padding * 2;
            int height = 7 * scale + padding * 2;
            var texture = New(width, height, TextureWrapMode.Clamp);
            texture.filterMode = FilterMode.Point;
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = background;
            }
            texture.SetPixels(pixels);
            DrawText(texture, text, padding, padding, foreground, scale);
            texture.Apply();
            return texture;
        }

        /// <summary>A far building's front at night: dark walls and a grid of windows, a few lit.</summary>
        public static Texture2D CityWindows()
        {
            const int width = 128;
            const int height = 128;
            var texture = New(width, height, TextureWrapMode.Repeat);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int column = x / 16;
                    int floor = y / 16;
                    bool window = x % 16 >= 4 && x % 16 < 12 && y % 16 >= 5 && y % 16 < 13;
                    float lit = Hash(column * 7 + 3, floor * 13 + 1);
                    Color wall = new Color(0.03f, 0.03f, 0.035f);
                    Color glass = lit > 0.9f ? new Color(1f, 0.78f, 0.45f) * (0.5f + lit * 0.4f)
                                : lit > 0.87f ? new Color(0.5f, 0.65f, 0.9f) * 0.5f
                                : new Color(0.012f, 0.014f, 0.02f);
                    pixels[y * width + x] = window ? glass : wall;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>A child's crayon drawing of the animatronics (the hall walls are covered in them).</summary>
        public static Texture2D Drawing(int seed)
        {
            const int s = 96;
            var texture = New(s, s, TextureWrapMode.Clamp);
            var pixels = new Color[s * s];
            Color[] crayons = { new Color(0.6f, 0.35f, 0.15f), new Color(0.3f, 0.3f, 0.8f), new Color(0.85f, 0.75f, 0.2f), new Color(0.75f, 0.2f, 0.2f) };
            Color crayon = crayons[seed % crayons.Length];
            var head = new Vector2(48f, 52f);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    var p = new Vector2(x, y);
                    float d = Vector2.Distance(p, head);
                    bool outline = Mathf.Abs(d - 22f) < 1.6f + Hash(x + seed, y) * 0.8f;
                    bool ears = Mathf.Abs(Vector2.Distance(p, head + new Vector2(-18f, 20f)) - 7f) < 1.4f
                                || Mathf.Abs(Vector2.Distance(p, head + new Vector2(18f, 20f)) - 7f) < 1.4f;
                    bool eyes = Vector2.Distance(p, head + new Vector2(-8f, 5f)) < 3f || Vector2.Distance(p, head + new Vector2(8f, 5f)) < 3f;
                    bool mouth = y > 38 && y < 41 && Mathf.Abs(x - 48) < 10;
                    bool body = x > 36 && x < 60 && y < 30 && (x == 37 || x == 59 || y < 2);
                    Color paper = new Color(0.92f, 0.9f, 0.82f) * (0.9f + Hash(x, y) * 0.1f);
                    pixels[y * s + x] = outline || ears || body ? crayon : eyes || mouth ? Color.black : paper;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        // ------------------------------------------------------------------ UI shapes

        /// <summary>White disc with soft edge (UI blips and dots).</summary>
        public static Texture2D Disc(int size = 64)
        {
            var texture = New(size, size, TextureWrapMode.Clamp);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 6f)));
                }
            }
            texture.Apply();
            return texture;
        }

        /// <summary>Thin white ring (noise ripples, tracker rings).</summary>
        public static Texture2D Ring(int size = 128, float thickness = 0.06f)
        {
            var texture = New(size, size, TextureWrapMode.Clamp);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - (1f - thickness)) / thickness);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            texture.Apply();
            return texture;
        }

        /// <summary>Transparent center, black edges (screen vignette).</summary>
        public static Texture2D Vignette(int size = 256, float strength = 1f)
        {
            var texture = New(size, size, TextureWrapMode.Clamp);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                    texture.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.Clamp01(Mathf.SmoothStep(0.35f, 1f, d) * strength)));
                }
            }
            texture.Apply();
            return texture;
        }

        /// <summary>View from inside a locker: black with horizontal slits to look through.</summary>
        public static Texture2D LockerSlits(int width = 256, int height = 256)
        {
            var texture = New(width, height, TextureWrapMode.Clamp);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float v = (float)y / height;
                    bool inBand = v > 0.4f && v < 0.62f && x > width * 0.22f && x < width * 0.78f;
                    bool slit = inBand && (y % 14) < 7;
                    texture.SetPixel(x, y, new Color(0f, 0f, 0f, slit ? 0f : 0.97f));
                }
            }
            texture.Apply();
            return texture;
        }

        /// <summary>TV static (UI overlay for the jumpscare).</summary>
        public static Texture2D Static(int width = 320, int height = 180)
        {
            var texture = New(width, height, TextureWrapMode.Repeat);
            texture.filterMode = FilterMode.Point;
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                float v = Hash(i % width, i / width + 17);
                pixels[i] = new Color(v, v, v, 1f);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        // ------------------------------------------------------------------ helpers

        private static Surface Build(Color[] color, float[] height, float strength)
        {
            var albedo = New(Size, Size, TextureWrapMode.Repeat);
            albedo.SetPixels(color);
            albedo.Apply();
            return new Surface { Albedo = albedo, Normal = NormalMap(height, strength) };
        }

        /// <summary>Normal map from a height field, in the "AG" layout URP unpacks (x in alpha, y in green).</summary>
        private static Texture2D NormalMap(float[] height, float strength)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float left = height[y * Size + (x + Size - 1) % Size];
                    float right = height[y * Size + (x + 1) % Size];
                    float down = height[((y + Size - 1) % Size) * Size + x];
                    float up = height[((y + 1) % Size) * Size + x];
                    var normal = new Vector3((left - right) * strength, (down - up) * strength, 1f).normalized;
                    pixels[y * Size + x] = new Color(1f, normal.y * 0.5f + 0.5f, 0f, normal.x * 0.5f + 0.5f);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D New(int width, int height, TextureWrapMode wrap)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                wrapMode = wrap,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };
        }

        private static float Fbm(int x, int y, float frequency)
        {
            return Mathf.PerlinNoise(x * frequency + 11.3f, y * frequency + 7.1f) * 0.6f
                 + Mathf.PerlinNoise(x * frequency * 2.3f + 3.7f, y * frequency * 2.3f + 1.9f) * 0.3f
                 + Mathf.PerlinNoise(x * frequency * 5.1f, y * frequency * 5.1f) * 0.1f;
        }

        private static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177;
            return (h & 0xFFFF) / 65535f;
        }

        // ------------------------------------------------------------------ 5x7 pixel font

        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            { 'A', new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" } },
            { 'B', new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." } },
            { 'C', new[] { ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###." } },
            { 'D', new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." } },
            { 'E', new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" } },
            { 'F', new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." } },
            { 'G', new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####" } },
            { 'H', new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" } },
            { 'I', new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####" } },
            { 'J', new[] { "..###", "...#.", "...#.", "...#.", "#..#.", "#..#.", ".##.." } },
            { 'K', new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" } },
            { 'L', new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" } },
            { 'M', new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" } },
            { 'N', new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#" } },
            { 'O', new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." } },
            { 'P', new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." } },
            { 'Q', new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" } },
            { 'R', new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" } },
            { 'S', new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." } },
            { 'T', new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." } },
            { 'U', new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." } },
            { 'V', new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." } },
            { 'W', new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#" } },
            { 'X', new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" } },
            { 'Y', new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." } },
            { 'Z', new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" } },
            { '0', new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." } },
            { '1', new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." } },
            { '2', new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" } },
            { '!', new[] { "..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#.." } },
            { '\'', new[] { "..#..", "..#..", ".....", ".....", ".....", ".....", "....." } },
            { '-', new[] { ".....", ".....", ".....", "#####", ".....", ".....", "....." } },
            { '.', new[] { ".....", ".....", ".....", ".....", ".....", ".....", "..#.." } },
            { ' ', new[] { ".....", ".....", ".....", ".....", ".....", ".....", "....." } }
        };

        private static void DrawText(Texture2D texture, string text, int left, int bottom, Color color, int scale)
        {
            int x0 = left;
            foreach (char raw in text.ToUpperInvariant())
            {
                if (!Glyphs.TryGetValue(raw, out string[] rows))
                {
                    rows = Glyphs[' '];
                }
                for (int row = 0; row < 7; row++)
                {
                    for (int col = 0; col < 5; col++)
                    {
                        if (rows[row][col] != '#')
                        {
                            continue;
                        }
                        for (int sy = 0; sy < scale; sy++)
                        {
                            for (int sx = 0; sx < scale; sx++)
                            {
                                int px = x0 + col * scale + sx;
                                int py = bottom + (6 - row) * scale + sy;
                                if (px >= 0 && px < texture.width && py >= 0 && py < texture.height)
                                {
                                    texture.SetPixel(px, py, color);
                                }
                            }
                        }
                    }
                }
                x0 += 6 * scale;
            }
        }
    }
}
