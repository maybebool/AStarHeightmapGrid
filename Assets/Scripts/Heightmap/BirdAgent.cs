using System.Collections.Generic;
using UnityEngine;

namespace Heightmap {
    /// <summary>
    /// Controls individual bird movement along a calculated path.
    /// Follows dependency injection pattern - all dependencies are provided externally.
    /// </summary>
    public class BirdAgent : MonoBehaviour {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 10f;
        [SerializeField] private float rotationSpeed = 5f;
        [SerializeField] private float heightOffset = 5f;
        [SerializeField] private float reachThreshold = 2f;
        
        [Header("Visual Settings")]
        [SerializeField] private bool showDebugPath = false;
        [SerializeField] private Color debugPathColor = Color.cyan;
        
        // Path following state
        private List<Vector3> _worldPath;
        private int _currentPathIndex;
        private Vector3 _currentTarget;
        private bool _isMoving;
        
        // Public properties
        public bool HasReachedDestination => !_isMoving || (_worldPath != null && _currentPathIndex >= _worldPath.Count);
        public Vector3 CurrentPosition => transform.position;
        public bool IsMoving => _isMoving;
        
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
            _currentPathIndex = 0;
            _isMoving = true;
            
            if (teleportToStart) {
                transform.position = _worldPath[0];
            }
            
            UpdateCurrentTarget();
        }
        
        /// <summary>
        /// Stops all movement and clears the current path.
        /// </summary>
        public void StopMovement() {
            _isMoving = false;
            _worldPath = null;
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
            if (_worldPath != null && _currentPathIndex < _worldPath.Count) {
                _isMoving = true;
            }
        }
        
        private void Update() {
            if (!_isMoving || _worldPath == null || _currentPathIndex >= _worldPath.Count) {
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
        }
        
        /// <summary>
        /// Called when the bird completes its path. Can be overridden for custom behavior.
        /// </summary>
        protected virtual void OnPathComplete() {
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