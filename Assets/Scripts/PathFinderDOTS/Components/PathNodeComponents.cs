using Unity.Entities;
using Unity.Mathematics;

namespace PathFinderDOTS.Components {
    
    public struct PathNodeComponent : IComponentData {
        public int2 GridPosition;
        public float TerrainHeight;
        public byte IsWalkable; 
        public byte IsProcessed; 
    }

    // Pathfinding calculation data - separated for better cache usage
    public struct PathNodeCost : IComponentData {
        public float GCost;
        public float HCost;
        public float FlyCost;
        public int ParentIndex; 

        public float FCost => GCost + HCost + FlyCost;
    }

    // Grid configuration singleton
    public struct GridConfiguration : IComponentData {
        public int Width;
        public int Height;
        public float CellSize;
        public float3 Origin;
        public float FlyCostMultiplier;
    }

    // Tag component for the grid entity
    public struct PathGridTag : IComponentData {
    }

    // Buffer element for storing neighbor indices
    public struct NeighborElement : IBufferElementData {
        public int NeighborIndex;
        public byte IsDiagonal; 
    }
}