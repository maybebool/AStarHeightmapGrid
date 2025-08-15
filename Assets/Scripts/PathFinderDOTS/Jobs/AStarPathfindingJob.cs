// AStarPathfindingJob.cs - Optimized with Octile Distance
using PathFinderDOTS.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PathFinderDOTS.Jobs
{
    /// <summary>
    /// Highly optimized A* pathfinding job using Octile distance
    /// Provides the best balance between performance and path quality for grid-based movement
    /// </summary>
    [BurstCompile(
        CompileSynchronously = false,        // Allow async compilation for better editor performance
        FloatMode = FloatMode.Fast,          // Faster math at slight precision cost
        FloatPrecision = FloatPrecision.Standard, // Standard precision for Octile calculations
        DisableSafetyChecks = true,          // Disable in builds for performance
        OptimizeFor = OptimizeFor.Performance // Prioritize speed over code size
    )]
    public struct AStarPathfindingJob : IJob
    {
        // Grid data - separated to avoid aliasing
        [ReadOnly] public NativeArray<PathNodeComponent> Nodes;
        [NativeDisableParallelForRestriction] 
        public NativeArray<PathNodeCost> Costs;
        [ReadOnly] public NativeArray<float> TerrainHeights;
        
        // Grid configuration
        public int Width;
        public int Height;
        
        // Pathfinding parameters
        public int2 StartPos;
        public int2 EndPos;
        public float FlyCostMultiplier;
        
        // Result and working data
        public NativeList<int> ResultPath;
        public NativeList<int> OpenList;
        public NativeHashMap<int, byte> ClosedSet;
        
        // Constants for diagonal movement
        private const float DIAGONAL_COST = 1.414f;
        private const float CARDINAL_COST = 1.0f;
        private const float OCTILE_FACTOR = 0.414f; // (sqrt(2) - 1)
        
        public void Execute()
        {
            // Quick validation
            if (!IsValidPosition(StartPos) || !IsValidPosition(EndPos))
                return;
            
            int startIndex = GetIndex(StartPos);
            int endIndex = GetIndex(EndPos);
            
            // Early exit if start or end are unwalkable
            if (Nodes[startIndex].IsWalkable == 0 || Nodes[endIndex].IsWalkable == 0)
                return;
            
            // Initialize start node with Octile distance heuristic
            Costs[startIndex] = new PathNodeCost
            {
                GCost = 0,
                HCost = OctileDistance(StartPos, EndPos),
                FlyCost = 0,
                ParentIndex = -1
            };
            
            OpenList.Clear();
            ClosedSet.Clear();
            OpenList.Add(startIndex);
            
            int iterations = 0;
            int maxIterations = Width * Height / 2; // Reasonable upper bound
            
            while (OpenList.Length > 0 && iterations++ < maxIterations)
            {
                // Get and remove lowest cost node efficiently
                int currentIndex = PopLowestCostNode();
                
                // Goal check
                if (currentIndex == endIndex)
                {
                    ReconstructPath(currentIndex);
                    return;
                }
                
                // Add to closed set
                ClosedSet.TryAdd(currentIndex, 1);
                
                // Process all 8 neighbors
                ProcessNeighborsOptimized(currentIndex, endIndex);
            }
        }
        
        [BurstCompile]
        private void ProcessNeighborsOptimized(int currentIndex, int endIndex)
        {
            int2 currentPos = GetGridPosition(currentIndex);
            float currentHeight = TerrainHeights[currentIndex];
            float currentGCost = Costs[currentIndex].GCost;
            
            // Unrolled neighbor checks for better CPU branch prediction
            // Process diagonals first (often better paths)
            CheckNeighbor(currentPos, currentIndex, currentHeight, currentGCost, new int2(-1, -1), DIAGONAL_COST, endIndex);
            CheckNeighbor(currentPos, currentIndex, currentHeight, currentGCost, new int2(1, -1), DIAGONAL_COST, endIndex);
            CheckNeighbor(currentPos, currentIndex, currentHeight, currentGCost, new int2(-1, 1), DIAGONAL_COST, endIndex);
            CheckNeighbor(currentPos, currentIndex, currentHeight, currentGCost, new int2(1, 1), DIAGONAL_COST, endIndex);
            
            // Then process cardinal directions
            CheckNeighbor(currentPos, currentIndex, currentHeight, currentGCost, new int2(0, -1), CARDINAL_COST, endIndex);
            CheckNeighbor(currentPos, currentIndex, currentHeight, currentGCost, new int2(-1, 0), CARDINAL_COST, endIndex);
            CheckNeighbor(currentPos, currentIndex, currentHeight, currentGCost, new int2(1, 0), CARDINAL_COST, endIndex);
            CheckNeighbor(currentPos, currentIndex, currentHeight, currentGCost, new int2(0, 1), CARDINAL_COST, endIndex);
        }
        
        [BurstCompile]
        private void CheckNeighbor(int2 currentPos, int currentIndex, float currentHeight, 
                                   float currentGCost, int2 offset, float movementCost, int endIndex)
        {
            int2 neighborPos = currentPos + offset;
            
            // Bounds check
            if (neighborPos.x < 0 || neighborPos.x >= Width || 
                neighborPos.y < 0 || neighborPos.y >= Height)
                return;
            
            int neighborIndex = GetIndex(neighborPos);
            
            // Skip unwalkable or closed nodes
            if (Nodes[neighborIndex].IsWalkable == 0 || ClosedSet.ContainsKey(neighborIndex))
                return;
            
            // Calculate costs including terrain elevation
            float heightDiff = TerrainHeights[neighborIndex] - currentHeight;
            // Use branchless selection for fly cost
            float flyCost = math.select(0, heightDiff * FlyCostMultiplier, heightDiff > 0);
            float newGCost = currentGCost + movementCost + flyCost;
            
            // Only update if we found a better path
            if (newGCost < Costs[neighborIndex].GCost)
            {
                // Calculate heuristic using Octile distance
                float hCost = OctileDistance(neighborPos, GetGridPosition(endIndex));
                
                Costs[neighborIndex] = new PathNodeCost
                {
                    GCost = newGCost,
                    HCost = hCost,
                    FlyCost = flyCost,
                    ParentIndex = currentIndex
                };
                
                // Add to open list if not already there
                if (!IsInOpenList(neighborIndex))
                {
                    OpenList.Add(neighborIndex);
                }
            }
        }
        
        [BurstCompile]
        private int PopLowestCostNode()
        {
            int bestIndex = 0;
            int bestNodeIndex = OpenList[0];
            float lowestCost = Costs[bestNodeIndex].FCost;
            
            // Find the node with lowest F cost
            for (int i = 1; i < OpenList.Length; i++)
            {
                int nodeIndex = OpenList[i];
                float cost = Costs[nodeIndex].FCost;
                
                if (cost < lowestCost)
                {
                    lowestCost = cost;
                    bestIndex = i;
                    bestNodeIndex = nodeIndex;
                }
            }
            
            // Remove from open list and return
            OpenList.RemoveAtSwapBack(bestIndex);
            return bestNodeIndex;
        }
        
        [BurstCompile]
        private bool IsInOpenList(int nodeIndex)
        {
            // Linear search - acceptable for small open lists
            // Consider NativeHashSet for very large grids
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
            int safety = 0;
            const int maxPathLength = 1000;
            
            // Build path backwards from end to start
            while (currentIndex != -1 && safety++ < maxPathLength)
            {
                ResultPath.Add(currentIndex);
                currentIndex = Costs[currentIndex].ParentIndex;
            }
            
            // Reverse path to get start->end order
            int halfLength = ResultPath.Length >> 1; // Bit shift for fast division by 2
            for (int i = 0; i < halfLength; i++)
            {
                int temp = ResultPath[i];
                int swapIndex = ResultPath.Length - 1 - i;
                ResultPath[i] = ResultPath[swapIndex];
                ResultPath[swapIndex] = temp;
            }
        }
        
        /// <summary>
        /// Octile distance heuristic - perfect for 8-directional grid movement
        /// Faster than Euclidean but maintains accuracy for grid-based pathfinding
        /// </summary>
        [BurstCompile]
        private float OctileDistance(int2 a, int2 b)
        {
            int dx = math.abs(a.x - b.x);
            int dy = math.abs(a.y - b.y);
            
            // Octile distance formula:
            // D * max(dx,dy) + (D2-D) * min(dx,dy)
            // Where D=1 (cardinal) and D2=1.414 (diagonal)
            // Simplified: max + 0.414 * min
            
            int max = math.max(dx, dy);
            int min = math.min(dx, dy);
            return max + OCTILE_FACTOR * min;
            
            // Alternative formula (equivalent but different calculation):
            // return (dx + dy) - (1 - OCTILE_FACTOR) * math.min(dx, dy);
        }
        
        // Helper methods
        [BurstCompile]
        private int GetIndex(int2 pos)
        {
            return pos.y * Width + pos.x;
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