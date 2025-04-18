using UnityEngine;


namespace Heightmap {
    public class PathNode {
        public Vector2Int Index;
        public float GCost { get; set; }  
        public float HCost { get; set; }  
        public float FCost => GCost + HCost + FlyCost;
        public float FlyCost { get; set; } 
    
        public bool IsWall { get; set; }

        public PathNode SourceNode;

        public PathNode(Vector2Int index) {
            Index = index;
            // X = x;
            // Y = y;
            GCost = float.MaxValue;
        }
    }
}
