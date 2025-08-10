using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

namespace Heightmap {
    public class PathFinding : MonoBehaviour {
        
        [SerializeField] private int samplesPerDimension = 4;
        public int SamplesPerDimension => samplesPerDimension;
        [SerializeField] private float flyCostMultiplier = 1.25f;
        [SerializeField] private TerrainInfo terrainInfo;
        [SerializeField] private TextMeshProUGUI time;
        
        // [Header("Visualization")]
        // [SerializeField] private PathVisualizer pathVisualizer;
        // [SerializeField] private bool use3DVisualization = true;
        // [SerializeField] private bool keepOldVisualization = false;
        
        private List<PathNode> _path = new();
        private PathNode _start;
        private PathNode _end;
        private Stopwatch _timer;
        private readonly List<PathNode> _openNodes = new();
        private readonly List<PathNode> _closedNodes = new();

        private PathGrid _pathGrid;
        private Coroutine _currentCoroutine;
        private float[,] _currentTerrainHeights;
        
        [Header("Marker Settings")]
        [SerializeField] private GameObject markerPrefab;
        [SerializeField] private float markerHeight = 27.0f;
        private GameObject _currentStartMarker;
        private GameObject _currentEndMarker;
        
        private void Start() {
            _timer = new Stopwatch();
            // Try to find PathVisualizer if not assigned
            // if (!pathVisualizer && use3DVisualization) {
            //     pathVisualizer = FindObjectOfType<PathVisualizer>();
            //     if (!pathVisualizer) {
            //         Debug.LogWarning("PathVisualizer not found. Creating one...");
            //         GameObject visualizerObj = new GameObject("PathVisualizer");
            //         pathVisualizer = visualizerObj.AddComponent<PathVisualizer>();
            //     }
            // }
        }
        
        private void OnValidate() {
            samplesPerDimension =  Mathf.Clamp(Mathf.ClosestPowerOfTwo(samplesPerDimension), 2, int.MaxValue);
        }

        private void SpawnMarkers() {
            if (_currentStartMarker) {
                Destroy(_currentStartMarker);
            }
            if (_currentEndMarker) {
                Destroy(_currentEndMarker);
            }
            
            var cellSize = terrainInfo.CellSize;
            
            var startPos = _pathGrid.GetWorldPositionFromNodeIndex(_start.Index, markerHeight, cellSize);
            var endPos = _pathGrid.GetWorldPositionFromNodeIndex(_end.Index, markerHeight, cellSize);
    
            if (markerPrefab)
                _currentStartMarker = Instantiate(markerPrefab, startPos, Quaternion.identity);
    
            if (markerPrefab)
                _currentEndMarker = Instantiate(markerPrefab, endPos, Quaternion.identity);
        }
        
        public void RandomPathSearchEvent() {
            _timer = Stopwatch.StartNew();
            RandomizePath();
            if (_currentCoroutine != null) {
                StopCoroutine(_currentCoroutine);
            }
            _currentCoroutine = StartCoroutine(SetColorToPathCoroutine());
        }

        /// <summary>
        /// Calculates the shortest path between the start and end nodes using the A* algorithm.
        /// </summary>
        /// <param name="start">The starting node for the pathfinding algorithm.</param>
        /// <param name="end">The target node to reach during the pathfinding operation.</param>
        /// <returns>A list of PathNode objects representing the calculated path from the start node to the end node, or null if no path is found.</returns>
        private List<PathNode> CalculateAStarPath(PathNode start, PathNode end) {
            if (_pathGrid == null || !terrainInfo) {
                return null;
            }
            _openNodes.Clear();
            _closedNodes.Clear();
            
            var terrainHeights = terrainInfo.SampleHeights(samplesPerDimension, false);
            start.GCost = 0;
            start.HCost = Vector2.Distance(start.Index, end.Index);
            start.FlyCost = 0;
            _openNodes.Add(start);
            _timer.Start();
            while (_openNodes.Count > 0) {
                var currentNode = GetLowestCostNode();
                if (currentNode == end) {
                    return GetFinalPath(currentNode);
                }

                _openNodes.Remove(currentNode);
                _closedNodes.Add(currentNode);
                var neighbours = _pathGrid.GetAllNeighbours(currentNode.Index);
                foreach (var neighbour in neighbours) {
                    if (neighbour.IsWall) {
                        _closedNodes.Add(neighbour);
                        continue;
                    }
                    
                    if (!_closedNodes.Contains(neighbour)) {
                        UpdateNeighborCosts(currentNode, neighbour, end, _currentTerrainHeights);

                        if (!_openNodes.Contains(neighbour)) {
                            _openNodes.Add(neighbour);
                        }
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// Updates the cost values for a neighboring node during pathfinding.
        /// </summary>
        /// <param name="currentNode">The current node being evaluated in the pathfinding process.</param>
        /// <param name="neighbour">The neighboring node whose costs are to be updated.</param>
        /// <param name="end">The destination node for the pathfinding operation.</param>
        /// <param name="terrainHeights">A 2D array of terrain heights used to calculate fly costs within the pathfinding algorithm.</param>
        private void UpdateNeighborCosts(PathNode currentNode, PathNode neighbour, PathNode end, float[,] terrainHeights) {
            var isDiagonal = currentNode.Index.x != neighbour.Index.x && currentNode.Index.y != neighbour.Index.y;
            var gCost = currentNode.GCost + (isDiagonal ? 1.4f : 1f);
            var flyCost = (terrainHeights[neighbour.Index.x, neighbour.Index.y] -
                           terrainHeights[currentNode.Index.x, currentNode.Index.y]) * flyCostMultiplier;
            
            if (!(gCost + flyCost < neighbour.GCost)) return;
            neighbour.GCost = gCost;
            neighbour.FlyCost = flyCost;
            neighbour.HCost = Vector2.Distance(neighbour.Index, end.Index);
            neighbour.SourceNode = currentNode;
        }


        private List<PathNode> GetFinalPath(PathNode endNode) {
            List<PathNode> finalPath = new();
            var pathNode = endNode;
            finalPath.Add(pathNode);
            
            while (pathNode.SourceNode != null) {
                pathNode = pathNode.SourceNode;
                finalPath.Add(pathNode);
            }

            finalPath.Reverse();
            _timer.Stop();
            var s = _timer.ElapsedMilliseconds;
            time.text = s.ToString();
        
            return finalPath;
        
        }

        private PathNode GetLowestCostNode() {
            var lowestCostNode = _openNodes[0];
            var lowestCost = lowestCostNode.FCost;
            foreach (var node in _openNodes) {
                var nodeCost = node.FCost;
                if (node.FCost < lowestCost) {
                    lowestCost = nodeCost;
                    lowestCostNode = node;
                }
            }
            return lowestCostNode;
        }
        
        private void RandomizePath() {
            _pathGrid = new PathGrid(samplesPerDimension, samplesPerDimension, terrainInfo.transform.position);
            _start = _pathGrid.GetRandomNode();
            _end = _pathGrid.GetRandomNode();
    
            while (_start == _end) {
                _end = _pathGrid.GetRandomNode();
            }
            terrainInfo.SetColor(_start.Index, Color.white);
            terrainInfo.SetColor(_end.Index, Color.white);
        }
        
        private IEnumerator SetColorToPathCoroutine() {
            _path = CalculateAStarPath(_start, _end);
            SpawnMarkers();
            foreach (var node in _path) {
                terrainInfo.SetColor(node.Index, Color.black);
                yield return new WaitForSeconds(0.05f);
            }
        }
    }
}
