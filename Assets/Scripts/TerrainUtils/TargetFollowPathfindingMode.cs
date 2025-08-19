using UnityEngine;

namespace TerrainUtils {

    public class TargetFollowPathfindingMode : PathfindingModeBase {
        // Target configuration
        private Transform _targetTransform;
        private Vector2Int _lastTargetGridPos;
        private Vector2Int _lastStartGridPos;
        private bool _targetWasOutOfBounds = false;
        
        // Recalculation settings
        [System.Serializable]
        public class RecalculationSettings {
            public float MinRecalculationInterval = 0.5f;
            public float TargetMoveThreshold = 2f;
            public float SwarmMoveThreshold = 3f;
            public bool UseDistanceBasedThrottling = true;
            public float MaxThrottleDistance = 50f;
            public float MaxThrottleMultiplier = 3f;
            public bool ShowBoundaryWarnings = true;
        }
        
        private RecalculationSettings _settings;
        private float _lastRecalculationTime;
        
        // Visual indicators
        private GameObject _targetIndicator;
        private GameObject _boundaryWarningIndicator;
        
        public override string ModeName => "Target Follow";
        
        public TargetFollowPathfindingMode() : this(new RecalculationSettings()) { }
        
        public TargetFollowPathfindingMode(RecalculationSettings settings) {
            _settings = settings ?? new RecalculationSettings();
        }
        
        public void SetTarget(Transform target) {
            if (target == _targetTransform) return;
            
            _targetTransform = target;
            
            if (_targetTransform != null) {
                // Use safe conversion to handle out-of-bounds targets
                _lastTargetGridPos = WorldToGridPositionSafe(_targetTransform.position, out _targetWasOutOfBounds);
                
                if (_targetWasOutOfBounds && _settings.ShowBoundaryWarnings) {
                    Debug.LogWarning($"[Target Follow] Target '{_targetTransform.name}' is outside grid bounds. Position will be clamped.");
                }
                
                if (_isActive) {
                    ForceRecalculation();
                    UpdateTargetIndicator();
                }
                
                UpdateInstructionText($"Following target: {_targetTransform.name}" + 
                    (_targetWasOutOfBounds ? " (clamped to grid bounds)" : ""));
            } else {
                UpdateInstructionText("No target set - assign a target GameObject");
                DestroyIndicators();
            }
        }
        
        public Transform GetTarget() => _targetTransform;
        
        public override void OnActivate() {
            base.OnActivate();
            _lastRecalculationTime = Time.time;
            
            CreateIndicators();
            
            if (_targetTransform != null) {
                ForceRecalculation();
                UpdateInstructionText($"Following target: {_targetTransform.name}");
            } else {
                UpdateInstructionText("Target Follow Mode: Please assign a target GameObject");
            }
        }
        
        public override void OnDeactivate() {
            base.OnDeactivate();
            DestroyIndicators();
        }
        
        public override void UpdateMode() {
            if (!_isActive || _targetTransform == null) return;
            
            // Check if recalculation is needed
            if (ShouldRecalculatePath()) {
                RecalculatePath();
            }
            
            // Update visual indicators
            UpdateTargetIndicator();
            UpdateBoundaryWarning();
        }
        
        public override void Cleanup() {
            base.Cleanup();
            DestroyIndicators();
        }
        
        public override void DrawDebugVisualization() {
            if (!_isActive || _targetTransform == null) return;
            
            // Draw actual target position
            Gizmos.color = _targetWasOutOfBounds ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(_targetTransform.position, 2f);
            Gizmos.DrawLine(_targetTransform.position, _targetTransform.position + Vector3.up * 10f);
            
            // Draw clamped position if different
            if (_targetWasOutOfBounds) {
                var clampedWorldPos = GridToWorldPosition(_lastTargetGridPos, 0);
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(clampedWorldPos, 2.5f);
                Gizmos.DrawLine(_targetTransform.position, clampedWorldPos);
            }
            
            // Draw recalculation threshold radius
            var targetGridWorld = GridToWorldPosition(_lastTargetGridPos);
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(targetGridWorld, _settings.TargetMoveThreshold * _context.PathfindingService.WorldToGridPosition(Vector3.one * 10f).x / 10f);
        }
        
