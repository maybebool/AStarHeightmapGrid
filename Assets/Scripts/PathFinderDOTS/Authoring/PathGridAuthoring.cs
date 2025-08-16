using PathFinder;
using PathFinderDOTS.Components;
using Unity.Entities;
using UnityEngine;

namespace PathFinderDOTS.Authoring {
    public class PathGridAuthoring : MonoBehaviour {
        [Header("Grid Configuration")] public int gridWidth = 64;
        public int gridHeight = 64;
        public float cellSize = 1.0f;
        public float flyCostMultiplier = 1.25f;

        [Header("Terrain Reference")] public TerrainInfo terrainInfo;

        class Baker : Baker<PathGridAuthoring> {
            public override void Bake(PathGridAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new GridConfiguration {
                    Width = authoring.gridWidth,
                    Height = authoring.gridHeight,
                    CellSize = authoring.cellSize,
                    Origin = authoring.transform.position,
                    FlyCostMultiplier = authoring.flyCostMultiplier
                });

                AddComponent(entity, new PathGridTag());

                // Add a managed component to hold the terrain reference
                AddComponentObject(entity, new ManagedTerrainReference {
                    TerrainInfo = authoring.terrainInfo
                });
            }
        }
    }

    public class ManagedTerrainReference : IComponentData {
        public TerrainInfo TerrainInfo;
    }
}