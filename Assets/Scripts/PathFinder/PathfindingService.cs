using System.Collections.Generic;
using UnityEngine;

namespace PathFinder {

    public class PathfindingService {
        private readonly int _gridSize;
        private readonly float _flyCostMultiplier;
        private readonly TerrainInfo _terrainInfo;
        private readonly PathGrid _pathGrid;
        
        private readonly List<PathNode> _openNodes = new();
        private readonly List<PathNode> _closedNodes = new();
        
        public PathfindingService(int gridSize, float flyCostMultiplier, TerrainInfo terrainInfo, Vector3 gridOrigin) {
            _gridSize = gridSize;
            _flyCostMultiplier = flyCostMultiplier;
            _terrainInfo = terrainInfo;
            _pathGrid = new PathGrid(gridSize, gridSize, gridOrigin);
        }
        
        /// <summary>
        /// Calculates an A* path between two grid positions.
        /// </summary>
        /// <returns>List of PathNodes representing the path, or null if no path found</returns>
        public List<PathNode> CalculatePath(Vector2Int startPos, Vector2Int endPos) {
            PathNode start = _pathGrid.GetNodeAt(startPos);
            PathNode end = _pathGrid.GetNodeAt(endPos);
            
            if (start == null || end == null) {
                Debug.LogWarning($"Invalid path positions: start={startPos}, end={endPos}");
                return null;
            }
            
            return CalculateAStarPath(start, end);
        }
        
        /// <summary>
        /// Converts a path of nodes to world positions with proper height.
        /// </summary>
        public List<Vector3> ConvertPathToWorldPositions(List<PathNode> path, float heightOffset) {
            if (path == null || path.Count == 0) return null;
            
            List<Vector3> worldPath = new List<Vector3>(path.Count);
            float[,] terrainHeights = _terrainInfo.SampleHeights(_gridSize);
            float cellSize = _terrainInfo.CellSize;
            Vector3 origin = _terrainInfo.transform.position;
            
            foreach (var node in path) {
                float terrainHeight = terrainHeights[node.Index.x, node.Index.y];
                Vector3 worldPos = origin + new Vector3(
                    (node.Index.x + 0.5f) * cellSize,
                    terrainHeight + heightOffset,
                    (node.Index.y + 0.5f) * cellSize
                );
                worldPath.Add(worldPos);
            }
            
            return worldPath;
        }
        
        /// <summary>
        /// Gets a node from the grid at the specified position.
        /// </summary>
        public PathNode GetNodeAt(Vector2Int position) {
            return _pathGrid.GetNodeAt(position);
        }
        
        /// <summary>
        /// Converts a world position to grid coordinates.
        /// </summary>
        public Vector2Int WorldToGridPosition(Vector3 worldPos) {
            Vector3 localPos = worldPos - _terrainInfo.transform.position;
            int gridX = Mathf.FloorToInt(localPos.x / _terrainInfo.CellSize);
            int gridY = Mathf.FloorToInt(localPos.z / _terrainInfo.CellSize);
            return new Vector2Int(gridX, gridY);
        }
        
        /// <summary>
        /// Converts grid coordinates to world position.
        /// </summary>
        public Vector3 GridToWorldPosition(Vector2Int gridPos, float yOffset = 0) {
            return _pathGrid.GetWorldPositionFromNodeIndex(gridPos, yOffset, _terrainInfo.CellSize);
        }
        
        /// <summary>
        /// Checks if a grid position is valid within the current grid bounds.
        /// </summary>
        public bool IsValidGridPosition(Vector2Int pos) {
            return pos.x >= 0 && pos.x < _gridSize && 
                   pos.y >= 0 && pos.y < _gridSize;
        }
        
        private List<PathNode> CalculateAStarPath(PathNode start, PathNode end) {
            _openNodes.Clear();
            _closedNodes.Clear();
            
            // Reset the grid for new pathfinding
            ResetPathGrid();
            
            var terrainHeights = _terrainInfo.SampleHeights(_gridSize, false);
            
            // Initialize start node
            start.GCost = 0;
            start.HCost = Vector2.Distance(start.Index, end.Index);
            start.FlyCost = 0;
            start.SourceNode = null;
            _openNodes.Add(start);
            
            while (_openNodes.Count > 0) {
                var currentNode = GetLowestCostNode();
                
                if (currentNode == end) {
                    return ReconstructPath(currentNode);
                }
                
                _openNodes.Remove(currentNode);
                _closedNodes.Add(currentNode);
                
                ProcessNeighbors(currentNode, end, terrainHeights);
            }
            
            return null; // No path found
        }
        
        private void ProcessNeighbors(PathNode currentNode, PathNode end, float[,] terrainHeights) {
            var neighbours = _pathGrid.GetAllNeighbours(currentNode.Index);
            
            foreach (var neighbour in neighbours) {
                if (neighbour.IsWall || _closedNodes.Contains(neighbour)) {
                    continue;
                }
                
                UpdateNeighborCosts(currentNode, neighbour, end, terrainHeights);
                
                if (!_openNodes.Contains(neighbour)) {
                    _openNodes.Add(neighbour);
                }
            }
        }
        
        private void UpdateNeighborCosts(PathNode currentNode, PathNode neighbour, PathNode end, float[,] terrainHeights) {
            bool isDiagonal = currentNode.Index.x != neighbour.Index.x && 
                             currentNode.Index.y != neighbour.Index.y;
            
            float movementCost = isDiagonal ? 1.4f : 1f;
            float newGCost = currentNode.GCost + movementCost;
            
            float heightDifference = terrainHeights[neighbour.Index.x, neighbour.Index.y] - 
                                    terrainHeights[currentNode.Index.x, currentNode.Index.y];
            float flyCost = Mathf.Max(0, heightDifference * _flyCostMultiplier);
            
            float totalCost = newGCost + flyCost;
            
            if (totalCost < neighbour.GCost) {
                neighbour.GCost = newGCost;
                neighbour.FlyCost = flyCost;
                neighbour.HCost = Vector2.Distance(neighbour.Index, end.Index);
                neighbour.SourceNode = currentNode;
            }
        }
        
        private PathNode GetLowestCostNode() {
            PathNode lowestCostNode = _openNodes[0];
            float lowestCost = lowestCostNode.FCost;
            
            foreach (var node in _openNodes) {
                if (node.FCost < lowestCost) {
                    lowestCost = node.FCost;
                    lowestCostNode = node;
                }
            }
            
            return lowestCostNode;
        }
        
        private List<PathNode> ReconstructPath(PathNode endNode) {
            List<PathNode> path = new List<PathNode>();
            PathNode currentNode = endNode;
            
            const int maxIterations = 1000;
            int iterations = 0;
            
            while (currentNode != null && iterations < maxIterations) {
                path.Add(currentNode);
                currentNode = currentNode.SourceNode;
                iterations++;
            }
            
            if (iterations >= maxIterations) {
                Debug.LogError("Path reconstruction exceeded maximum iterations");
                return null;
            }
            
            path.Reverse();
            return path;
        }
        
        private void ResetPathGrid() {
            for (int x = 0; x < _gridSize; x++) {
                for (int y = 0; y < _gridSize; y++) {
                    var node = _pathGrid.GetNodeAt(new Vector2Int(x, y));
                    if (node != null) {
                        node.GCost = float.MaxValue;
                        node.HCost = 0;
                        node.FlyCost = 0;
                        node.SourceNode = null;
                        node.IsWall = false;
                    }
                }
            }
        }
    }
}