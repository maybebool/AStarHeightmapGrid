using System.Collections.Generic;
using System.Diagnostics;
using PathFinderDOTS.Components;
using PathFinderDOTS.Data;
using PathFinderDOTS.Jobs;
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
            
            // CRITICAL FIX: Reset working costs BEFORE pathfinding
            ResetWorkingCosts();
            
            // Clear working data (no allocations!)
            _resultPath.Clear();
            _openList.Clear();
            _closedSet.Clear();
            
            // Create and run the optimized job
            var pathfindingJob = new AStarPathfindingJob()
            {
                Nodes = _gridData.Nodes,
                Costs = _workingCosts,
                TerrainHeights = _gridData.TerrainHeights,
                Width = _gridData.Width,
                Height = _gridData.Height,
                CellSize = _cellSize, // Pass the actual cell size for proper cost scaling
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
                
                // Debug diagonal paths
                if (IsDiagonalPath(startPos, endPos))
                {
                    UnityEngine.Debug.Log($"Diagonal path detected: Start({startPos.x},{startPos.y}) -> End({endPos.x},{endPos.y})");
                    UnityEngine.Debug.Log($"Path nodes: {string.Join(" -> ", legacyPath.ConvertAll(n => $"({n.Index.x},{n.Index.y})"))}");
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning($"No path found from ({startPos.x},{startPos.y}) to ({endPos.x},{endPos.y})");
                
                // Additional debug info for failed paths
                int startIndex = _gridData.GetIndex(startPos.x, startPos.y);
                int endIndex = _gridData.GetIndex(endPos.x, endPos.y);
                UnityEngine.Debug.Log($"Start walkable: {_gridData.Nodes[startIndex].IsWalkable}, End walkable: {_gridData.Nodes[endIndex].IsWalkable}");
            }
            
            return legacyPath;
        }
        
        private bool IsDiagonalPath(Vector2Int start, Vector2Int end)
        {
            int dx = Mathf.Abs(end.x - start.x);
            int dy = Mathf.Abs(end.y - start.y);
            // Path is considered diagonal if both x and y distances are significant
            return dx > 0 && dy > 0 && Mathf.Abs(dx - dy) < Mathf.Max(dx, dy) * 0.5f;
        }
        
        public List<Vector3> ConvertPathToWorldPositions(List<PathFinder.PathNode> path, float heightOffset)
        {
            if (path == null || path.Count == 0)
            {
                UnityEngine.Debug.LogWarning("ConvertPathToWorldPositions: Path is null or empty");
                return null;
            }
            
            var worldPath = new List<Vector3>(path.Count);
            
            foreach (var node in path)
            {
                int index = _gridData.GetIndex(node.Index.x, node.Index.y);
                
                // Validate index
                if (index < 0 || index >= _gridData.TerrainHeights.Length)
                {
                    UnityEngine.Debug.LogError($"Invalid node index: {index} for position ({node.Index.x},{node.Index.y})");
                    continue;
                }
                
                float terrainHeight = _gridData.TerrainHeights[index];
                
                Vector3 worldPos = _gridData.GridToWorldPosition(
                    new int2(node.Index.x, node.Index.y),
                    terrainHeight + heightOffset
                );
                
                worldPath.Add(worldPos);
            }
            
            if (worldPath.Count == 0)
            {
                UnityEngine.Debug.LogError("ConvertPathToWorldPositions: Failed to convert any nodes to world positions");
                return null;
            }
            
            UnityEngine.Debug.Log($"Converted {worldPath.Count} path nodes to world positions");
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
}