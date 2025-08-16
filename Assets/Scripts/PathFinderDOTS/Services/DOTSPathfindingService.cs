using System.Collections.Generic;
using System.Diagnostics;
using PathFinderDOTS.Components;
using PathFinderDOTS.Data;
using PathFinderDOTS.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PathFinderDOTS.Services
{
    /// <summary>
    /// Optimized DOTS Pathfinding Service using all available fields
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
        
        // Persistent arrays for pathfinding
        private NativeArray<PathNodeComponent> _workingNodes; // For mutable node state
        private NativeArray<PathNodeCost> _workingCosts;
        private NativeList<int> _resultPath;
        private NativeList<int> _openList;
        
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
            
            // Pass flyCostMultiplier to the grid data
            _gridData = new PathGridData(
                _gridSize,
                _gridSize,
                _cellSize,
                origin,
                _flyCostMultiplier, // Now properly passed to grid
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
                    }
                }
            }
            
            _isInitialized = true;
            UnityEngine.Debug.Log($"DOTSPathfindingService initialized with grid size {_gridSize}x{_gridSize}, " +
                                 $"flyCostMultiplier: {_flyCostMultiplier}");
        }
        
        private void InitializePersistentArrays()
        {
            int totalNodes = _gridSize * _gridSize;
            
            // Allocate working arrays
            _workingNodes = new NativeArray<PathNodeComponent>(totalNodes, Allocator.Persistent);
            _workingCosts = new NativeArray<PathNodeCost>(totalNodes, Allocator.Persistent);
            _resultPath = new NativeList<int>(256, Allocator.Persistent);
            _openList = new NativeList<int>(totalNodes / 4, Allocator.Persistent);
            
            // Initialize working arrays
            ResetWorkingState();
        }
        
        private void ResetWorkingState()
        {
            // Copy node walkability state and reset pathfinding flags
            for (int i = 0; i < _workingNodes.Length; i++)
            {
                _workingNodes[i] = new PathNodeComponent
                {
                    IsWalkable = _gridData.Nodes[i].IsWalkable,
                    IsProcessed = 0,
                    IsInOpenList = 0
                };
                
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
        /// Calculates path and returns world positions directly
        /// </summary>
        public List<Vector3> CalculatePath(Vector2Int startPos, Vector2Int endPos, float heightOffset)
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
            
            // Reset working state
            ResetWorkingState();
            
            // Clear working data
            _resultPath.Clear();
            _openList.Clear();
            
            // Create and run the updated job (using original name)
            var pathfindingJob = new AStarPathfindingJob()
            {
                Nodes = _workingNodes,
                Costs = _workingCosts,
                TerrainHeights = _gridData.TerrainHeights,
                Width = _gridData.Width,
                Height = _gridData.Height,
                CellSize = _cellSize,
                FlyCostMultiplier = _gridData.FlyCostMultiplier, // Use from grid configuration
                StartPos = new int2(startPos.x, startPos.y),
                EndPos = new int2(endPos.x, endPos.y),
                ResultPath = _resultPath,
                OpenList = _openList
            };
            
            // Run with Burst compilation
            JobHandle handle = pathfindingJob.Schedule();
            handle.Complete();
            
            _stopwatch.Stop();
            
            // Convert directly to world positions
            List<Vector3> worldPath = null;
            
            if (_resultPath.Length > 0)
            {
                worldPath = new List<Vector3>(_resultPath.Length);
                
                for (int i = 0; i < _resultPath.Length; i++)
                {
                    int index = _resultPath[i];
                    int2 gridPos = _gridData.GetGridPosition(index);
                    float terrainHeight = _gridData.TerrainHeights[index];
                    
                    Vector3 worldPos = _gridData.GridToWorldPosition(
                        gridPos,
                        terrainHeight + heightOffset
                    );
                    
                    worldPath.Add(worldPos);
                }
                
                UnityEngine.Debug.Log($"[DOTS] Path found: {worldPath.Count} positions in {_stopwatch.ElapsedMilliseconds}ms");
                
                // Log optimization stats
                int processedCount = 0;
                for (int i = 0; i < _workingNodes.Length; i++)
                {
                    if (_workingNodes[i].IsProcessed == 1)
                        processedCount++;
                }
                UnityEngine.Debug.Log($"[DOTS] Nodes processed: {processedCount}/{_workingNodes.Length} " +
                                     $"({(processedCount * 100f / _workingNodes.Length):F1}%)");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"No path found from ({startPos.x},{startPos.y}) to ({endPos.x},{endPos.y})");
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
            if (_workingNodes.IsCreated) _workingNodes.Dispose();
            if (_workingCosts.IsCreated) _workingCosts.Dispose();
            if (_resultPath.IsCreated) _resultPath.Dispose();
            if (_openList.IsCreated) _openList.Dispose();
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