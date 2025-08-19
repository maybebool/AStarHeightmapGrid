using System.Collections.Generic;
using UnityEngine;

namespace TerrainUtils {
    
    public static class PathSmoother {
        
        public struct SmoothingConfig {
            public float CornerAngleThreshold;
            public float MinTurnRadius;
            public float MaxTurnRadius;
            public int BezierSegments;
            public float TerrainClearance;
            public float PreferredClearance;
            public float TurnSpeedMultiplier;
            
            public static SmoothingConfig Default => new() {
                CornerAngleThreshold = 45f,
                MinTurnRadius = 3f,
                MaxTurnRadius = 10f,
                BezierSegments = 5,
                TerrainClearance = 2f,
                PreferredClearance = 5f,
                TurnSpeedMultiplier = 0.7f
            };
        }
        
        public struct BirdVariation {
            public float turnRadiusMultiplier;
            public float speedMultiplier;
            public float reactionDelay;
            public float heightPreference;
            
            public static BirdVariation Random() {
                return new BirdVariation {
                    turnRadiusMultiplier = UnityEngine.Random.Range(0.8f, 1.2f),
                    speedMultiplier = UnityEngine.Random.Range(0.9f, 1.1f),
                    reactionDelay = UnityEngine.Random.Range(0f, 0.3f),
                    heightPreference = UnityEngine.Random.Range(-2f, 2f)
                };
            }
        }
        
        public static List<Vector3> SmoothPath(List<Vector3> rawPath, 
            SmoothingConfig config, BirdVariation variation, out List<float> speedModifiers) {
            
            speedModifiers = new List<float>();
            
            if (rawPath == null || rawPath.Count < 3) {
                if (rawPath != null) {
                    for (int i = 0; i < rawPath.Count; i++) {
                        speedModifiers.Add(1f);
                    }
                }
                return rawPath;
            }
            
            var smoothedPath = new List<Vector3>();
            var tempSpeedMods = new List<float>();
            
            smoothedPath.Add(AdjustHeightForVariation(rawPath[0], variation.heightPreference));
            tempSpeedMods.Add(1f);
            
            for (int i = 1; i < rawPath.Count - 1; i++) {
                var prev = rawPath[i - 1];
                var current = rawPath[i];
                var next = rawPath[i + 1];
                
                var dirIn = (current - prev).normalized;
                var dirOut = (next - current).normalized;
                var angle = Vector3.Angle(dirIn, dirOut);
                
                if (angle >= config.CornerAngleThreshold) {
                    var (curvePoints, curveSpeeds) = GenerateBezierCorner(
                        prev, current, next, 
                        angle, config, variation
                    );
                    
                    for (int j = 1; j < curvePoints.Count; j++) {
                        smoothedPath.Add(curvePoints[j]);
                        tempSpeedMods.Add(curveSpeeds[j]);
                    }
                } else {
                    smoothedPath.Add(AdjustHeightForVariation(current, variation.heightPreference));
                    tempSpeedMods.Add(1f * variation.speedMultiplier);
                }
            }
            
            smoothedPath.Add(AdjustHeightForVariation(
                rawPath[rawPath.Count - 1], 
                variation.heightPreference
            ));
            tempSpeedMods.Add(1f);
            
            smoothedPath = EnsureTerrainClearance(smoothedPath, config);
            
            speedModifiers = tempSpeedMods;
            return smoothedPath;
        }
        
        private static (List<Vector3> points, List<float> speeds) GenerateBezierCorner(
            Vector3 prev, Vector3 corner, Vector3 next,
            float angle, SmoothingConfig config, BirdVariation variation) {
            
            var curvePoints = new List<Vector3>();
            var speeds = new List<float>();
            
            var normalizedAngle = Mathf.Clamp01(angle / 180f);
            var turnRadius = Mathf.Lerp(config.MinTurnRadius, config.MaxTurnRadius, normalizedAngle);
            turnRadius *= variation.turnRadiusMultiplier;
            
            var offsetDistance = turnRadius * Mathf.Tan(angle * 0.5f * Mathf.Deg2Rad);
            offsetDistance = Mathf.Min(offsetDistance, Vector3.Distance(prev, corner) * 0.4f);
            offsetDistance = Mathf.Min(offsetDistance, Vector3.Distance(corner, next) * 0.4f);
            
            var entryPoint = corner - (corner - prev).normalized * offsetDistance;
            var exitPoint = corner + (next - corner).normalized * offsetDistance;
            
            var controlPoint = corner;
            
            var heightVariation = variation.heightPreference * 0.5f;
            controlPoint.y += heightVariation;
            
            for (int i = 0; i <= config.BezierSegments; i++) {
                var t = i / (float)config.BezierSegments;
                
                var oneMinusT = 1f - t;
                var point = oneMinusT * oneMinusT * entryPoint +
                            2f * oneMinusT * t * controlPoint +
                            t * t * exitPoint;
                
                point.y += variation.heightPreference;
                
                curvePoints.Add(point);
                
                var speedMod = Mathf.Lerp(1f, config.TurnSpeedMultiplier, 
                    Mathf.Sin(t * Mathf.PI));
                speeds.Add(speedMod * variation.speedMultiplier);
            }
            
            return (curvePoints, speeds);
        }
        
        private static Vector3 AdjustHeightForVariation(Vector3 point, float heightPreference) {
            point.y += heightPreference;
            return point;
        }
        
        private static List<Vector3> EnsureTerrainClearance(List<Vector3> path, SmoothingConfig config) {
            var adjustedPath = new List<Vector3>(path.Count);
            
            foreach (var point in path) {
                var adjustedPoint = point;
                
                if (Physics.Raycast(point + Vector3.up * 100f, Vector3.down, 
                    out RaycastHit hit, 200f)) {
                    
                    var terrainHeight = hit.point.y;
                    var desiredHeight = terrainHeight + config.PreferredClearance;
                    
                    if (adjustedPoint.y < terrainHeight + config.TerrainClearance) {
                        adjustedPoint.y = desiredHeight;
                    }
                }
                
                adjustedPath.Add(adjustedPoint);
            }
            
            for (int i = 1; i < adjustedPath.Count - 1; i++) {
                var prevHeight = adjustedPath[i - 1].y;
                var currentHeight = adjustedPath[i].y;
                var nextHeight = adjustedPath[i + 1].y;
                
                var avgHeight = (prevHeight + nextHeight) * 0.5f;
                var heightDiff = Mathf.Abs(currentHeight - avgHeight);
                
                if (heightDiff > 5f) {
                    var smoothedPoint = adjustedPath[i];
                    smoothedPoint.y = Mathf.Lerp(currentHeight, avgHeight, 0.5f);
                    adjustedPath[i] = smoothedPoint;
                }
            }
            
            return adjustedPath;
        }
        
        public static float GetMaxTurnRate(float normalizedSpeed) {
            if (normalizedSpeed > 0.8f) {
                return 120f;
            }

            if (normalizedSpeed > 0.4f) {
                return 150f;
            }

            return 180f;
        }
    }
}