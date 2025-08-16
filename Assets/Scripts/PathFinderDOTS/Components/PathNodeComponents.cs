using Unity.Entities;
using Unity.Mathematics;

namespace PathFinderDOTS.Components {
    
    public struct PathNodeComponent : IComponentData {
        public byte IsWalkable; 
        public byte IsProcessed; 
        public byte IsInOpenList;
    }
    
    public struct PathNodeCost : IComponentData {
        public float GCost;
        public float HCost;
        public float FlyCost;
        public int ParentIndex; 

        public float FCost => GCost + HCost + FlyCost;
    }
    
    public struct GridConfiguration : IComponentData {
        public int Width;
        public int Height;
        public float CellSize;
        public float3 Origin;
        public float FlyCostMultiplier;
    }
    
    public struct PathGridTag : IComponentData {
    }
}