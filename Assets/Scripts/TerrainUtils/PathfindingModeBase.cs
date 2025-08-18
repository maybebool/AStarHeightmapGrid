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
        
        // Helper methods
        protected void RequestPath(Vector2Int start, Vector2Int end) {
            if (!IsValidGridPosition(start) || !IsValidGridPosition(end)) {
                Debug.LogWarning($"[{ModeName}] Invalid grid positions: start={start}, end={end}");
                return;
            }
            
            var request = new PathRequest(start, end, _context.BirdHeightOffset);
            OnPathRequested?.Invoke(request);
        }
        
        protected void ClearCurrentPath() {
            OnClearPath?.Invoke();
        }
        
        protected Vector2Int WorldToGridPosition(Vector3 worldPos) {
            return _context.PathfindingService.WorldToGridPosition(worldPos);
        }
        
        protected Vector3 GridToWorldPosition(Vector2Int gridPos, float yOffset = 0) {
            return _context.PathfindingService.GridToWorldPosition(gridPos, yOffset);
        }
        
        protected bool IsValidGridPosition(Vector2Int pos) {
            return _context.PathfindingService.IsValidGridPosition(pos);
        }
        
        protected void SetCellColor(Vector2Int gridPos, Color color) {
            if (_context.TerrainInfo) {
                _context.TerrainInfo.SetColor(gridPos, color);
                if (!_coloredCells.Contains(gridPos)) {
                    _coloredCells.Add(gridPos);
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
                // Provide detailed error information
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
        
        // Optional: Helper for ray casting from camera
        protected bool GetTerrainHitFromScreenPoint(Vector2 screenPoint, out RaycastHit hit) {
            if (!_context.MainCamera) {
                hit = default;
                return false;
            }
            
            var ray = _context.MainCamera.ScreenPointToRay(screenPoint);
            return Physics.Raycast(ray, out hit);
        }
    }
}