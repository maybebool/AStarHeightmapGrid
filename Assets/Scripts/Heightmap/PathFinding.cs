using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using UnityEngine;

namespace Heightmap {
    public class PathFinding : MonoBehaviour {
        
        
        [SerializeField] private int samplesPerDimension = 4;
        [SerializeField] private float flyCostMultiplier = 1.25f;
        [SerializeField] private TerrainInfo terrainInfo;
        [SerializeField] private TextMeshProUGUI time;
        private List<PathNode> _path = new();
        private PathNode _start;
        private PathNode _end;
        private Stopwatch _timer;
        private readonly List<PathNode> _openNodes = new();
        private readonly List<PathNode> _closedNodes = new();

        private PathGrid _pathGrid;
        private Coroutine _currentCoroutine;


        private void Start() {
            _timer = new Stopwatch();
        }


        private void OnValidate() {
            samplesPerDimension =  Mathf.Clamp(Mathf.ClosestPowerOfTwo(samplesPerDimension), 2, int.MaxValue);
        }
    
        
        public void RandomPathSearchEvent() {
            _timer = Stopwatch.StartNew();
            RandomizePath();
            if (_currentCoroutine != null) {
                StopCoroutine(_currentCoroutine);
            }
            _currentCoroutine = StartCoroutine(SetColorToPathCoroutine());
        }

        public List<PathNode> BirdBehaviorHeightAvoidingAStarPathSearch(PathNode start, PathNode end) {
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
            while (_openNodes.Count > 0)
            {
                var currentNode = GetLowestCostNode();
                if (currentNode == end) {
                
                    return GetFinalPath(currentNode);
                }

                _openNodes.Remove(currentNode);
                _closedNodes.Add(currentNode);
                var neighbours = _pathGrid.GetAllNeighbours(currentNode.Index);
                foreach (var neighbour in neighbours) {
                    if (neighbour.isWall) {
                        _closedNodes.Add(neighbour);
                        continue;
                    }
                

                    if (!_closedNodes.Contains(neighbour)) {
                        var isDiagonal = currentNode.Index.x != neighbour.Index.x &&
                                         currentNode.Index.y != neighbour.Index.y;

                        var gCost = currentNode.GCost + (isDiagonal ? 1.4f : 1);
                        var flyCost = (terrainHeights[neighbour.Index.x, neighbour.Index.y] -
                                       terrainHeights[currentNode.Index.x, currentNode.Index.y]) * flyCostMultiplier; 

                        if (gCost + flyCost < neighbour.GCost) {
                            neighbour.GCost = gCost;
                            neighbour.FlyCost = flyCost;
                            neighbour.HCost = Vector2.Distance(neighbour.Index, end.Index);
                            neighbour.SourceNode = currentNode;
                        }
                        if (!_openNodes.Contains(neighbour)) {
                            _openNodes.Add(neighbour);
                        }
                    }
                }
            }
            
            return null;
        
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
            _pathGrid = new PathGrid(samplesPerDimension, samplesPerDimension);
            _start = _pathGrid.GetRandomNode();
            _end = _pathGrid.GetRandomNode();
            
            while (_start == _end) {
                _end = _pathGrid.GetRandomNode();
            }
        }
    
        IEnumerator SetColorToPathCoroutine() {
            _path = BirdBehaviorHeightAvoidingAStarPathSearch(_start, _end);
            foreach (var node in _path) {
                terrainInfo.SetColor(node.Index, Color.black);
                yield return new WaitForSeconds(0.05f);
            }
            
            terrainInfo.SetColor(_start.Index, Color.white);
            terrainInfo.SetColor(_end.Index, Color.white);
        
        }
    }
}
