using System.Collections.Generic;
using UnityEngine;

namespace PathFinder {

    public sealed class BirdAgent : MonoBehaviour {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 10f;
        [SerializeField] private float rotationSpeed = 5f;
        [SerializeField] private float reachThreshold = 2f;
        [SerializeField] private bool instantRotationOnNewPath = true;
        
        [Header("Swarm Behavior")]
        [SerializeField] private bool maintainOffset = true;
        
        [Header("Visual Settings")]
        [SerializeField] private bool showDebugPath = false;
        [SerializeField] private Color debugPathColor = Color.cyan;
        
        // Path following state
        private List<Vector3> _worldPath;
        private List<Vector3> _offsetPath;
        private int _currentPathIndex;
        private Vector3 _currentTarget;
        private bool _isMoving;
        private Vector3 _pathOffset; 
        private float _heightOffsetValue; 
        
        // Public properties
        public bool HasReachedDestination => !_isMoving || (_worldPath != null && _currentPathIndex >= _worldPath.Count);
        public Vector3 CurrentPosition => transform.position;
        public bool IsMoving => _isMoving;
        
        /// <summary>
        /// Initializes the bird with a unique offset for swarm behavior.
        /// </summary>
        public void InitializeSwarmBehavior(float offsetRadius, float heightVar) {
            if (offsetRadius > 0) {
                // Generate a random offset in the XZ plane
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float radius = Random.Range(0f, offsetRadius);
                _pathOffset = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0,
                    Mathf.Sin(angle) * radius
                );
            } else {
                _pathOffset = Vector3.zero;
            }
            
            // Add height variation
            _heightOffsetValue = Random.Range(-heightVar, heightVar);
        }
        
        /// <summary>
        /// Sets a pre-calculated world space path for the bird to follow.
        /// Path should already include proper height offsets.
        /// </summary>
        /// <param name="worldPath">List of world positions to follow</param>
        /// <param name="teleportToStart">If true, immediately moves bird to start position</param>
        public void SetPath(List<Vector3> worldPath, bool teleportToStart = true) {
            if (worldPath == null || worldPath.Count == 0) {
                StopMovement();
                return;
            }
            
            _worldPath = new List<Vector3>(worldPath);
            
            // Create offset path for this bird
            _offsetPath = CreateOffsetPath(_worldPath);
            
            _currentPathIndex = 0;
            _isMoving = true;
            
            if (teleportToStart) {
                transform.position = _offsetPath[0];
            } else {
                // Find the closest point on the new path to continue from
                _currentPathIndex = FindClosestPathIndex(transform.position, _offsetPath);
            }
            
            UpdateCurrentTarget();
        }
        
        /// <summary>
        /// Creates an offset version of the path for this bird to follow.
        /// </summary>
        private List<Vector3> CreateOffsetPath(List<Vector3> originalPath) {
            List<Vector3> offsetPath = new List<Vector3>(originalPath.Count);
            
            for (int i = 0; i < originalPath.Count; i++) {
                Vector3 point = originalPath[i];
                
                if (maintainOffset) {
                    // Apply consistent offset throughout the path
                    point += _pathOffset;
                    point.y += _heightOffsetValue;
                } else {
                    // Create dynamic offset that changes along the path
                    float t = (float)i / (originalPath.Count - 1);
                    Vector3 dynamicOffset = _pathOffset * Mathf.Sin(t * Mathf.PI * 2f + Time.time);
                    point += dynamicOffset;
                    point.y += _heightOffsetValue * Mathf.Cos(t * Mathf.PI + Time.time);
                }
                
                offsetPath.Add(point);
            }
            
            return offsetPath;
        }
        
        /// <summary>
        /// Finds the closest point on the path to the given position.
        /// </summary>
        private int FindClosestPathIndex(Vector3 position, List<Vector3> path) {
            int closestIndex = 0;
            float closestDistance = float.MaxValue;
            
            for (int i = 0; i < path.Count; i++) {
                float distance = Vector3.Distance(position, path[i]);
                if (distance < closestDistance) {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }
            
            // Skip to next point if we're very close to the current closest point
            // This prevents birds from going backwards
            if (closestIndex < path.Count - 1 && closestDistance < reachThreshold) {
                closestIndex++;
            }
            
            return closestIndex;
        }
        
        /// <summary>
        /// Stops all movement and clears the current path.
        /// </summary>
        public void StopMovement() {
            _isMoving = false;
            _worldPath = null;
            _offsetPath = null;
            _currentPathIndex = 0;
        }
        
        /// <summary>
        /// Pauses movement without clearing the path.
        /// </summary>
        public void PauseMovement() {
            _isMoving = false;
        }
        
        /// <summary>
        /// Resumes movement if a path exists.
        /// </summary>
        public void ResumeMovement() {
            if (_offsetPath != null && _currentPathIndex < _offsetPath.Count) {
                _isMoving = true;
            }
        }
        
        private void Update() {
            if (!_isMoving || _offsetPath == null || _currentPathIndex >= _offsetPath.Count) {
                return;
            }
            
            MoveAlongPath();
        }
        
        private void MoveAlongPath() {
            // Move towards current target
            Vector3 direction = (_currentTarget - transform.position).normalized;
            transform.position = Vector3.MoveTowards(transform.position, _currentTarget, moveSpeed * Time.deltaTime);
            
            // Rotate towards movement direction
            if (direction != Vector3.zero) {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
            
            // Check if reached current waypoint
            if (Vector3.Distance(transform.position, _currentTarget) < reachThreshold) {
                _currentPathIndex++;
                if (_currentPathIndex < _worldPath.Count) {
                    UpdateCurrentTarget();
                } else {
                    _isMoving = false;
                    OnPathComplete();
                }
            }
        }
        
        private void UpdateCurrentTarget() {
            if (_currentPathIndex >= _worldPath.Count) return;
            _currentTarget = _worldPath[_currentPathIndex];
            
            // Immediately orient toward new target if enabled
            if (instantRotationOnNewPath) {
                Vector3 direction = (_currentTarget - transform.position).normalized;
                if (direction != Vector3.zero) {
                    transform.rotation = Quaternion.LookRotation(direction);
                }
            }
        }
        
        /// <summary>
        /// Called when the bird completes its path. Can be overridden for custom behavior.
        /// </summary>
        private void OnPathComplete() {
            // Override in derived classes if needed
        }
        
        private void OnDrawGizmos() {
            if (!showDebugPath || _worldPath == null || !_isMoving) return;
            
            Gizmos.color = debugPathColor;
            for (int i = _currentPathIndex; i < _worldPath.Count - 1; i++) {
                Gizmos.DrawLine(_worldPath[i], _worldPath[i + 1]);
            }
            
            // Draw sphere at current target
            if (_currentPathIndex < _worldPath.Count) {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(_currentTarget, 1f);
            }
        }
    }
}