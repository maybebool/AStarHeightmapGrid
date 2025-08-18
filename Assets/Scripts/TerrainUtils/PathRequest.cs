using System.Collections.Generic;
using UnityEngine;

namespace TerrainUtils {
    /// <summary>
    /// Encapsulates a pathfinding request to maintain modularity between modes and DOTS service
    /// </summary>
    public struct PathRequest {
        public Vector2Int StartGridPos { get; }
        public Vector2Int EndGridPos { get; }
        public float HeightOffset { get; }
        public int RequestId { get; }
        public float Timestamp { get; }
        
        // Optional parameters for optimization
        public bool IsUrgent { get; }
        public float Priority { get; }

        public PathRequest(Vector2Int start, Vector2Int end, float heightOffset, 
            bool isUrgent = false, float priority = 1.0f) {
            StartGridPos = start;
            EndGridPos = end;
            HeightOffset = heightOffset;
            RequestId = GetNextRequestId();
            Timestamp = Time.time;
            IsUrgent = isUrgent;
            Priority = priority;
        }

        private static int _requestCounter = 0;
        private static int GetNextRequestId() => ++_requestCounter;

        public bool IsValid() {
            return StartGridPos != EndGridPos;
        }
    }

    /// <summary>
    /// Result of a pathfinding calculation
    /// </summary>
    public class PathResult {
        public List<Vector3> WorldPath { get; }
        public int RequestId { get; }
        public float CalculationTime { get; }
        public bool Success { get; }
        public string ErrorMessage { get; }

        public PathResult(List<Vector3> path, int requestId, float calcTime, bool success = true, string error = null) {
            WorldPath = path;
            RequestId = requestId;
            CalculationTime = calcTime;
            Success = success;
            ErrorMessage = error;
        }

        public static PathResult Failed(int requestId, string reason) {
            return new PathResult(null, requestId, 0f, false, reason);
        }
    }
}