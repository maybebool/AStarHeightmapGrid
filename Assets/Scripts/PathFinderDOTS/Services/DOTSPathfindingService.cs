// Optimized DOTSPathfindingService.cs - Actually fast version

using System.Collections.Generic;
using System.Diagnostics;
using PathFinderDOTS.Components;
using PathFinderDOTS.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PathFinderDOTS.Services
{
    /// <summary>
    /// Optimized DOTS Pathfinding Service with persistent memory and minimal allocations
    /// </summary>
    public class DOTSPathfindingService : MonoBehaviour
    {
        private PathGridData _gridData;
        private bool _isInitialized = false;
        
        // Configuration
        private int _gridSize;
        private float _cellSize;
        private float _flyCostMultiplier;
        private PathFinder.TerrainInfo _terrainInfo;
        
        // Persistent arrays for pathfinding - no allocations during runtime!
        private NativeArray<PathNodeCost> _workingCosts;
        private NativeList<int> _resultPath;
        private NativeList<int> _openList;
        private NativeHashMap<int, byte> _closedSet;
        private NativeArray<int> _neighborIndices;
        
        // For performance comparison
        private Stopwatch _stopwatch = new Stopwatch();
        
        public void Initialize(int gridSize, float cellSize, float flyCostMultiplier, PathFinder.TerrainInfo terrainInfo)
        {
            _gridSize = gridSize;
            _cellSize = cellSize;
            _flyCostMultiplier = flyCostMultiplier;
            _terrainInfo = terrainInfo;
            
            InitializeGrid();
            InitializePersistentArrays();
        }
        
        private void InitializeGrid()
        {
            if (_isInitialized)
            {
                CleanupPersistentArrays();
                _gridData.Dispose();
            }
            
            Vector3 origin = _terrainInfo != null ? _terrainInfo.transform.position : Vector3.zero;
            
            _gridData = new PathGridData(
                _gridSize,
                _gridSize,
                _cellSize,
                origin,
                Allocator.Persistent
            );
            
            // Sample terrain heights once
            if (_terrainInfo != null)
            {
                var heights = _terrainInfo.SampleHeights(_gridSize, true);
                for (int y = 0; y < _gridSize; y++)
                {
                    for (int x = 0; x < _gridSize; x++)
                    {
                        int index = _gridData.GetIndex(x, y);
                        _gridData.TerrainHeights[index] = heights[x, y];
                        
                        var node = _gridData.Nodes[index];
                        node.TerrainHeight = heights[x, y];
                        _gridData.Nodes[index] = node;
                    }
                }
            }
            
            _isInitialized = true;
            UnityEngine.Debug.Log($"DOTSPathfindingService initialized with grid size {_gridSize}x{_gridSize}");
        }
        
        private void InitializePersistentArrays()
        {
            int totalNodes = _gridSize * _gridSize;
            
            // Allocate once, reuse forever
            _workingCosts = new NativeArray<PathNodeCost>(totalNodes, Allocator.Persistent);
            _resultPath = new NativeList<int>(256, Allocator.Persistent); // Reasonable initial capacity
            _openList = new NativeList<int>(totalNodes / 4, Allocator.Persistent); // Usually don't need full grid
            _closedSet = new NativeHashMap<int, byte>(totalNodes / 2, Allocator.Persistent);
            _neighborIndices = new NativeArray<int>(8, Allocator.Persistent); // For 8 neighbors
            
            // Initialize working costs
            ResetWorkingCosts();
        }
        
        private void ResetWorkingCosts()
        {
            for (int i = 0; i < _workingCosts.Length; i++)
            {
                _workingCosts[i] = new PathNodeCost
                {
                    GCost = float.MaxValue,
                    HCost = 0,
                    FlyCost = 0,
                    ParentIndex = -1
                };
            }
        }
        
        /// <summary>
        /// Optimized path calculation with minimal allocations
        /// </summary>
        public List<PathFinder.PathNode> CalculatePath(Vector2Int startPos, Vector2Int endPos)
        {
            if (!_isInitialized)
            {
                UnityEngine.Debug.LogError("DOTSPathfindingService not initialized!");
                return null;
            }
            
            _stopwatch.Restart();
            
            // Validate positions
            if (!IsValidGridPosition(startPos) || !IsValidGridPosition(endPos))
            {
                UnityEngine.Debug.LogWarning($"Invalid path positions: start={startPos}, end={endPos}");
                return null;
            }
            
            // Clear working data (no allocations!)
            _resultPath.Clear();
            _openList.Clear();
            _closedSet.Clear();
            
            // Create and run the optimized job
            var pathfindingJob = new OptimizedAStarJob
            {
                Nodes = _gridData.Nodes,
                Costs = _workingCosts,
                TerrainHeights = _gridData.TerrainHeights,
                Width = _gridData.Width,
                Height = _gridData.Height,
                StartPos = new int2(startPos.x, startPos.y),
                EndPos = new int2(endPos.x, endPos.y),
                FlyCostMultiplier = _flyCostMultiplier,
                ResultPath = _resultPath,
                OpenList = _openList,
                ClosedSet = _closedSet
            };
            
            // Run with Burst compilation
            JobHandle handle = pathfindingJob.Schedule();
            handle.Complete();
            
            _stopwatch.Stop();
            
            // Convert result (minimal allocation here)
            List<PathFinder.PathNode> legacyPath = null;
            
            if (_resultPath.Length > 0)
            {
                legacyPath = new List<PathFinder.PathNode>(_resultPath.Length);
                
                for (int i = 0; i < _resultPath.Length; i++)
                {
                    int index = _resultPath[i];
                    int2 gridPos = _gridData.GetGridPosition(index);
                    
                    var pathNode = new PathFinder.PathNode(new Vector2Int(gridPos.x, gridPos.y))
                    {
                        GCost = _workingCosts[index].GCost,
                        HCost = _workingCosts[index].HCost,
                        FlyCost = _workingCosts[index].FlyCost
                    };
                    
                    legacyPath.Add(pathNode);
                }
                
                UnityEngine.Debug.Log($"DOTS Path found: {legacyPath.Count} nodes in {_stopwatch.ElapsedMilliseconds}ms");
            }
            
            // Reset costs for next use
            ResetWorkingCosts();
            
            return legacyPath;
        }
        
        public List<Vector3> ConvertPathToWorldPositions(List<PathFinder.PathNode> path, float heightOffset)
        {
            if (path == null || path.Count == 0) return null;
            
            var worldPath = new List<Vector3>(path.Count);
            
            foreach (var node in path)
            {
                int index = _gridData.GetIndex(node.Index.x, node.Index.y);
                float terrainHeight = _gridData.TerrainHeights[index];
                
                Vector3 worldPos = _gridData.GridToWorldPosition(
                    new int2(node.Index.x, node.Index.y),
                    terrainHeight + heightOffset
                );
                
                worldPath.Add(worldPos);
            }
            
            return worldPath;
        }
        
        public Vector2Int WorldToGridPosition(Vector3 worldPos)
        {
            if (!_isInitialized) return Vector2Int.zero;
            int2 gridPos = _gridData.WorldToGridPosition(worldPos);
            return new Vector2Int(gridPos.x, gridPos.y);
        }
        
        public Vector3 GridToWorldPosition(Vector2Int gridPos, float yOffset = 0)
        {
            if (!_isInitialized) return Vector3.zero;
            return _gridData.GridToWorldPosition(new int2(gridPos.x, gridPos.y), yOffset);
        }
        
        public bool IsValidGridPosition(Vector2Int pos)
        {
            if (!_isInitialized) return false;
            return _gridData.IsValidPosition(new int2(pos.x, pos.y));
        }
        
        private void CleanupPersistentArrays()
        {
            if (_workingCosts.IsCreated) _workingCosts.Dispose();
            if (_resultPath.IsCreated) _resultPath.Dispose();
            if (_openList.IsCreated) _openList.Dispose();
            if (_closedSet.IsCreated) _closedSet.Dispose();
            if (_neighborIndices.IsCreated) _neighborIndices.Dispose();
        }
        
        private void OnDestroy()
        {
            if (_isInitialized)
            {
                CleanupPersistentArrays();
                _gridData.Dispose();
                _isInitialized = false;
            }
        }
    }
    
    /// <summary>
    /// Optimized A* Job with better memory access patterns
    /// </summary>
    [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    public struct OptimizedAStarJob : IJob
    {
        [ReadOnly] public NativeArray<PathNodeComponent> Nodes;
        [NativeDisableParallelForRestriction]
        public NativeArray<PathNodeCost> Costs;
        [ReadOnly] public NativeArray<float> TerrainHeights;
        
        public int Width;
        public int Height;
        public int2 StartPos;
        public int2 EndPos;
        public float FlyCostMultiplier;
        
        public NativeList<int> ResultPath;
        public NativeList<int> OpenList;
        public NativeHashMap<int, byte> ClosedSet;
        
        public void Execute()
        {
            int startIndex = GetIndex(StartPos);
            int endIndex = GetIndex(EndPos);
            
            // Early exit checks
            if (Nodes[startIndex].IsWalkable == 0 || Nodes[endIndex].IsWalkable == 0)
                return;
            
            // Initialize start node
            Costs[startIndex] = new PathNodeCost
            {
                GCost = 0,
                HCost = Heuristic(StartPos, EndPos),
                FlyCost = 0,
                ParentIndex = -1
            };
            
            OpenList.Add(startIndex);
            
            int iterations = 0;
            const int maxIterations = 5000;
            
            while (OpenList.Length > 0 && iterations++ < maxIterations)
            {
                // Get lowest cost node - optimized version
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
            var currentCost = Costs[currentIndex];
            
            // Unroll the neighbor loop for better performance
            CheckNeighbor(currentPos, currentHeight, currentCost, new int2(-1, -1), 1.414f, endIndex);
            CheckNeighbor(currentPos, currentHeight, currentCost, new int2(0, -1), 1.0f, endIndex);
            CheckNeighbor(currentPos, currentHeight, currentCost, new int2(1, -1), 1.414f, endIndex);
            CheckNeighbor(currentPos, currentHeight, currentCost, new int2(-1, 0), 1.0f, endIndex);
            CheckNeighbor(currentPos, currentHeight, currentCost, new int2(1, 0), 1.0f, endIndex);
            CheckNeighbor(currentPos, currentHeight, currentCost, new int2(-1, 1), 1.414f, endIndex);
            CheckNeighbor(currentPos, currentHeight, currentCost, new int2(0, 1), 1.0f, endIndex);
            CheckNeighbor(currentPos, currentHeight, currentCost, new int2(1, 1), 1.414f, endIndex);
        }
        
        [BurstCompile]
        private void CheckNeighbor(int2 currentPos, float currentHeight, PathNodeCost currentCost, 
                                   int2 offset, float movementCost, int endIndex)
        {
            int2 neighborPos = currentPos + offset;
            
            if (neighborPos.x < 0 || neighborPos.x >= Width || 
                neighborPos.y < 0 || neighborPos.y >= Height)
                return;
            
            int neighborIndex = GetIndex(neighborPos);
            
            if (Nodes[neighborIndex].IsWalkable == 0)
                return;
            
            if (ClosedSet.ContainsKey(neighborIndex))
                return;
            
            float heightDiff = TerrainHeights[neighborIndex] - currentHeight;
            float flyCost = math.max(0, heightDiff * FlyCostMultiplier);
            float newGCost = currentCost.GCost + movementCost + flyCost;
            
            var neighborCost = Costs[neighborIndex];
            
            if (newGCost < neighborCost.GCost)
            {
                neighborCost.GCost = newGCost;
                neighborCost.FlyCost = flyCost;
                neighborCost.HCost = Heuristic(neighborPos, GetGridPosition(endIndex));
                neighborCost.ParentIndex = GetIndex(currentPos);
                Costs[neighborIndex] = neighborCost;
                
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
            int currentIndex = endIndex;
            int safety = 0;
            
            while (currentIndex != -1 && safety++ < 1000)
            {
                ResultPath.Add(currentIndex);
                currentIndex = Costs[currentIndex].ParentIndex;
            }
            
            // Reverse in place
            int halfLength = ResultPath.Length / 2;
            for (int i = 0; i < halfLength; i++)
            {
                (ResultPath[i], ResultPath[ResultPath.Length - 1 - i]) = (ResultPath[ResultPath.Length - 1 - i], ResultPath[i]);
            }
        }
        
        [BurstCompile]
        private int GetIndex(int2 pos) => pos.y * Width + pos.x;
        
        [BurstCompile]
        private int2 GetGridPosition(int index) => new int2(index % Width, index / Width);
        
        [BurstCompile]
        private float Heuristic(int2 a, int2 b)
        {
            // Manhattan distance is faster than Euclidean
            return math.abs(a.x - b.x) + math.abs(a.y - b.y);
        }
    }
}