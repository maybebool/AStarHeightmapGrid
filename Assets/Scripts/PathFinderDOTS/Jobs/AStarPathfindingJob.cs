using PathFinderDOTS.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PathFinderDOTS.Jobs
{
    /// <summary>
    /// A* pathfinding job with properly scaled costs based on actual world distances
    /// </summary>
    [BurstCompile(
        CompileSynchronously = false,
        FloatMode = FloatMode.Fast,
        FloatPrecision = FloatPrecision.Standard,
        DisableSafetyChecks = true,
        OptimizeFor = OptimizeFor.Performance
    )]
    public struct AStarPathfindingJob : IJob
    {
        // Grid data
        [ReadOnly] public NativeArray<PathNodeComponent> Nodes;
        [NativeDisableParallelForRestriction] 
        public NativeArray<PathNodeCost> Costs;
        [ReadOnly] public NativeArray<float> TerrainHeights;
        
        // Grid configuration
        public int Width;
        public int Height;
        public float CellSize; // ADD THIS: Actual world size of each cell
        
        // Pathfinding parameters
        public int2 StartPos;
        public int2 EndPos;
        public float FlyCostMultiplier; // Now this can be a reasonable value like 2.0-5.0
        
        // Result and working data
        public NativeList<int> ResultPath;
        public NativeList<int> OpenList;
        public NativeHashMap<int, byte> ClosedSet;
        
        // Movement cost constants - now these will be multiplied by actual distances
        private const float DIAGONAL_FACTOR = 1.414f; // sqrt(2)
        private const float CARDINAL_FACTOR = 1.0f;
        private const float OCTILE_FACTOR = 0.414f; // (sqrt(2) - 1)
        
        public void Execute()
        {
            if (!IsValidPosition(StartPos) || !IsValidPosition(EndPos))
                return;
            
            int startIndex = GetIndex(StartPos);
            int endIndex = GetIndex(EndPos);
            
            if (Nodes[startIndex].IsWalkable == 0 || Nodes[endIndex].IsWalkable == 0)
                return;
            
            // Initialize start node - scale heuristic by cell size
            Costs[startIndex] = new PathNodeCost
            {
                GCost = 0,
                HCost = OctileDistance(StartPos, EndPos) * CellSize,
                FlyCost = 0,
                ParentIndex = -1
            };
            
            OpenList.Clear();
            ClosedSet.Clear();
            OpenList.Add(startIndex);
            
            int iterations = 0;
            int maxIterations = Width * Height;
            
            while (OpenList.Length > 0 && iterations++ < maxIterations)
            {
                int currentIndex = PopLowestCostNode();
                
                if (currentIndex == endIndex)
                {
                    ReconstructPath(currentIndex);
                    return;
                }
                
                ClosedSet.TryAdd(currentIndex, 1);
                ProcessNeighborsOptimized(currentIndex, endIndex);
            }
        }
        
        [BurstCompile]
        private void ProcessNeighborsOptimized(int currentIndex, int endIndex)
        {
            int2 currentPos = GetGridPosition(currentIndex);
            float currentHeight = TerrainHeights[currentIndex];
            float currentGCost = Costs[currentIndex].GCost;
            
            // Process cardinal directions first
            CheckNeighbor(currentPos, currentIndex, currentHeight, currentGCost, 
                         new int2(0, -1), CARDINAL_FACTOR * CellSize, endIndex);
            CheckNeighbor(currentPos, currentIndex, currentHeight, currentGCost, 
                         new int2(-1, 0), CARDINAL_FACTOR * CellSize, endIndex);
            CheckNeighbor(currentPos, currentIndex, currentHeight, currentGCost, 
                         new int2(1, 0), CARDINAL_FACTOR * CellSize, endIndex);
            CheckNeighbor(currentPos, currentIndex, currentHeight, currentGCost, 
                         new int2(0, 1), CARDINAL_FACTOR * CellSize, endIndex);
            
            // Then process diagonals with corner-cutting check
            CheckDiagonalNeighbor(currentPos, currentIndex, currentHeight, currentGCost, 
                                 new int2(-1, -1), endIndex);
            CheckDiagonalNeighbor(currentPos, currentIndex, currentHeight, currentGCost, 
                                 new int2(1, -1), endIndex);
            CheckDiagonalNeighbor(currentPos, currentIndex, currentHeight, currentGCost, 
                                 new int2(-1, 1), endIndex);
            CheckDiagonalNeighbor(currentPos, currentIndex, currentHeight, currentGCost, 
                                 new int2(1, 1), endIndex);
        }
        
        [BurstCompile]
        private void CheckDiagonalNeighbor(int2 currentPos, int currentIndex, float currentHeight, 
                                          float currentGCost, int2 offset, int endIndex)
        {
            int2 horizontal = new int2(offset.x, 0);
            int2 vertical = new int2(0, offset.y);
            
            int2 horizontalPos = currentPos + horizontal;
            int2 verticalPos = currentPos + vertical;
            
            bool horizontalWalkable = false;
            bool verticalWalkable = false;
            
            if (IsValidPosition(horizontalPos))
            {
                int horizontalIndex = GetIndex(horizontalPos);
                horizontalWalkable = Nodes[horizontalIndex].IsWalkable == 1;
            }
            
            if (IsValidPosition(verticalPos))
            {
                int verticalIndex = GetIndex(verticalPos);
                verticalWalkable = Nodes[verticalIndex].IsWalkable == 1;
            }
            
            if (horizontalWalkable || verticalWalkable)
            {
                // Scale diagonal movement by actual world distance
                float diagonalDistance = DIAGONAL_FACTOR * CellSize;
                CheckNeighbor(currentPos, currentIndex, currentHeight, currentGCost, 
                            offset, diagonalDistance, endIndex);
            }
        }
        
        [BurstCompile]
        private void CheckNeighbor(int2 currentPos, int currentIndex, float currentHeight, 
                                   float currentGCost, int2 offset, float movementCost, int endIndex)
        {
            int2 neighborPos = currentPos + offset;
            
            if (!IsValidPosition(neighborPos))
                return;
            
            int neighborIndex = GetIndex(neighborPos);
            
            if (Nodes[neighborIndex].IsWalkable == 0 || ClosedSet.ContainsKey(neighborIndex))
                return;
            
            // Calculate costs with proper scaling
            float heightDiff = TerrainHeights[neighborIndex] - currentHeight;
            
            // IMPROVED FLY COST CALCULATION
            // Now the fly cost is proportional to the movement cost
            // This makes the multiplier more intuitive (e.g., 2.0 means climbing costs 2x horizontal movement)
            float flyCost = 0;
            if (heightDiff > 0)
            {
                // Option 1: Linear scaling based on height difference
                flyCost = heightDiff * FlyCostMultiplier;
                
                // Option 2: Scale relative to movement distance for more consistent behavior
                // float slopeAngle = math.atan2(heightDiff, movementCost);
                // flyCost = math.tan(slopeAngle) * movementCost * FlyCostMultiplier;
                
                // Option 3: Quadratic scaling for steeper penalties
                // flyCost = (heightDiff * heightDiff / CellSize) * FlyCostMultiplier;
            }
            
            float newGCost = currentGCost + movementCost + flyCost;
            
            float existingGCost = Costs[neighborIndex].GCost;
            
            if (newGCost < existingGCost)
            {
                // Scale heuristic by cell size for consistency
                float hCost = OctileDistance(neighborPos, GetGridPosition(endIndex)) * CellSize;
                
                Costs[neighborIndex] = new PathNodeCost
                {
                    GCost = newGCost,
                    HCost = hCost,
                    FlyCost = flyCost,
                    ParentIndex = currentIndex
                };
                
                if (!IsInOpenList(neighborIndex))
                {
                    OpenList.Add(neighborIndex);
                }
            }
        }
        
        [BurstCompile]
        private int PopLowestCostNode()
        {
            if (OpenList.Length == 0)
                return -1;
                
            int bestIndex = 0;
            int bestNodeIndex = OpenList[0];
            float lowestCost = Costs[bestNodeIndex].FCost;
            
            for (int i = 1; i < OpenList.Length; i++)
            {
                int nodeIndex = OpenList[i];
                float cost = Costs[nodeIndex].FCost;
                
                if (cost < lowestCost || (math.abs(cost - lowestCost) < 0.001f && 
                    Costs[nodeIndex].HCost < Costs[bestNodeIndex].HCost))
                {
                    lowestCost = cost;
                    bestIndex = i;
                    bestNodeIndex = nodeIndex;
                }
            }
            
            OpenList.RemoveAtSwapBack(bestIndex);
            return bestNodeIndex;
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
            int safety = 0;
            const int maxPathLength = 10000;
            
            while (currentIndex != -1 && safety++ < maxPathLength)
            {
                ResultPath.Add(currentIndex);
                currentIndex = Costs[currentIndex].ParentIndex;
            }
            
            int halfLength = ResultPath.Length >> 1;
            for (int i = 0; i < halfLength; i++)
            {
                int temp = ResultPath[i];
                int swapIndex = ResultPath.Length - 1 - i;
                ResultPath[i] = ResultPath[swapIndex];
                ResultPath[swapIndex] = temp;
            }
        }
        
        /// <summary>
        /// Octile distance - returns grid steps, not world units
        /// World scaling is applied separately
        /// </summary>
        [BurstCompile]
        private float OctileDistance(int2 a, int2 b)
        {
            int dx = math.abs(a.x - b.x);
            int dy = math.abs(a.y - b.y);
            
            int max = math.max(dx, dy);
            int min = math.min(dx, dy);
            return max + OCTILE_FACTOR * min;
        }
        
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