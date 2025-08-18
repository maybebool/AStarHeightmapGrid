using System.Collections.Generic;
using TerrainUtils.Pathfinding;
using UnityEngine;

namespace TerrainUtils {
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
            public bool UsePredictiveTargeting = true;     // Predict target's future position
            public float PredictionTime = 1f;              // How far ahead to predict
            public bool UseDistanceBasedThrottling = true; // Recalculate less often when far from target
            public float MaxThrottleDistance = 50f;        // Distance at which max throttling occurs
            public float MaxThrottleMultiplier = 3f;       // Max interval multiplier when far away
        }
        
        private RecalculationSettings _settings;
        private float _lastRecalculationTime;
        private Vector3 _lastTargetPosition;
        private Vector3 _targetVelocity;
        private Queue<Vector3> _targetPositionHistory;
        private const int VELOCITY_SAMPLE_SIZE = 5;
        
        // Visual indicators
        private GameObject _targetIndicator;
        private GameObject _predictedPositionIndicator;
        private LineRenderer _predictionLine;
        
        public override string ModeName => "Target Follow";
        
        public TargetFollowPathfindingMode() : this(new RecalculationSettings()) { }
        
        public TargetFollowPathfindingMode(RecalculationSettings settings) {
            _settings = settings ?? new RecalculationSettings();
            _targetPositionHistory = new Queue<Vector3>(VELOCITY_SAMPLE_SIZE);
        }
        
        public void SetTarget(Transform target) {
            if (target == _targetTransform) return;
            
            _targetTransform = target;
            _targetPositionHistory.Clear();
            
            if (_targetTransform != null) {
                _lastTargetPosition = _targetTransform.position;
                _lastTargetGridPos = WorldToGridPosition(_lastTargetPosition);
                
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
            
            // Update target velocity tracking
            UpdateTargetVelocity();
            
            // Check if recalculation is needed
            if (ShouldRecalculatePath()) {
                RecalculatePath();
            }
            
            // Update visual indicators
            UpdateIndicators();
        }
        
        public override void Cleanup() {
            base.Cleanup();
            DestroyIndicators();
            _targetPositionHistory.Clear();
        }
        
        public override void DrawDebugVisualization() {
            if (!_isActive || _targetTransform == null) return;
            
            // Draw target position
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_targetTransform.position, 2f);
            Gizmos.DrawLine(_targetTransform.position, _targetTransform.position + Vector3.up * 10f);
            
            // Draw predicted position if using prediction
            if (_settings.UsePredictiveTargeting && _targetVelocity.magnitude > 0.1f) {
                var predictedPos = GetPredictedTargetPosition();
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(predictedPos, 1.5f);
                Gizmos.DrawLine(_targetTransform.position, predictedPos);
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
            var velocity = _targetVelocity.magnitude;
            var throttleMultiplier = GetThrottleMultiplier();
            
            return $"Target: {_targetTransform.name} | " +
                   $"Velocity: {velocity:F1} m/s | " +
                   $"Last Recalc: {timeSinceRecalc:F1}s ago | " +
                   $"Throttle: {throttleMultiplier:F1}x";
        }
        
        private void UpdateTargetVelocity() {
            if (_targetTransform == null) return;
            
            var currentPos = _targetTransform.position;
            
            // Add to history
            _targetPositionHistory.Enqueue(currentPos);
            if (_targetPositionHistory.Count > VELOCITY_SAMPLE_SIZE) {
                _targetPositionHistory.Dequeue();
            }
            
            // Calculate average velocity
            if (_targetPositionHistory.Count >= 2) {
                var positions = new List<Vector3>(_targetPositionHistory);
                Vector3 totalVelocity = Vector3.zero;
                
                for (int i = 1; i < positions.Count; i++) {
                    var vel = (positions[i] - positions[i - 1]) / Time.deltaTime;
                    totalVelocity += vel;
                }
                
                _targetVelocity = totalVelocity / (positions.Count - 1);
            }
            
            _lastTargetPosition = currentPos;
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
        
        private Vector3 GetPredictedTargetPosition() {
            if (!_settings.UsePredictiveTargeting || _targetVelocity.magnitude < 0.1f) {
                return _targetTransform.position;
            }
            
            // Predict future position based on velocity
            var predictedPos = _targetTransform.position + _targetVelocity * _settings.PredictionTime;
            
            // Clamp to terrain bounds if needed
            // (You might want to add terrain boundary checking here)
            
            return predictedPos;
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
            
            // Determine end position (with prediction if enabled)
            if (_settings.UsePredictiveTargeting) {
                endPos = WorldToGridPosition(GetPredictedTargetPosition());
            } else {
                endPos = WorldToGridPosition(_targetTransform.position);
            }
            
            // Validate positions
            if (!IsValidGridPosition(startPos) || !IsValidGridPosition(endPos)) {
                Debug.LogWarning($"[Target Follow] Invalid positions for path calculation");
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
            
            // Create predicted position indicator
            if (_predictedPositionIndicator == null && _settings.UsePredictiveTargeting) {
                _predictedPositionIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _predictedPositionIndicator.name = "PredictedPositionIndicator";
                _predictedPositionIndicator.transform.localScale = Vector3.one * 2f;
                var renderer = _predictedPositionIndicator.GetComponent<MeshRenderer>();
                if (renderer != null) {
                    renderer.material.color = Color.magenta;
                }
                // Remove collider
                var collider = _predictedPositionIndicator.GetComponent<Collider>();
                if (collider != null) Object.Destroy(collider);
                _predictedPositionIndicator.SetActive(false);
            }
            
            // Create prediction line
            if (_predictionLine == null && _settings.UsePredictiveTargeting) {
                var lineObj = new GameObject("PredictionLine");
                _predictionLine = lineObj.AddComponent<LineRenderer>();
                _predictionLine.startWidth = 0.5f;
                _predictionLine.endWidth = 0.5f;
                _predictionLine.material = new Material(Shader.Find("Sprites/Default"));
                _predictionLine.startColor = Color.yellow;
                _predictionLine.endColor = Color.magenta;
                _predictionLine.enabled = false;
            }
        }
        
        private void UpdateIndicators() {
            UpdateTargetIndicator();
            UpdatePredictionIndicator();
        }
        
        private void UpdateTargetIndicator() {
            if (_targetIndicator != null && _targetTransform != null) {
                _targetIndicator.transform.position = _targetTransform.position + Vector3.up * 2f;
                _targetIndicator.SetActive(true);
            }
        }
        
        private void UpdatePredictionIndicator() {
            if (!_settings.UsePredictiveTargeting || _targetTransform == null) return;
            
            if (_predictedPositionIndicator != null && _targetVelocity.magnitude > 0.1f) {
                var predictedPos = GetPredictedTargetPosition();
                _predictedPositionIndicator.transform.position = predictedPos + Vector3.up * 2f;
                _predictedPositionIndicator.SetActive(true);
            } else if (_predictedPositionIndicator != null) {
                _predictedPositionIndicator.SetActive(false);
            }
            
            if (_predictionLine != null && _targetVelocity.magnitude > 0.1f) {
                _predictionLine.enabled = true;
                _predictionLine.SetPosition(0, _targetTransform.position + Vector3.up * 2f);
                _predictionLine.SetPosition(1, GetPredictedTargetPosition() + Vector3.up * 2f);
            } else if (_predictionLine != null) {
                _predictionLine.enabled = false;
            }
        }
        
        private void DestroyIndicators() {
            if (_targetIndicator != null) {
                if (Application.isPlaying) Object.Destroy(_targetIndicator);
                else Object.DestroyImmediate(_targetIndicator);
                _targetIndicator = null;
            }
            
            if (_predictedPositionIndicator != null) {
                if (Application.isPlaying) Object.Destroy(_predictedPositionIndicator);
                else Object.DestroyImmediate(_predictedPositionIndicator);
                _predictedPositionIndicator = null;
            }
            
            if (_predictionLine != null) {
                if (Application.isPlaying) Object.Destroy(_predictionLine.gameObject);
                else Object.DestroyImmediate(_predictionLine.gameObject);
                _predictionLine = null;
            }
        }
        
        // Configuration methods
        public void UpdateSettings(RecalculationSettings newSettings) {
            _settings = newSettings ?? new RecalculationSettings();
        }
        
        public RecalculationSettings GetSettings() => _settings;
    }
}