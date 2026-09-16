using System;
using UnityEngine;

namespace Meridian
{
    [CreateAssetMenu(menuName="Meridian/Surface Generation Settings")]
    public sealed class SurfaceGenerationSettings : ScriptableObject
    {
        [Header("Geographic mapping — one local unit is one metre")]
        public float mappingRadius=30000;
        public float elevationScale=1200;
        public float tileSize=1000;
        public int heightmapResolution=513;
        public int alphamapResolution=128;
        public float minimumTerrainHeight=-400;
        public float terrainHeightRange=2400;
        [Header("Readable regional landforms")]
        public float broadReliefHeight=100;
        public float landformSpacing=650;
        public float colonyPlateauHeight=35;
        public float smallHillHeight=18;
        public float smallHillSpacing=210;
        [Header("Physical watercourses")]
        public float minimumRiverWidth=9;
        public float maximumRiverWidth=24;
        [Header("Survey readiness")]
        public float minimumBuildableArea=90000;
        public float minimumInteriorSize=200;
        public float buildableSlope=5;
        public float buildableCellSize=10;
        public float plainRadius=190;
        public float plainBlend=180;
        public float objectCellSize=18;
        [Header("Timber groves")]
        public float groveSpacing=320;
        public float groveRadius=125;
        public float treeSpacing=11;
        [Header("Landing hull, deployment access and clearance")]
        public float shipWidth=14;
        public float shipLength=26;
        public float accessLength=18;
        public float safetyMargin=5;
        public float maximumLandingSlope=7;
        public float maximumLandingVariation=1.8f;
        public float placementSampleSpacing=2;

        public SurfaceParameters Snapshot()=>new SurfaceParameters {
            mappingRadius=Mathf.Clamp(mappingRadius,10000,100000), elevationScale=Mathf.Clamp(elevationScale,100,2000),
            tileSize=Mathf.Clamp(tileSize,500,2000), heightmapResolution=Mathf.Clamp(Mathf.ClosestPowerOfTwo(heightmapResolution-1),128,1024)+1,
            alphamapResolution=Mathf.Clamp(Mathf.ClosestPowerOfTwo(alphamapResolution),32,256),
            minimumTerrainHeight=Mathf.Min(-50,minimumTerrainHeight), terrainHeightRange=Mathf.Max(2400,terrainHeightRange),
            broadReliefHeight=Mathf.Clamp(broadReliefHeight,30,220),landformSpacing=Mathf.Clamp(landformSpacing,450,1000),colonyPlateauHeight=Mathf.Clamp(colonyPlateauHeight,0,80),
            smallHillHeight=Mathf.Clamp(smallHillHeight,6,35),smallHillSpacing=Mathf.Clamp(smallHillSpacing,150,300),
            minimumRiverWidth=Mathf.Clamp(minimumRiverWidth,4,30),maximumRiverWidth=Mathf.Clamp(Mathf.Max(minimumRiverWidth,maximumRiverWidth),4,60),
            minimumBuildableArea=Mathf.Max(90000,minimumBuildableArea),minimumInteriorSize=Mathf.Max(200,minimumInteriorSize),
            buildableSlope=Mathf.Clamp(buildableSlope,1,10),buildableCellSize=Mathf.Clamp(buildableCellSize,5,20),
            plainRadius=Mathf.Clamp(plainRadius,185,400),plainBlend=Mathf.Clamp(plainBlend,100,500),objectCellSize=Mathf.Clamp(objectCellSize,10,40),
            groveSpacing=Mathf.Clamp(groveSpacing,220,500),groveRadius=Mathf.Clamp(groveRadius,65,Mathf.Clamp(groveSpacing,220,500)*.46f),
            treeSpacing=Mathf.Clamp(treeSpacing,8,18),
            shipWidth=Mathf.Max(4,shipWidth),shipLength=Mathf.Max(6,shipLength),accessLength=Mathf.Max(4,accessLength),safetyMargin=Mathf.Max(1,safetyMargin),
            maximumLandingSlope=Mathf.Clamp(maximumLandingSlope,1,12),maximumLandingVariation=Mathf.Clamp(maximumLandingVariation,.3f,4),
            placementSampleSpacing=Mathf.Clamp(placementSampleSpacing,.5f,3)
        };
    }

    [Serializable]
    public struct SurfaceParameters
    {
        public float mappingRadius,elevationScale,tileSize,minimumTerrainHeight,terrainHeightRange;
        public float broadReliefHeight,landformSpacing,colonyPlateauHeight;
        public float smallHillHeight,smallHillSpacing;
        public int heightmapResolution,alphamapResolution;
        public float minimumRiverWidth,maximumRiverWidth;
        public float minimumBuildableArea,minimumInteriorSize,buildableSlope,buildableCellSize,plainRadius,plainBlend,objectCellSize;
        public float groveSpacing,groveRadius,treeSpacing;
        public float shipWidth,shipLength,accessLength,safetyMargin,maximumLandingSlope,maximumLandingVariation,placementSampleSpacing;
    }
}
