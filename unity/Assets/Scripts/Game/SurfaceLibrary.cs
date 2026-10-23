using System.Collections.Generic;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// Which scanned PBR surface (Assets/Art/Surfaces, from tools/fetch_surfaces.py) replaces each
    /// surface the level used to generate in code, and its tint. The rooms keep asking for
    /// "floorChecker" or "locker"; <see cref="BuildKit.Surface"/> hands them the real material.
    /// </summary>
    public static class SurfaceLibrary
    {
        private static readonly Dictionary<string, (string surface, Color tint)> Map = new Dictionary<string, (string, Color)>
        {
            // Public wing
            { "floorChecker", ("checker_floor", Color.white) },
            { "wainscotRed", ("checker_band", Color.white) },
            { "wallpaper", ("party_wallpaper", Color.white) },
            { "ceiling", ("ceiling_tiles", new Color(0.85f, 0.83f, 0.78f)) },
            { "carpet", ("arcade_carpet", Color.white) },
            { "arcadeBand", ("checker_band", new Color(0.55f, 0.5f, 1f)) },
            { "arcadeWall", ("party_wallpaper", new Color(0.42f, 0.36f, 0.62f)) },
            { "stagePlanks", ("stage_wood", Color.white) },
            { "planks", ("stage_wood", new Color(0.8f, 0.72f, 0.65f)) },
            { "curtain", ("velvet", new Color(0.75f, 0.3f, 0.85f)) },
            { "tablecloth", ("tablecloth", Color.white) },
            // Staff wing
            { "officeFloor", ("staff_floor", Color.white) },
            { "officeWall", ("staff_wall", new Color(0.82f, 0.9f, 0.84f)) },
            { "officeBand", ("staff_band", Color.white) },
            { "workBand", ("staff_band", new Color(0.7f, 0.72f, 0.7f)) },
            { "kitchenFloor", ("kitchen_floor", Color.white) },
            { "kitchenTiles", ("white_tiles", Color.white) },
            { "restroomTiles", ("white_tiles", new Color(0.78f, 0.92f, 0.98f)) },
            { "restroomWall", ("staff_wall", new Color(0.8f, 0.88f, 0.92f)) },
            { "restroomFloor", ("restroom_floor", Color.white) },
            { "concrete", ("concrete_floor", Color.white) },
            { "concreteWall", ("concrete_wall", Color.white) },
            // Props
            { "propWood", ("wood_planks", Color.white) },
            { "cabinetWood", ("wood_planks", new Color(0.7f, 0.6f, 0.55f)) },
            { "locker", ("metal_green", Color.white) },
            { "doorSteel", ("metal_red", Color.white) },
            { "fuseBox", ("metal_worn", new Color(0.95f, 0.8f, 0.35f)) },
            { "breaker", ("metal_worn", new Color(0.7f, 0.72f, 0.75f)) },
            { "duct", ("metal_galvanized", Color.white) },
            { "ductInside", ("metal_galvanized", new Color(0.7f, 0.7f, 0.7f)) },
            // Outside
            { "bricks", ("bricks", Color.white) },
            { "asphaltWet", ("asphalt", new Color(0.7f, 0.7f, 0.72f)) },
            { "sidewalk", ("sidewalk", Color.white) },
            { "trim", ("sidewalk", new Color(0.85f, 0.83f, 0.8f)) },
            { "boards", ("wood_planks", Color.white) },
        };

        public static bool TryGet(string key, out string surface, out Color tint)
        {
            if (Map.TryGetValue(key, out (string surface, Color tint) entry))
            {
                surface = entry.surface;
                tint = entry.tint;
                return true;
            }
            surface = null;
            tint = Color.white;
            return false;
        }
    }
}
