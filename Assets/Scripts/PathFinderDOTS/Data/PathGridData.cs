using PathFinderDOTS.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace PathFinderDOTS.Data
{
    [BurstCompile]
    public struct PathGridData : System.IDisposable
    {
        public NativeArray<PathNodeComponent> Nodes;
        public NativeArray<PathNodeCost> Costs;
        public NativeArray<float> TerrainHeights;
        
        public readonly int Width;
        public readonly int Height;
        public readonly float CellSize;
        public readonly float3 Origin;
        
        public PathGridData(int width, int height, float cellSize, float3 origin, Allocator allocator)
        {
            Width = width;
            Height = height;
            CellSize = cellSize;
            Origin = origin;
            
            int totalNodes = width * height;
            Nodes = new NativeArray<PathNodeComponent>(totalNodes, allocator);
            Costs = new NativeArray<PathNodeCost>(totalNodes, allocator);
            TerrainHeights = new NativeArray<float>(totalNodes, allocator);
            
            InitializeGrid();
        }
        
        private void InitializeGrid()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int index = GetIndex(x, y);
                    
                    Nodes[index] = new PathNodeComponent
                    {
                        GridPosition = new int2(x, y),
                        TerrainHeight = 0f,
                        IsWalkable = 1,
                        IsProcessed = 0
                    };
                    
                    Costs[index] = new PathNodeCost
                    {
                        GCost = float.MaxValue,
                        HCost = 0,
                        FlyCost = 0,
                        ParentIndex = -1
                    };
                }
            }
        }
        
        [BurstCompile]
        public int GetIndex(int x, int y)
        {
            return y * Width + x;
        }
        
        [BurstCompile]
        public int2 GetGridPosition(int index)
        {
            return new int2(index % Width, index / Width);
        }
        
        [BurstCompile]
        public bool IsValidPosition(int2 position)
        {
            return position.x >= 0 && position.x < Width && 
                   position.y >= 0 && position.y < Height;
        }
        
        [BurstCompile]
        public float3 GridToWorldPosition(int2 gridPos, float yOffset = 0)
        {
            return Origin + new float3(
                (gridPos.x + 0.5f) * CellSize,
                yOffset,
                (gridPos.y + 0.5f) * CellSize
            );
        }
        
        [BurstCompile]
        public int2 WorldToGridPosition(float3 worldPos)
        {
            float3 localPos = worldPos - Origin;
            return new int2(
                (int)math.floor(localPos.x / CellSize),
                (int)math.floor(localPos.z / CellSize)
            );
        }
        
        public void Dispose()
        {
            if (Nodes.IsCreated) Nodes.Dispose();
            if (Costs.IsCreated) Costs.Dispose();
            if (TerrainHeights.IsCreated) TerrainHeights.Dispose();
        }
    }
}