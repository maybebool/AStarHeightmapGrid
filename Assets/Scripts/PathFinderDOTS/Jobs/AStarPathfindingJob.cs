using PathFinderDOTS.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PathFinderDOTS.Jobs {

    [BurstCompile(
        CompileSynchronously = false,
        FloatMode = FloatMode.Fast,
        FloatPrecision = FloatPrecision.Standard,
        DisableSafetyChecks = true,
        OptimizeFor = OptimizeFor.Performance
    )]
    public struct AStarPathfindingJob : IJob {
        
        [NativeDisableParallelForRestriction]
        public NativeArray<PathNodeComponent> Nodes;

        [NativeDisableParallelForRestriction] public NativeArray<PathNodeCost> Costs;
        [ReadOnly] public NativeArray<float> TerrainHeights;
        
        public int Width;
        public int Height;
        public float CellSize;
        
        public int2 StartPos;
        public int2 EndPos;
        public float FlyCostMultiplier;
        
        public NativeList<int> ResultPath;
        public NativeList<int> OpenList;
        
        private const float DIAGONAL_FACTOR = 1.414f; // sqrt(2)
        private const float CARDINAL_FACTOR = 1.0f;
        private const float OCTILE_FACTOR = 0.414f; // (sqrt(2) - 1)

        public void Execute() {
            if (!IsValidPosition(StartPos) || !IsValidPosition(EndPos))
                return;

            var startIndex = GetIndex(StartPos.x, StartPos.y);
            var endIndex = GetIndex(EndPos.x, EndPos.y);

            if (Nodes[startIndex].IsWalkable == 0 || Nodes[endIndex].IsWalkable == 0)
                return;
            
            Costs[startIndex] = new PathNodeCost {
                GCost = 0,
                HCost = OctileDistance(StartPos, EndPos) * CellSize,
                FlyCost = 0,
                ParentIndex = -1
            };
            
            var startNode = Nodes[startIndex];
            startNode.IsInOpenList = 1;
            Nodes[startIndex] = startNode;

            OpenList.Clear();
            OpenList.Add(startIndex);

            var iterations = 0;
            var maxIterations = Width * Height;

            while (OpenList.Length > 0 && iterations++ < maxIterations) {
                var currentIndex = PopLowestCostNode();

                if (currentIndex == endIndex) {
                    ReconstructPath(currentIndex);
                    return;
                }

                // Mark as processed (closed) using the IsProcessed flag
                var currentNode = Nodes[currentIndex];
                currentNode.IsProcessed = 1;
                currentNode.IsInOpenList = 0;
                Nodes[currentIndex] = currentNode;

                ProcessNeighborsOptimized(currentIndex, endIndex);
            }
        }

        [BurstCompile]
        private void ProcessNeighborsOptimized(int currentIndex, int endIndex) {
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
            float currentGCost, int2 offset, int endIndex) {
            int2 horizontal = new int2(offset.x, 0);
            int2 vertical = new int2(0, offset.y);

            int2 horizontalPos = currentPos + horizontal;
            int2 verticalPos = currentPos + vertical;

            var horizontalWalkable = false;
            var verticalWalkable = false;

            if (IsValidPosition(horizontalPos)) {
                var horizontalIndex = GetIndex(horizontalPos.x, horizontalPos.y);
                horizontalWalkable = Nodes[horizontalIndex].IsWalkable == 1;
            }

            if (IsValidPosition(verticalPos)) {
                var verticalIndex = GetIndex(verticalPos.x, verticalPos.y);
                verticalWalkable = Nodes[verticalIndex].IsWalkable == 1;
            }

            if (horizontalWalkable || verticalWalkable) {
                // Scale diagonal movement by actual world distance
                var diagonalDistance = DIAGONAL_FACTOR * CellSize;
                CheckNeighbor(currentPos, currentIndex, currentHeight, currentGCost,
                    offset, diagonalDistance, endIndex);
            }
        }

        [BurstCompile]
        private void CheckNeighbor(int2 currentPos, int currentIndex, float currentHeight,
            float currentGCost, int2 offset, float movementCost, int endIndex) {
            
            int2 neighborPos = currentPos + offset;

            if (!IsValidPosition(neighborPos))
                return;
            
            var neighborIndex = GetIndex(neighborPos.x, neighborPos.y);
            var neighborNode = Nodes[neighborIndex];

            if (neighborNode.IsWalkable == 0 || neighborNode.IsProcessed == 1)
                return;

            var heightDiff = TerrainHeights[neighborIndex] - currentHeight;
            float flyCost = 0;
            
            if (heightDiff > 0) {
                flyCost = heightDiff * FlyCostMultiplier;
            }

            var newGCost = currentGCost + movementCost + flyCost;
            var existingGCost = Costs[neighborIndex].GCost;

            if (newGCost < existingGCost) {
                // Scale heuristic by cell size for consistency
                var hCost = OctileDistance(neighborPos, GetGridPosition(endIndex)) * CellSize;

                Costs[neighborIndex] = new PathNodeCost {
                    GCost = newGCost,
                    HCost = hCost,
                    FlyCost = flyCost,
                    ParentIndex = currentIndex
                };
                
                if (neighborNode.IsInOpenList == 0) {
                    neighborNode.IsInOpenList = 1;
                    Nodes[neighborIndex] = neighborNode;
                    OpenList.Add(neighborIndex);
                }
            }
        }

        [BurstCompile]
        private int PopLowestCostNode() {
            if (OpenList.Length == 0)
                return -1;

            var bestIndex = 0;
            var bestNodeIndex = OpenList[0];
            var lowestCost = Costs[bestNodeIndex].FCost;

            for (int i = 1; i < OpenList.Length; i++) {
                var nodeIndex = OpenList[i];
                var cost = Costs[nodeIndex].FCost;

                if (cost < lowestCost || (math.abs(cost - lowestCost) < 0.001f &&
                                          Costs[nodeIndex].HCost < Costs[bestNodeIndex].HCost)) {
                    lowestCost = cost;
                    bestIndex = i;
                    bestNodeIndex = nodeIndex;
                }
            }

            OpenList.RemoveAtSwapBack(bestIndex);
            return bestNodeIndex;
        }

        [BurstCompile]
        private void ReconstructPath(int endIndex) {
            ResultPath.Clear();

            var currentIndex = endIndex;
            var safety = 0;
            const int maxPathLength = 10000;

            while (currentIndex != -1 && safety++ < maxPathLength) {
                ResultPath.Add(currentIndex);
                currentIndex = Costs[currentIndex].ParentIndex;
            }

            var halfLength = ResultPath.Length >> 1;
            for (int i = 0; i < halfLength; i++) {
                var temp = ResultPath[i];
                var swapIndex = ResultPath.Length - 1 - i;
                ResultPath[i] = ResultPath[swapIndex];
                ResultPath[swapIndex] = temp;
            }
        }

        /// <summary>
        /// Octile distance - returns grid steps, not world units
        /// World scaling is applied separately
        /// </summary>
        [BurstCompile]
        private float OctileDistance(int2 a, int2 b) {
            var dx = math.abs(a.x - b.x);
            var dy = math.abs(a.y - b.y);

            var max = math.max(dx, dy);
            var min = math.min(dx, dy);
            return max + OCTILE_FACTOR * min;
        }

        [BurstCompile]
        private int GetIndex(int x, int y) {
            return y * Width + x;
        }

        [BurstCompile]
        private int2 GetGridPosition(int index) {
            return new int2(index % Width, index / Width);
        }

        [BurstCompile]
        private bool IsValidPosition(int2 position) {
            return position.x >= 0 && position.x < Width &&
                   position.y >= 0 && position.y < Height;
        }
    }
}