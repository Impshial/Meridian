using System;
using UnityEngine;

namespace Meridian
{
    [CreateAssetMenu(menuName = "Meridian/Planet Generation Settings")]
    public sealed class PlanetGenerationSettings : ScriptableObject
    {
        public bool overrideSeed;
        public int developmentSeed = 73129;
        [Range(4, 6)] public int geographySubdivisions = 6;
        [Range(3, 5)] public int meshSubdivisions = 4;
        [Range(256, 2048)] public int mapWidth = 1024;
        [Range(0.25f, 0.45f)] public float minimumLand = 0.25f;
        [Range(0.25f, 0.45f)] public float maximumLand = 0.45f;
        [Range(0.001f, 0.01f)] public float relief = 0.005f;
        [Range(0.5f, 2f)] public float continentScale = 1f;
        [Range(0.5f, 2f)] public float riverDensity = 1f;

        public PlanetParameters Snapshot() => new PlanetParameters
        {
            geographySubdivisions = Mathf.Clamp(geographySubdivisions, 4, 6),
            meshSubdivisions = Mathf.Clamp(meshSubdivisions, 3, 5),
            mapWidth = Mathf.ClosestPowerOfTwo(Mathf.Clamp(mapWidth, 256, 2048)),
            minimumLand = Mathf.Clamp(minimumLand, .25f, .45f),
            maximumLand = Mathf.Clamp(Mathf.Max(minimumLand, maximumLand), .25f, .45f),
            relief = Mathf.Clamp(relief, .001f, .01f),
            continentScale = Mathf.Clamp(continentScale, .5f, 2f),
            riverDensity = Mathf.Clamp(riverDensity, .5f, 2f)
        };
    }

    [Serializable]
    public struct PlanetParameters
    {
        public int geographySubdivisions, meshSubdivisions, mapWidth;
        public float minimumLand, maximumLand, relief, continentScale, riverDensity;
    }
}
