// Updated AStarPathfindingJob.cs

using PathFinderDOTS.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PathFinderDOTS
{
    [BurstCompile]
    public struct AStarPathfindingJob : IJob
    {
        // Grid data - separated to avoid aliasing
        [ReadOnly] public NativeArray<PathNodeComponent> Nodes;
        public NativeArray<PathNodeCost> Costs;  // Writable for pathfinding calculations
        [ReadOnly] public NativeArray<float> TerrainHeights;
        
        // Grid configuration
        public readonly int Width;
        public readonly int Height;
        
        // Pathfinding parameters
        public int2 StartPos;
        public int2 EndPos;
        public float FlyCostMultiplier;
        
        // Result and working data
        public NativeList<int> ResultPath;
        public NativeList<int> OpenList;
        public NativeHashMap<int, byte> ClosedSet;
        
        public void Execute()
        {
            ResultPath.Clear();
            
            if (!IsValidPosition(StartPos) || !IsValidPosition(EndPos))
                return;
            
            int startIndex = GetIndex(StartPos.x, StartPos.y);
            int endIndex = GetIndex(EndPos.x, EndPos.y);
            
            // Check if start or end nodes are walkable
            if (Nodes[startIndex].IsWalkable == 0 || Nodes[endIndex].IsWalkable == 0)
                return;
            
            // Reset costs
            for (int i = 0; i < Costs.Length; i++)
            {
                var cost = Costs[i];
                cost.GCost = float.MaxValue;
                cost.HCost = 0;
                cost.FlyCost = 0;
                cost.ParentIndex = -1;
                Costs[i] = cost;
            }
            
            // Initialize start node
            var startCost = Costs[startIndex];
            startCost.GCost = 0;
            startCost.HCost = math.distance(StartPos, EndPos);
            startCost.FlyCost = 0;
            startCost.ParentIndex = -1;
            Costs[startIndex] = startCost;
            
            OpenList.Clear();
            ClosedSet.Clear();
            OpenList.Add(startIndex);
            
            int iterations = 0;
            const int maxIterations = 10000; // Prevent infinite loops
            
            while (OpenList.Length > 0 && iterations < maxIterations)
            {
                iterations++;
                int currentIndex = GetLowestCostNodeIndex();
                
                if (currentIndex == endIndex)
                {
                    ReconstructPath(currentIndex);
                    return;
                }
                
                OpenList.RemoveAtSwapBack(GetOpenListIndex(currentIndex));
                ClosedSet.TryAdd(currentIndex, 1);
                
                ProcessNeighbors(currentIndex, endIndex);
            }
        }
        
        [BurstCompile]
        private void ProcessNeighbors(int currentIndex, int endIndex)
        {
            int2 currentPos = GetGridPosition(currentIndex);
            
            // Check all 8 neighbors
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    
                    int2 neighborPos = currentPos + new int2(dx, dy);
                    
                    if (!IsValidPosition(neighborPos))
                        continue;
                    
                    int neighborIndex = GetIndex(neighborPos.x, neighborPos.y);
                    
                    if (Nodes[neighborIndex].IsWalkable == 0)
                        continue;
                    
                    if (ClosedSet.ContainsKey(neighborIndex))
                        continue;
                    
                    bool isDiagonal = (dx != 0 && dy != 0);
                    float movementCost = isDiagonal ? 1.414f : 1.0f;
                    
                    var currentCost = Costs[currentIndex];
                    float newGCost = currentCost.GCost + movementCost;
                    
                    // Calculate fly cost based on height difference
                    float heightDiff = TerrainHeights[neighborIndex] - TerrainHeights[currentIndex];
                    float flyCost = math.max(0, heightDiff * FlyCostMultiplier);
                    
                    float totalCost = newGCost + flyCost;
                    
                    var neighborCost = Costs[neighborIndex];
                    
                    if (totalCost < neighborCost.GCost)
                    {
                        neighborCost.GCost = newGCost;
                        neighborCost.FlyCost = flyCost;
                        neighborCost.HCost = math.distance(neighborPos, GetGridPosition(endIndex));
                        neighborCost.ParentIndex = currentIndex;
                        Costs[neighborIndex] = neighborCost;
                        
                        if (!IsInOpenList(neighborIndex))
                        {
                            OpenList.Add(neighborIndex);
                        }
                    }
                }
            }
        }
        
        [BurstCompile]
        private int GetLowestCostNodeIndex()
        {
            int bestIndex = OpenList[0];
            float lowestCost = Costs[bestIndex].FCost;
            
            for (int i = 1; i < OpenList.Length; i++)
            {
                int index = OpenList[i];
                float cost = Costs[index].FCost;
                
                if (cost < lowestCost)
                {
                    lowestCost = cost;
                    bestIndex = index;
                }
            }
            
            return bestIndex;
        }
        
        [BurstCompile]
        private int GetOpenListIndex(int nodeIndex)
        {
            for (int i = 0; i < OpenList.Length; i++)
            {
                if (OpenList[i] == nodeIndex)
                    return i;
            }
            return -1;
        }
        
        [BurstCompile]
        private bool IsInOpenList(int nodeIndex)
        {
            for (int i = 0; i < OpenList.Length; i++)
            {
                if (OpenList[i] == nodeIndex)
                    return true;
            }
            return false;
        }
        
        [BurstCompile]
        private void ReconstructPath(int endIndex)
        {
            ResultPath.Clear();
            
            int currentIndex = endIndex;
            int iterations = 0;
            const int maxIterations = 1000;
            
            while (currentIndex != -1 && iterations < maxIterations)
            {
                ResultPath.Add(currentIndex);
                currentIndex = Costs[currentIndex].ParentIndex;
                iterations++;
            }
            
            // Reverse the path to go from start to end
            for (int i = 0; i < ResultPath.Length / 2; i++)
            {
                (ResultPath[i], ResultPath[ResultPath.Length - 1 - i]) = (ResultPath[ResultPath.Length - 1 - i], ResultPath[i]);
            }
        }
        
        // Helper methods
        [BurstCompile]
        private int GetIndex(int x, int y)
        {
            return y * Width + x;
        }
        
        [BurstCompile]
        private int2 GetGridPosition(int index)
        {
            return new int2(index % Width, index / Width);
        }
        
        [BurstCompile]
        private bool IsValidPosition(int2 position)
        {
            return position.x >= 0 && position.x < Width && 
                   position.y >= 0 && position.y < Height;
        }
    }
}