        public override string GetStatusInfo() {
            if (_targetTransform == null) {
                return "No target assigned";
            }
            
            var timeSinceRecalc = Time.time - _lastRecalculationTime;
            var throttleMultiplier = GetThrottleMultiplier();
            var boundaryStatus = _targetWasOutOfBounds ? " [OUT OF BOUNDS]" : "";
            
            return $"Target: {_targetTransform.name}{boundaryStatus} | " +
                   $"Last Recalc: {timeSinceRecalc:F1}s ago | " +
                   $"Throttle: {throttleMultiplier:F1}x";
        }
        
        private bool ShouldRecalculatePath() {
            // Check time threshold with throttling
            var throttleMultiplier = GetThrottleMultiplier();
            var adjustedInterval = _settings.MinRecalculationInterval * throttleMultiplier;
            
            if (Time.time - _lastRecalculationTime < adjustedInterval) {
                return false;
            }
            
            // Check target movement threshold with safe boundary handling
            var currentTargetGridPos = WorldToGridPositionSafe(_targetTransform.position, out bool currentlyOutOfBounds);
            var targetGridDistance = Vector2Int.Distance(currentTargetGridPos, _lastTargetGridPos);
            
            // Force recalculation if target boundary status changed
            if (currentlyOutOfBounds != _targetWasOutOfBounds) {
                _targetWasOutOfBounds = currentlyOutOfBounds;
                return true;
            }
            
            if (targetGridDistance < _settings.TargetMoveThreshold) {
                return false;
            }
            
            // Check swarm movement threshold (if applicable)
            if (IsPathActive()) {
                var currentSwarmPos = WorldToGridPositionSafe(GetAverageBirdPosition(), out _);
                var swarmGridDistance = Vector2Int.Distance(currentSwarmPos, _lastStartGridPos);
                
                if (swarmGridDistance < _settings.SwarmMoveThreshold) {
                    return false;
                }
            }
            
            return true;
        }
        
        private float GetThrottleMultiplier() {
            if (!_settings.UseDistanceBasedThrottling || !IsPathActive()) {
                return 1f;
            }
            
            var swarmPos = GetAverageBirdPosition();
            var targetPos = _targetTransform.position;
            var distance = Vector3.Distance(swarmPos, targetPos);
            
            // Linear interpolation based on distance
            var t = Mathf.Clamp01(distance / _settings.MaxThrottleDistance);
            return Mathf.Lerp(1f, _settings.MaxThrottleMultiplier, t);
        }
        
        private void RecalculatePath() {
            Vector2Int startPos;
            Vector2Int endPos;
            bool startOutOfBounds = false;
            bool endOutOfBounds = false;
            
            // Determine start position with boundary safety
            if (IsPathActive()) {
                startPos = WorldToGridPositionSafe(GetAverageBirdPosition(), out startOutOfBounds);
            } else {
                startPos = WorldToGridPositionSafe(_context.TerrainInfo.transform.position, out startOutOfBounds);
            }
            
            // Get current target position with boundary safety
            endPos = WorldToGridPositionSafe(_targetTransform.position, out endOutOfBounds);
            _targetWasOutOfBounds = endOutOfBounds;
            
            // Log boundary violations if needed
            if ((startOutOfBounds || endOutOfBounds) && _settings.ShowBoundaryWarnings) {
                var warning = "[Target Follow] Boundary violation detected: ";
                if (startOutOfBounds) warning += "Swarm is outside grid. ";
                if (endOutOfBounds) warning += $"Target '{_targetTransform.name}' is outside grid. ";
                warning += "Positions clamped to valid bounds.";
                Debug.LogWarning(warning);
            }
            
            // Check if start and end are the same (birds have reached the target)
            if (startPos == endPos) {
                Debug.Log($"[Target Follow] Birds have reached the target position");
                _lastRecalculationTime = Time.time;
                _lastTargetGridPos = endPos;
                return;
            }
            
            // Store last positions
            _lastStartGridPos = startPos;
            _lastTargetGridPos = endPos;
            _lastRecalculationTime = Time.time;
            
            // Visualize with warning colors if out of bounds
            ClearVisualization();
            SetCellColor(startPos, startOutOfBounds ? WarningColor : StartColor);
            SetCellColor(endPos, endOutOfBounds ? WarningColor : EndColor);
            
            // Use the base class RequestPath method which will invoke the event properly
            RequestPath(startPos, endPos);
        }
        
