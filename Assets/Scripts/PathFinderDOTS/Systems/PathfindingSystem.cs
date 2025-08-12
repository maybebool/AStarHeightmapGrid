using PathFinderDOTS.Authoring;
using PathFinderDOTS.Components;
using PathFinderDOTS.Data;
using Unity.Collections;
using Unity.Entities;

namespace PathFinderDOTS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PathfindingSystem : ISystem
    {
        private PathGridData _gridData;
        private bool _isInitialized;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GridConfiguration>();
            state.RequireForUpdate<PathGridTag>();
        }
        
        public void OnUpdate(ref SystemState state)
        {
            if (!_isInitialized)
            {
                InitializeGrid(ref state);
            }
        }
        
        private void InitializeGrid(ref SystemState state)
        {
            var gridConfig = SystemAPI.GetSingleton<GridConfiguration>();
            
            _gridData = new PathGridData(
                gridConfig.Width,
                gridConfig.Height,
                gridConfig.CellSize,
                gridConfig.Origin,
                Allocator.Persistent
            );
            
            // Sample terrain heights if available
            Entity gridEntity = SystemAPI.GetSingletonEntity<PathGridTag>();
            if (state.EntityManager.HasComponent<ManagedTerrainReference>(gridEntity))
            {
                var terrainRef = state.EntityManager.GetComponentData<ManagedTerrainReference>(gridEntity);
                if (terrainRef.TerrainInfo != null)
                {
                    SampleTerrainHeights(terrainRef.TerrainInfo, ref _gridData);
                }
            }
            
            _isInitialized = true;
        }
        
        private void SampleTerrainHeights(PathFinder.TerrainInfo terrainInfo, ref PathGridData grid)
        {
            var heights = terrainInfo.SampleHeights(grid.Width, true);
            
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    int index = grid.GetIndex(x, y);
                    grid.TerrainHeights[index] = heights[x, y];
                    
                    var node = grid.Nodes[index];
                    node.TerrainHeight = heights[x, y];
                    grid.Nodes[index] = node;
                }
            }
        }
        
        public void OnDestroy(ref SystemState state)
        {
            if (_isInitialized)
            {
                _gridData.Dispose();
            }
        }
    }
}