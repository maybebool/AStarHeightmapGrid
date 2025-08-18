using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainUtils {

    public sealed class BirdAgent : MonoBehaviour {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 10f;
        [SerializeField] private float rotationSpeed = 5f;
        [SerializeField] private float reachThreshold = 2f;
        [SerializeField] private bool instantRotationOnNewPath = false;
        
        [Header("Path Smoothing")]
        [SerializeField] private bool enablePathSmoothing = true;
        [SerializeField] private float cornerAngleThreshold = 45f;
        [SerializeField] private float minTurnRadius = 3f;
        [SerializeField] private float maxTurnRadius = 10f;
        [SerializeField] private float terrainClearance = 2f;
        [SerializeField] private float preferredClearance = 5f;
        [SerializeField] private AnimationCurve turnSpeedCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.6f);
        
        [Header("Turn Rate Limiting")]
        [SerializeField] private bool enableTurnRateLimit = true;
        [SerializeField] private float baseTurnRate = 150f;
        
        [Header("Swarm Behavior")]
        [SerializeField] private bool maintainOffset = true;
        
        [Header("Visual Settings")]
        [SerializeField] private bool showDebugPath = false;
        [SerializeField] private Color debugPathColor = Color.cyan;
        [SerializeField] private Color smoothedPathColor = Color.green;
        [SerializeField] private bool showSpeedIndicators = false;
        
        private List<Vector3> _rawPath;
        private List<Vector3> _smoothedPath;
        private List<float> _speedModifiers;
        private int _currentPathIndex;
        private Vector3 _currentTarget;
        private bool _isMoving;
        private Vector3 _pathOffset;
        private float _heightOffsetValue;
        
        private PathSmoother.SmoothingConfig _smoothingConfig;
        private PathSmoother.BirdVariation _birdVariation;
        
        private float _currentSpeed;
        private float _targetSpeed;
        private Quaternion _targetRotation;
        
        private int _lookAheadDistance = 2;
        private Vector3 _lookAheadTarget;
        
        public Vector3 CurrentPosition => transform.position;
        
        private void Awake() {
            InitializeSmoothingConfig();
        }
        
        private void InitializeSmoothingConfig() {
            _smoothingConfig = new PathSmoother.SmoothingConfig {
                CornerAngleThreshold = cornerAngleThreshold,
                MinTurnRadius = minTurnRadius,
                MaxTurnRadius = maxTurnRadius,
                BezierSegments = 5,
                TerrainClearance = terrainClearance,
                PreferredClearance = preferredClearance,
                TurnSpeedMultiplier = 0.7f
            };
        }
        
        public void InitializeSwarmBehavior(float offsetRadius, float heightVar) {
            if (offsetRadius > 0) {
                var angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                var radius = Random.Range(0f, offsetRadius);
                _pathOffset = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius
                );
            } else {
                _pathOffset = Vector3.zero;
            }
            
            _heightOffsetValue = Random.Range(-heightVar, heightVar);
            
            _birdVariation = PathSmoother.BirdVariation.Random();
            _currentSpeed = moveSpeed * _birdVariation.speedMultiplier;
            _targetSpeed = _currentSpeed;
        }
        
        public void SetPath(List<Vector3> worldPath, bool teleportToStart = true) {
            if (worldPath == null || worldPath.Count == 0) {
                StopMovement();
                return;
            }
            
            _rawPath = new List<Vector3>(worldPath);
            
            List<Vector3> offsetPath = CreateOffsetPath(_rawPath);
            
            if (enablePathSmoothing && offsetPath.Count >= 3) {
                _smoothedPath = PathSmoother.SmoothPath(
                    offsetPath, 
                    _smoothingConfig, 
                    _birdVariation,
                    out _speedModifiers
                );
            } else {
                _smoothedPath = new List<Vector3>(offsetPath);
                _speedModifiers = new List<float>();
                for (int i = 0; i < _smoothedPath.Count; i++) {
                    _speedModifiers.Add(1f);
                }
            }
            
            _currentPathIndex = 0;
            _isMoving = true;
            
            if (teleportToStart) {
                transform.position = _smoothedPath[0];
                UpdateCurrentTarget();
                if (_currentTarget != transform.position) {
                    transform.rotation = Quaternion.LookRotation(_currentTarget - transform.position);
                }
            } else {
                _currentPathIndex = FindClosestPathIndex(transform.position, _smoothedPath);
                UpdateCurrentTarget();
            }
        }
        
        private List<Vector3> CreateOffsetPath(List<Vector3> originalPath) {
            var offsetPath = new List<Vector3>(originalPath.Count);
            
            for (int i = 0; i < originalPath.Count; i++) {
                var point = originalPath[i];
                
                if (maintainOffset) {
                    point += _pathOffset;
                    point.y += _heightOffsetValue;
                } else {
                    var t = (float)i / (originalPath.Count - 1);
                    var dynamicOffset = _pathOffset * Mathf.Sin(t * Mathf.PI * 2f + Time.time);
                    point += dynamicOffset;
                    point.y += _heightOffsetValue * Mathf.Cos(t * Mathf.PI + Time.time);
                }
                
                offsetPath.Add(point);
            }
            
            return offsetPath;
        }
        
        private int FindClosestPathIndex(Vector3 position, List<Vector3> path) {
            var closestIndex = 0;
            var closestDistance = float.MaxValue;
            
            for (int i = 0; i < path.Count; i++) {
                var distance = Vector3.Distance(position, path[i]);
                if (distance < closestDistance) {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }
            
            if (closestIndex < path.Count - 1 && closestDistance < reachThreshold) {
                closestIndex++;
            }
            
            return closestIndex;
        }
        
        public void StopMovement() {
            _isMoving = false;
            _rawPath = null;
            _smoothedPath = null;
            _speedModifiers = null;
            _currentPathIndex = 0;
            _currentSpeed = 0f;
        }
        
        public void PauseMovement() {
            _isMoving = false;
        }
        
        public void ResumeMovement() {
            if (_smoothedPath != null && _currentPathIndex < _smoothedPath.Count) {
                _isMoving = true;
            }
        }
        
        private void Update() {
            if (!_isMoving || _smoothedPath == null || _currentPathIndex >= _smoothedPath.Count) {
                return;
            }
            
            UpdateSpeedModulation();
            MoveAlongSmoothedPath();
        }
        
        private void UpdateSpeedModulation() {
            var speedMod = 1f;
            if (_speedModifiers != null && _currentPathIndex < _speedModifiers.Count) {
                speedMod = _speedModifiers[_currentPathIndex];
            }
            
            if (turnSpeedCurve != null && turnSpeedCurve.keys.Length > 0) {
                float curvePosition = _currentPathIndex / (float)Mathf.Max(1, _smoothedPath.Count - 1);
                speedMod *= turnSpeedCurve.Evaluate(curvePosition);
            }
            
            _targetSpeed = moveSpeed * speedMod * _birdVariation.speedMultiplier;
            _currentSpeed = Mathf.Lerp(_currentSpeed, _targetSpeed, Time.deltaTime * 2f);
        }
        
        private void MoveAlongSmoothedPath() {
            UpdateLookAheadTarget();
            
            if (_lookAheadTarget != transform.position) {
                Vector3 lookDirection = (_lookAheadTarget - transform.position).normalized;
                _targetRotation = Quaternion.LookRotation(lookDirection);
            }
            
            if (enableTurnRateLimit) {
                var maxTurnRate = PathSmoother.GetMaxTurnRate(_currentSpeed / moveSpeed);
                var maxTurnThisFrame = maxTurnRate * Time.deltaTime;
                
                var angleDiff = Quaternion.Angle(transform.rotation, _targetRotation);
                
                if (angleDiff > maxTurnThisFrame) {
                    var t = maxTurnThisFrame / angleDiff;
                    transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, t);
                } else {
                    transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, 
                        rotationSpeed * Time.deltaTime);
                }
            } else {
                transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, 
                    rotationSpeed * Time.deltaTime);
            }
            
            transform.position = Vector3.MoveTowards(transform.position, _currentTarget, 
                _currentSpeed * Time.deltaTime);
            
            if (Vector3.Distance(transform.position, _currentTarget) < reachThreshold) {
                _currentPathIndex++;
                if (_currentPathIndex < _smoothedPath.Count) {
                    UpdateCurrentTarget();
                } else {
                    _isMoving = false;
                    OnPathComplete();
                }
            }
        }
        
        private void UpdateCurrentTarget() {
            if (_currentPathIndex >= _smoothedPath.Count) return;
            _currentTarget = _smoothedPath[_currentPathIndex];
        }
        
        private void UpdateLookAheadTarget() {
            int lookAheadIndex = Mathf.Min(
                _currentPathIndex + _lookAheadDistance, 
                _smoothedPath.Count - 1
            );
            
            _lookAheadTarget = _smoothedPath[lookAheadIndex];
        }
        
        private void OnPathComplete() {
            Debug.Log($"{gameObject.name} completed path");
        }
        
        private void OnDrawGizmos() {
            if (!showDebugPath || !_isMoving) return;
            
            if (_rawPath != null && _rawPath.Count > 1) {
                Gizmos.color = debugPathColor;
                for (int i = 0; i < _rawPath.Count - 1; i++) {
                    Gizmos.DrawLine(_rawPath[i], _rawPath[i + 1]);
                    Gizmos.DrawWireSphere(_rawPath[i], 0.5f);
                }
            }
            
            if (_smoothedPath != null && _smoothedPath.Count > 1) {
                Gizmos.color = smoothedPathColor;
                for (int i = _currentPathIndex; i < _smoothedPath.Count - 1; i++) {
                    Gizmos.DrawLine(_smoothedPath[i], _smoothedPath[i + 1]);
                    
                    if (showSpeedIndicators && _speedModifiers != null && i < _speedModifiers.Count) {
                        float speedMod = _speedModifiers[i];
                        Gizmos.color = Color.Lerp(Color.red, Color.green, speedMod);
                        Gizmos.DrawWireSphere(_smoothedPath[i], 0.3f);
                    }
                }
            }
            
            if (_currentPathIndex < _smoothedPath.Count) {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(_currentTarget, 1f);
            }
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_lookAheadTarget, 0.8f);
            Gizmos.DrawLine(transform.position, _lookAheadTarget);
        }
    }
}