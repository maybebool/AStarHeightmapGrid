using System;
using PathFinderDOTS.Services;
using UnityEngine;

namespace TerrainUtils {

    public interface IPathfindingMode {
        /// <summary>
        /// Name of the mode for UI display
        /// </summary>
        string ModeName { get; }
        
        /// <summary>
        /// Whether this mode is currently active and processing
        /// </summary>
        bool IsActive { get; }
        
        /// <summary>
        /// Initialize the mode with required dependencies
        /// </summary>
        void Initialize(PathfindingModeContext context);
        
        /// <summary>
        /// Called when this mode becomes active
        /// </summary>
        void OnActivate();
        
        /// <summary>
        /// Called when this mode becomes inactive
        /// </summary>
        void OnDeactivate();
        
        /// <summary>
        /// Update loop for the mode
        /// </summary>
        void UpdateMode();
        
        /// <summary>
        /// Handle any necessary cleanup
        /// </summary>
        void Cleanup();
        
        /// <summary>
        /// Event fired when a new path is requested
        /// </summary>
        event Action<PathRequest> OnPathRequested;
        
        /// <summary>
        /// Event fired when the mode wants to clear the current path
        /// </summary>
        event Action OnClearPath;

        /// <summary>
        /// Draw any mode-specific debug visualization
        /// </summary>
        void DrawDebugVisualization();
        
        /// <summary>
        /// Get mode-specific status information for UI
        /// </summary>
        string GetStatusInfo();
    }

    /// <summary>
    /// Context object containing dependencies for pathfinding modes
    /// </summary>
    public class PathfindingModeContext {
        public Camera MainCamera { get; set; }
        public TerrainInfo TerrainInfo { get; set; }
        public DOTSPathfindingService PathfindingService { get; set; }
        public Transform SwarmCenterTransform { get; set; }
        public float BirdHeightOffset { get; set; }
        
        // Grid information for boundary checking
        public int GridSize { get; set; } = 64; // Default based on TerrainInfo
        public Vector2Int GridDimensions { get; set; } // For non-square grids
        
        // Callbacks for mode to communicate with manager
        public Action<string> UpdateInstructionText { get; set; }
        public Func<Vector3> GetAverageBirdPosition { get; set; }
        public Func<bool> IsPathActive { get; set; }
        
        // Additional configuration
        public bool AutoClampOutOfBounds { get; set; } = true;
        public bool ShowClampWarnings { get; set; } = true;
        
        // Validation
        public bool IsValid() {
            return MainCamera != null && 
                   TerrainInfo != null && 
                   PathfindingService != null;
        }
        
        /// <summary>
        /// Initialize grid dimensions from terrain info
        /// </summary>
        public void InitializeFromTerrain() {
            if (TerrainInfo != null) {
                // Try to get the actual grid size from terrain
                // This assumes samplesPerSide is accessible
                GridSize = 64; // You'll need to make this accessible from TerrainInfo
                GridDimensions = new Vector2Int(GridSize, GridSize);
            }
        }
        
        /// <summary>
        /// Helper to clamp a grid position to valid bounds
        /// </summary>
        public Vector2Int ClampToValidGrid(Vector2Int position) {
            if (GridDimensions == Vector2Int.zero) {
                // Use square grid assumption
                return new Vector2Int(
                    Mathf.Clamp(position.x, 0, GridSize - 1),
                    Mathf.Clamp(position.y, 0, GridSize - 1)
                );
            } else {
                // Use actual dimensions
                return new Vector2Int(
                    Mathf.Clamp(position.x, 0, GridDimensions.x - 1),
                    Mathf.Clamp(position.y, 0, GridDimensions.y - 1)
                );
            }
        }
        
        /// <summary>
        /// Check if a grid position is within valid bounds
        /// </summary>
        public bool IsValidGridPosition(Vector2Int position) {
            if (GridDimensions == Vector2Int.zero) {
                // Use square grid assumption
                return position.x >= 0 && position.x < GridSize &&
                       position.y >= 0 && position.y < GridSize;
            } else {
                // Use actual dimensions
                return position.x >= 0 && position.x < GridDimensions.x &&
                       position.y >= 0 && position.y < GridDimensions.y;
            }
        }
    }
}