        public void ForceRecalculation() {
            _lastRecalculationTime = 0; // Force time check to pass
            RecalculatePath();
        }
        
        private void CreateIndicators() {
            // Create target indicator
            if (_targetIndicator == null) {
                _targetIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _targetIndicator.name = "TargetIndicator";
                _targetIndicator.transform.localScale = Vector3.one * 3f;
                var renderer = _targetIndicator.GetComponent<MeshRenderer>();
                if (renderer != null) {
                    renderer.material.color = Color.yellow;
                }
                var collider = _targetIndicator.GetComponent<Collider>();
                if (collider != null) Object.Destroy(collider);
                _targetIndicator.SetActive(false);
            }
            
            // Create boundary warning indicator
            if (_boundaryWarningIndicator == null) {
                _boundaryWarningIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _boundaryWarningIndicator.name = "BoundaryWarningIndicator";
                _boundaryWarningIndicator.transform.localScale = new Vector3(1f, 5f, 1f);
                var renderer = _boundaryWarningIndicator.GetComponent<MeshRenderer>();
                if (renderer != null) {
                    renderer.material.color = WarningColor;
                }
                var collider = _boundaryWarningIndicator.GetComponent<Collider>();
                if (collider != null) Object.Destroy(collider);
                _boundaryWarningIndicator.SetActive(false);
            }
        }
        
        private void UpdateTargetIndicator() {
            if (_targetIndicator != null && _targetTransform != null) {
                // Show indicator at clamped position if out of bounds
                if (_targetWasOutOfBounds) {
                    var clampedPos = GridToWorldPosition(_lastTargetGridPos, 2f);
                    _targetIndicator.transform.position = clampedPos;
                    var renderer = _targetIndicator.GetComponent<MeshRenderer>();
                    if (renderer != null) {
                        renderer.material.color = WarningColor;
                    }
                } else {
                    _targetIndicator.transform.position = _targetTransform.position + Vector3.up * 2f;
                    var renderer = _targetIndicator.GetComponent<MeshRenderer>();
                    if (renderer != null) {
                        renderer.material.color = Color.yellow;
                    }
                }
                _targetIndicator.SetActive(true);
            }
        }
        
        private void UpdateBoundaryWarning() {
            if (_boundaryWarningIndicator != null && _targetTransform != null) {
                if (_targetWasOutOfBounds) {
                    var clampedPos = GridToWorldPosition(_lastTargetGridPos, 5f);
                    _boundaryWarningIndicator.transform.position = clampedPos;
                    _boundaryWarningIndicator.SetActive(true);
                    
                    // Pulse effect for warning
                    var scale = _boundaryWarningIndicator.transform.localScale;
                    scale.y = 5f + Mathf.Sin(Time.time * 3f) * 0.5f;
                    _boundaryWarningIndicator.transform.localScale = scale;
                } else {
                    _boundaryWarningIndicator.SetActive(false);
                }
            }
        }
        
        private void DestroyIndicators() {
            if (_targetIndicator != null) {
                if (Application.isPlaying) Object.Destroy(_targetIndicator);
                else Object.DestroyImmediate(_targetIndicator);
                _targetIndicator = null;
            }
            
            if (_boundaryWarningIndicator != null) {
                if (Application.isPlaying) Object.Destroy(_boundaryWarningIndicator);
                else Object.DestroyImmediate(_boundaryWarningIndicator);
                _boundaryWarningIndicator = null;
            }
        }
        
        // Configuration methods
        public void UpdateSettings(RecalculationSettings newSettings) {
            _settings = newSettings ?? new RecalculationSettings();
        }
        
        public RecalculationSettings GetSettings() => _settings;
    }
}