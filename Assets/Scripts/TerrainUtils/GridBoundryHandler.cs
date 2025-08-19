using Unity.Mathematics;
using UnityEngine;

namespace TerrainUtils {

    public class GridBoundaryHandler {
        private readonly int _width;
        private readonly int _height;
        private readonly float _cellSize;
        private readonly Vector3 _origin;
        
        public GridBoundaryHandler(int width, int height, float cellSize, Vector3 origin) {
            _width = width;
            _height = height;
            _cellSize = cellSize;
            _origin = origin;
        }
        
        /// <summary>
        /// Clamps a grid position to valid boundaries
        /// </summary>
        public Vector2Int ClampToValidGrid(Vector2Int position) {
            return new Vector2Int(
                Mathf.Clamp(position.x, 0, _width - 1),
                Mathf.Clamp(position.y, 0, _height - 1)
            );
        }
        
        /// <summary>
        /// Clamps an int2 position to valid boundaries (for DOTS compatibility)
        /// </summary>
        public int2 ClampToValidGrid(int2 position) {
            return new int2(
                math.clamp(position.x, 0, _width - 1),
                math.clamp(position.y, 0, _height - 1)
            );
        }
        
        /// <summary>
        /// Checks if a position is within valid grid boundaries
        /// </summary>
        public bool IsValidGridPosition(Vector2Int position) {
            return position.x >= 0 && position.x < _width &&
                   position.y >= 0 && position.y < _height;
        }
        
        /// <summary>
        /// Checks if a position is within valid grid boundaries (DOTS version)
        /// </summary>
        public bool IsValidGridPosition(int2 position) {
            return position.x >= 0 && position.x < _width &&
                   position.y >= 0 && position.y < _height;
        }
        
        /// <summary>
        /// Converts world position to grid position with boundary clamping
        /// </summary>
        public Vector2Int WorldToGridPositionClamped(Vector3 worldPos) {
            Vector3 localPos = worldPos - _origin;
            var gridPos = new Vector2Int(
                Mathf.FloorToInt(localPos.x / _cellSize),
                Mathf.FloorToInt(localPos.z / _cellSize)
            );
            return ClampToValidGrid(gridPos);
        }
        
        /// <summary>
        /// Converts world position to grid position without clamping
        /// </summary>
        public Vector2Int WorldToGridPositionUnclamped(Vector3 worldPos) {
            Vector3 localPos = worldPos - _origin;
            return new Vector2Int(
                Mathf.FloorToInt(localPos.x / _cellSize),
                Mathf.FloorToInt(localPos.z / _cellSize)
            );
        }
        
        /// <summary>
        /// Gets the nearest valid grid position from a world position
        /// </summary>
        public (Vector2Int gridPos, bool wasOutOfBounds) GetNearestValidGridPosition(Vector3 worldPos) {
            var unclamped = WorldToGridPositionUnclamped(worldPos);
            var clamped = ClampToValidGrid(unclamped);
            bool wasOutOfBounds = unclamped != clamped;
            return (clamped, wasOutOfBounds);
        }
        
        /// <summary>
        /// Calculates the distance from a position to the nearest grid boundary
        /// </summary>
        public float DistanceToNearestBoundary(Vector2Int position) {
            int minDistX = Mathf.Min(position.x, _width - 1 - position.x);
            int minDistY = Mathf.Min(position.y, _height - 1 - position.y);
            return Mathf.Min(minDistX, minDistY);
        }
        
        /// <summary>
        /// Gets boundary information for debugging/logging
        /// </summary>
        public string GetBoundaryInfo() {
            return $"Grid Boundaries: (0,0) to ({_width-1},{_height-1})";
        }
    }
}