using System;
using PathFinderDOTS.Services;
using UnityEngine;

namespace TerrainUtils {
    /// <summary>
    /// Interface for different pathfinding modes (mouse-click, target-follow, etc.)
    /// </summary>
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
        
        // Callbacks for mode to communicate with manager
        public Action<string> UpdateInstructionText { get; set; }
        public Func<Vector3> GetAverageBirdPosition { get; set; }
        public Func<bool> IsPathActive { get; set; }
        
        // Validation
        public bool IsValid() {
            return MainCamera != null && 
                   TerrainInfo != null && 
                   PathfindingService != null;
        }
    }
}