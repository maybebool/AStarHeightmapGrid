using Unity.Entities;
using Unity.Mathematics;

namespace PathFinderDOTS.Components {
    // Main node component - kept small for cache efficiency
    public struct PathNodeComponent : IComponentData {
        public int2 GridPosition;
        public float TerrainHeight;
        public byte IsWalkable; // byte for better memory alignment than bool
        public byte IsProcessed; // For pathfinding state
    }

    // Pathfinding calculation data - separated for better cache usage
    public struct PathNodeCost : IComponentData {
        public float GCost;
        public float HCost;
        public float FlyCost;
        public int ParentIndex; // Index in the grid instead of reference

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
        public byte IsDiagonal; // 1 for diagonal, 0 for cardinal
    }
}