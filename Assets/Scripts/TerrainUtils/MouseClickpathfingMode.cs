using UnityEngine;
using UnityEngine.InputSystem;

namespace TerrainUtils {
    /// <summary>
    /// Traditional mouse-click based pathfinding mode for turn-based games
    /// </summary>
    public class MouseClickPathfindingMode : PathfindingModeBase {
        private Vector2Int _startGridPos;
        private Vector2Int _endGridPos;
        private bool _hasStartPoint = false;
        private GameObject _startMarker;
        private GameObject _endMarker;
        
        // Configuration
        private float _markerHeight = 2f;
        private float _markerSize = 1f;
        
        public override string ModeName => "Mouse Click";
        
        public override void OnActivate() {
            base.OnActivate();
            _hasStartPoint = false;
            UpdateInstructionText("Click on the terrain to set START point");
            CreateMarkers();
        }
        
        public override void OnDeactivate() {
            base.OnDeactivate();
            DestroyMarkers();
        }
        
        public override void UpdateMode() {
            if (!_isActive) return;
            
            HandleMouseInput();
        }
        
        public override void Cleanup() {
            base.Cleanup();
            DestroyMarkers();
        }
        
        public override void DrawDebugVisualization() {
            if (!_isActive) return;
            
            // Draw markers
            if (_hasStartPoint) {
                var startWorld = GridToWorldPosition(_startGridPos, _markerHeight);
                Gizmos.color = StartColor;
                Gizmos.DrawWireSphere(startWorld, _markerSize);
                Gizmos.DrawLine(startWorld, startWorld + Vector3.up * 5f);
            }
            
            if (_hasStartPoint && _endGridPos != _startGridPos) {
                var endWorld = GridToWorldPosition(_endGridPos, _markerHeight);
                Gizmos.color = EndColor;
                Gizmos.DrawWireSphere(endWorld, _markerSize);
                Gizmos.DrawLine(endWorld, endWorld + Vector3.up * 5f);
            }
        }
        
        public override string GetStatusInfo() {
            if (!_hasStartPoint) {
                return "Waiting for start point...";
            } else {
                return $"Start: ({_startGridPos.x}, {_startGridPos.y}) | Click for end point";
            }
        }
        
        private void HandleMouseInput() {
            // Check for mouse click
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;
            
            var mousePos = Mouse.current.position.ReadValue();
            if (GetTerrainHitFromScreenPoint(mousePos, out RaycastHit hit)) {
                Vector2Int gridPos = WorldToGridPosition(hit.point);
                
                if (IsValidGridPosition(gridPos)) {
                    if (!_hasStartPoint) {
                        SetStartPoint(gridPos);
                    } else {
                        SetEndPointAndRequestPath(gridPos);
                    }
                }
            }
        }
        
        private void SetStartPoint(Vector2Int gridPos) {
            ClearVisualization();
            
            _startGridPos = gridPos;
            _hasStartPoint = true;
            
            // Visualize start point
            SetCellColor(gridPos, StartColor);
            UpdateStartMarker(gridPos);
            
            UpdateInstructionText("Click on the terrain to set END point");
        }
        
        private void SetEndPointAndRequestPath(Vector2Int gridPos) {
            // If path is already active, use average bird position as new start
            if (IsPathActive()) {
                var avgPos = GetAverageBirdPosition();
                var newStartPos = WorldToGridPosition(avgPos);
                
                if (IsValidGridPosition(newStartPos)) {
                    _startGridPos = newStartPos;
                }
            }
            
            _endGridPos = gridPos;
            ClearVisualization();
            
            // Visualize start and end points
            SetCellColor(_startGridPos, StartColor);
            SetCellColor(_endGridPos, EndColor);
            
            UpdateStartMarker(_startGridPos);
            UpdateEndMarker(_endGridPos);
            
            // Request path calculation
            RequestPath(_startGridPos, _endGridPos);
            
            UpdateInstructionText("Path calculated! Click anywhere to set a new destination.");
        }
        
        private void CreateMarkers() {
            if (_startMarker == null) {
                _startMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                _startMarker.name = "PathStartMarker";
                _startMarker.transform.localScale = Vector3.one * _markerSize;
                var startRenderer = _startMarker.GetComponent<MeshRenderer>();
                if (startRenderer != null) {
                    startRenderer.material.color = StartColor;
                }
                _startMarker.SetActive(false);
            }
            
            if (_endMarker == null) {
                _endMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                _endMarker.name = "PathEndMarker";
                _endMarker.transform.localScale = Vector3.one * _markerSize;
                var endRenderer = _endMarker.GetComponent<MeshRenderer>();
                if (endRenderer != null) {
                    endRenderer.material.color = EndColor;
                }
                _endMarker.SetActive(false);
            }
        }
        
        private void UpdateStartMarker(Vector2Int gridPos) {
            if (_startMarker != null) {
                var worldPos = GridToWorldPosition(gridPos, _markerHeight);
                _startMarker.transform.position = worldPos;
                _startMarker.SetActive(true);
            }
        }
        
        private void UpdateEndMarker(Vector2Int gridPos) {
            if (_endMarker != null) {
                var worldPos = GridToWorldPosition(gridPos, _markerHeight);
                _endMarker.transform.position = worldPos;
                _endMarker.SetActive(true);
            }
        }
        
        private void DestroyMarkers() {
            if (_startMarker != null) {
                if (Application.isPlaying) {
                    Object.Destroy(_startMarker);
                } else {
                    Object.DestroyImmediate(_startMarker);
                }
                _startMarker = null;
            }
            
            if (_endMarker != null) {
                if (Application.isPlaying) {
                    Object.Destroy(_endMarker);
                } else {
                    Object.DestroyImmediate(_endMarker);
                }
                _endMarker = null;
            }
        }
        
        // Reset method for when switching back to this mode
        public void Reset() {
            _hasStartPoint = false;
            _startGridPos = Vector2Int.zero;
            _endGridPos = Vector2Int.zero;
            ClearVisualization();
            DestroyMarkers();
            CreateMarkers();
            UpdateInstructionText("Click on the terrain to set START point");
        }
    }
}