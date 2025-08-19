using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainUtils {
    
    public abstract class PathfindingModeBase : IPathfindingMode {
        protected PathfindingModeContext _context;
        protected bool _isActive;
        protected List<Vector2Int> _coloredCells = new();
        
        public event Action<PathRequest> OnPathRequested;
        public event Action OnClearPath;
        
        public abstract string ModeName { get; }
        public bool IsActive => _isActive;
        
        protected readonly Color StartColor = Color.green;
        protected readonly Color EndColor = Color.red;
        protected readonly Color PathColor = Color.blue;
        protected readonly Color WarningColor = new Color(1f, 0.5f, 0f); // Orange for boundary warnings
        
        public virtual void Initialize(PathfindingModeContext context) {
            _context = context;
            ValidateContext();
        }
        
        public virtual void OnActivate() {
            _isActive = true;
            ClearVisualization();
        }
        
        public virtual void OnDeactivate() {
            _isActive = false;
            ClearVisualization();
        }
        
        public abstract void UpdateMode();
        
        public virtual void Cleanup() {
            ClearVisualization();
            _coloredCells.Clear();
        }
        
        public abstract void DrawDebugVisualization();
        
        public abstract string GetStatusInfo();
        
        // Protected methods for event invocation (allows derived classes to trigger events)
        
        /// <summary>
        /// Invokes the OnPathRequested event - accessible to derived classes
        /// </summary>
        protected void InvokePathRequest(PathRequest request) {
            OnPathRequested?.Invoke(request);
        }
        
        /// <summary>
        /// Invokes the OnClearPath event - accessible to derived classes
        /// </summary>
        protected void InvokeClearPath() {
            OnClearPath?.Invoke();
        }
        
        // Enhanced helper methods with boundary safety
        
        /// <summary>
        /// Request path with automatic boundary clamping
        /// </summary>
        protected void RequestPath(Vector2Int start, Vector2Int end) {
            // Clamp positions to valid grid bounds
            var clampedStart = ClampToValidGridPosition(start);
            var clampedEnd = ClampToValidGridPosition(end);
            
            // Log if positions were clamped
            if (clampedStart != start || clampedEnd != end) {
                LogBoundaryClamp(start, clampedStart, "start");
                LogBoundaryClamp(end, clampedEnd, "end");
            }
            
            var request = new PathRequest(clampedStart, clampedEnd, _context.BirdHeightOffset);
            InvokePathRequest(request);
        }
        
        /// <summary>
        /// Request path with boundary checking and optional warning visualization
        /// </summary>
        protected void RequestPathSafe(Vector2Int start, Vector2Int end, bool visualizeWarning = true) {
            bool startWasClamped = false;
            bool endWasClamped = false;
            
            var clampedStart = start;
            var clampedEnd = end;
            
            if (!IsValidGridPosition(start)) {
                clampedStart = ClampToValidGridPosition(start);
                startWasClamped = true;
                
                if (visualizeWarning) {
                    SetCellColor(clampedStart, WarningColor);
                }
            }
            
            if (!IsValidGridPosition(end)) {
                clampedEnd = ClampToValidGridPosition(end);
                endWasClamped = true;
                
                if (visualizeWarning) {
                    SetCellColor(clampedEnd, WarningColor);
                }
            }
            
            if (startWasClamped || endWasClamped) {
                var warningMsg = $"Path positions were clamped to grid bounds: ";
                if (startWasClamped) warningMsg += $"Start ({start}→{clampedStart}) ";
                if (endWasClamped) warningMsg += $"End ({end}→{clampedEnd})";
                
                Debug.LogWarning($"[{ModeName}] {warningMsg}");
                UpdateInstructionText("Warning: Position was outside grid bounds and was clamped!");
            }
            
            var request = new PathRequest(clampedStart, clampedEnd, _context.BirdHeightOffset);
            InvokePathRequest(request);
        }
        
        protected void ClearCurrentPath() {
            InvokeClearPath();
        }
        
        /// <summary>
        /// Convert world position to grid position with automatic clamping
        /// </summary>
        protected Vector2Int WorldToGridPosition(Vector3 worldPos) {
            return _context.PathfindingService.WorldToGridPosition(worldPos);
        }
        
        /// <summary>
        /// Convert world position to grid position with out-of-bounds check
        /// </summary>
        protected Vector2Int WorldToGridPositionSafe(Vector3 worldPos, out bool wasOutOfBounds) {
            return _context.PathfindingService.WorldToGridPositionSafe(worldPos, out wasOutOfBounds);
        }
        
        protected Vector3 GridToWorldPosition(Vector2Int gridPos, float yOffset = 0) {
            return _context.PathfindingService.GridToWorldPosition(gridPos, yOffset);
        }
        
        protected bool IsValidGridPosition(Vector2Int pos) {
            return _context.PathfindingService.IsValidGridPosition(pos);
        }
        
        /// <summary>
        /// Clamp a grid position to valid bounds
        /// </summary>
        protected Vector2Int ClampToValidGridPosition(Vector2Int pos) {
            return _context.PathfindingService.ClampToValidGridPosition(pos);
        }
        
        protected void SetCellColor(Vector2Int gridPos, Color color) {
            // Ensure position is valid before setting color
            var clampedPos = ClampToValidGridPosition(gridPos);
            
            if (_context.TerrainInfo) {
                _context.TerrainInfo.SetColor(clampedPos, color);
                if (!_coloredCells.Contains(clampedPos)) {
                    _coloredCells.Add(clampedPos);
                }
            }
        }
        
        protected void ClearVisualization() {
            if (_context?.TerrainInfo && Application.isPlaying) {
                foreach (var cell in _coloredCells) {
                    try {
                        _context.TerrainInfo.ResetCellColor(cell);
                    } catch (MissingReferenceException) {
                        // Handle destroyed objects gracefully
                    }
                }
            }
            _coloredCells.Clear();
        }
        
        protected void UpdateInstructionText(string text) {
            _context?.UpdateInstructionText?.Invoke(text);
        }
        
        protected Vector3 GetAverageBirdPosition() {
            return _context?.GetAverageBirdPosition?.Invoke() ?? Vector3.zero;
        }
        
        protected bool IsPathActive() {
            return _context?.IsPathActive?.Invoke() ?? false;
        }
        
        private void ValidateContext() {
            if (_context == null) {
                throw new InvalidOperationException($"[{ModeName}] Context is null");
            }
            
            if (!_context.IsValid()) {
                var missingComponents = new List<string>();
                if (!_context.MainCamera) missingComponents.Add("MainCamera");
                if (!_context.TerrainInfo) missingComponents.Add("TerrainInfo");
                if (!_context.PathfindingService) missingComponents.Add("PathfindingService");
                
                var errorDetails = missingComponents.Count > 0 
                    ? $"Missing: {string.Join(", ", missingComponents)}" 
                    : "Unknown validation error";
                    
                throw new InvalidOperationException($"[{ModeName}] Context is invalid - {errorDetails}");
            }
        }
        
        /// <summary>
        /// Helper for ray casting from camera with boundary check
        /// </summary>
        protected bool GetTerrainHitFromScreenPoint(Vector2 screenPoint, out RaycastHit hit) {
            if (!_context.MainCamera) {
                hit = default;
                return false;
            }
            
            var ray = _context.MainCamera.ScreenPointToRay(screenPoint);
            
            if (Physics.Raycast(ray, out hit)) {
                // Check if hit point is within terrain bounds
                var gridPos = WorldToGridPositionSafe(hit.point, out bool wasOutOfBounds);
                
                if (wasOutOfBounds && _context.ShowClampWarnings) {
                    Debug.LogWarning($"[{ModeName}] Click was outside grid bounds. Position will be clamped.");
                }
                
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Log boundary clamping for debugging
        /// </summary>
        private void LogBoundaryClamp(Vector2Int original, Vector2Int clamped, string positionName) {
            if (original != clamped && _context.ShowClampWarnings) {
                Debug.LogWarning($"[{ModeName}] {positionName} position clamped from {original} to {clamped}");
            }
        }
        
        /// <summary>
        /// Get distance to nearest grid boundary (useful for warning thresholds)
        /// </summary>
        protected float GetDistanceToNearestBoundary(Vector2Int position) {
            var boundaryHandler = _context.PathfindingService.GetBoundaryHandler();
            return boundaryHandler?.DistanceToNearestBoundary(position) ?? float.MaxValue;
        }
    }
}