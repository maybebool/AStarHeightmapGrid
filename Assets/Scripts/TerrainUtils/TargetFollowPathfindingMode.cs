using UnityEngine;
using System.Collections.Generic;

namespace TerrainUtils.Pathfinding {
    /// <summary>
    /// Real-time target following mode for open-world games
    /// </summary>
    public class TargetFollowPathfindingMode : PathfindingModeBase {
        // Target configuration
        private Transform _targetTransform;
        private Vector2Int _lastTargetGridPos;
        private Vector2Int _lastStartGridPos;
        
        // Recalculation settings
        [System.Serializable]
        public class RecalculationSettings {
            public float MinRecalculationInterval = 0.5f;  // Minimum time between recalculations
            public float TargetMoveThreshold = 2f;         // Min distance target must move (in grid cells)
            public float SwarmMoveThreshold = 3f;          // Min distance swarm must move
            public bool UseDistanceBasedThrottling = true; // Recalculate less often when far from target
            public float MaxThrottleDistance = 50f;        // Distance at which max throttling occurs
            public float MaxThrottleMultiplier = 3f;       // Max interval multiplier when far away
        }
        
        private RecalculationSettings _settings;
        private float _lastRecalculationTime;
        
        // Visual indicators
        private GameObject _targetIndicator;
        
        public override string ModeName => "Target Follow";
        
        public TargetFollowPathfindingMode() : this(new RecalculationSettings()) { }
        
        public TargetFollowPathfindingMode(RecalculationSettings settings) {
            _settings = settings ?? new RecalculationSettings();
        }
        
        public void SetTarget(Transform target) {
            if (target == _targetTransform) return;
            
            _targetTransform = target;
            
            if (_targetTransform != null) {
                _lastTargetGridPos = WorldToGridPosition(_targetTransform.position);
                
                if (_isActive) {
                    ForceRecalculation();
                    UpdateTargetIndicator();
                }
                
                UpdateInstructionText($"Following target: {_targetTransform.name}");
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
        }
        
        public override void Cleanup() {
            base.Cleanup();
            DestroyIndicators();
        }
        
        public override void DrawDebugVisualization() {
            if (!_isActive || _targetTransform == null) return;
            
            // Draw target position
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_targetTransform.position, 2f);
            Gizmos.DrawLine(_targetTransform.position, _targetTransform.position + Vector3.up * 10f);
            
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
            
            return $"Target: {_targetTransform.name} | " +
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
            
            // Check target movement threshold
            var currentTargetGridPos = WorldToGridPosition(_targetTransform.position);
            var targetGridDistance = Vector2Int.Distance(currentTargetGridPos, _lastTargetGridPos);
            
            if (targetGridDistance < _settings.TargetMoveThreshold) {
                return false;
            }
            
            // Check swarm movement threshold (if applicable)
            if (IsPathActive()) {
                var currentSwarmPos = WorldToGridPosition(GetAverageBirdPosition());
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
            
            // Determine start position
            if (IsPathActive()) {
                startPos = WorldToGridPosition(GetAverageBirdPosition());
            } else {
                // If no active path, start from a default position or skip
                startPos = WorldToGridPosition(_context.TerrainInfo.transform.position);
            }
            
            // Get current target position (no prediction)
            endPos = WorldToGridPosition(_targetTransform.position);
            
            // Validate positions
            if (!IsValidGridPosition(startPos) || !IsValidGridPosition(endPos)) {
                Debug.LogWarning($"[Target Follow] Invalid positions for path calculation");
                return;
            }
            
            // Check if start and end are the same (birds have reached the target)
            if (startPos == endPos) {
                Debug.Log($"[Target Follow] Birds have reached the target position");
                // Don't clear the path, just skip recalculation
                _lastRecalculationTime = Time.time;
                _lastTargetGridPos = endPos;
                return;
            }
            
            // Store last positions
            _lastStartGridPos = startPos;
            _lastTargetGridPos = endPos;
            _lastRecalculationTime = Time.time;
            
            // Visualize
            ClearVisualization();
            SetCellColor(startPos, StartColor);
            SetCellColor(endPos, EndColor);
            
            // Request path
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
                // Remove collider
                var collider = _targetIndicator.GetComponent<Collider>();
                if (collider != null) Object.Destroy(collider);
                _targetIndicator.SetActive(false);
            }
        }
        
        private void UpdateTargetIndicator() {
            if (_targetIndicator != null && _targetTransform != null) {
                _targetIndicator.transform.position = _targetTransform.position + Vector3.up * 2f;
                _targetIndicator.SetActive(true);
            }
        }
        
        private void DestroyIndicators() {
            if (_targetIndicator != null) {
                if (Application.isPlaying) Object.Destroy(_targetIndicator);
                else Object.DestroyImmediate(_targetIndicator);
                _targetIndicator = null;
            }
        }
        
        // Configuration methods
        public void UpdateSettings(RecalculationSettings newSettings) {
            _settings = newSettings ?? new RecalculationSettings();
        }
        
        public RecalculationSettings GetSettings() => _settings;
    }
}