using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using PathFinderDOTS.Services;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace TerrainUtils {
    public class SwarmPathfindingManager : MonoBehaviour {

        [Header("Bird Swarm Settings")] 
        [SerializeField] private GameObject birdPrefab;
        [SerializeField] private int numberOfBirds = 5;
        [SerializeField] private float spawnRadius = 5f;
        [SerializeField] private float birdHeightOffset = 10f;

        [Header("Swarm Behavior")] 
        [SerializeField] private float pathFollowRadius = 3f;
        [SerializeField] private float heightVariation = 2f;
        [SerializeField] private float staggerDelay = 0.5f;

        [Header("Pathfinding Settings")] 
        [SerializeField] private int samplesPerDimension = 4;
        [SerializeField] private float flyCostMultiplier = 1.25f;
        [SerializeField] private TerrainInfo terrainInfo;

        [Header("UI Elements")] 
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private TextMeshProUGUI instructionText;

        [Header("Visualization Settings")] 
        [SerializeField] private GameObject pathLineRendererPrefab;
        [SerializeField] private float pathLineHeight = 15f;
        [SerializeField] private Material pathMaterial;
        [SerializeField] private Color pathColor = Color.blue;
        [SerializeField] private AnimationCurve pathLineWidthCurve;

        private DOTSPathfindingService _dotsPathfindingService;
        
        private List<BirdAgent> _birdAgents = new();
        private Camera _mainCamera;
        private Stopwatch _timer;
        private LineRenderer _pathLineRenderer;
        
        private Vector2Int _startGridPos;
        private Vector2Int _endGridPos;
        private bool _hasStartPoint = false;
        private bool _isPathActive = false;
        
        private GameObject _startMarker;
        private GameObject _endMarker;
        private List<Vector2Int> _coloredCells = new();
        private Coroutine _pathVisualizationCoroutine;

        private void Awake() {
            ValidateSettings();
            InitializeServices();
        }

        private void Start() {
            _mainCamera = Camera.main;
            _timer = new Stopwatch();

            SetupLineRenderer();
            UpdateInstructionText("Click on the terrain to set START point");
        }

        private void Update() {
            HandleMouseInput();
        }

        private void OnValidate() {
            samplesPerDimension = Mathf.Clamp(Mathf.ClosestPowerOfTwo(samplesPerDimension), 2, 512);
            numberOfBirds = Mathf.Clamp(numberOfBirds, 1, 20);
        }

        private void ValidateSettings() {
            if (terrainInfo == null) {
                Debug.LogError("TerrainInfo is not assigned to SwarmPathfindingManager!");
            }

            if (birdPrefab == null) {
                Debug.LogError("Bird prefab is not assigned to SwarmPathfindingManager!");
            }

            if (pathLineRendererPrefab == null) {
                Debug.LogError("PathLineRendererPrefab is required! Please assign a GameObject with LineRenderer component.");
            }
        }

        private void InitializeServices() {
            if (terrainInfo != null) {
                // Only use DOTS pathfinding
                _dotsPathfindingService = gameObject.AddComponent<DOTSPathfindingService>();
                _dotsPathfindingService.Initialize(
                    samplesPerDimension,
                    terrainInfo.CellSize,
                    flyCostMultiplier,
                    terrainInfo
                );
            }
        }

        private Vector2Int WorldToGridPositionInternal(Vector3 worldPos) {
            if (_dotsPathfindingService) {
                return _dotsPathfindingService.WorldToGridPosition(worldPos);
            }
            return Vector2Int.zero;
        }

        private bool IsValidGridPositionInternal(Vector2Int pos) {
            if (_dotsPathfindingService) {
                return _dotsPathfindingService.IsValidGridPosition(pos);
            }
            return false;
        }

        private Vector3 GridToWorldPositionInternal(Vector2Int gridPos, float yOffset = 0) {
            if (_dotsPathfindingService != null) {
                return _dotsPathfindingService.GridToWorldPosition(gridPos, yOffset);
            }
            return Vector3.zero;
        }

        private void SpawnBirds(Vector3 spawnPosition) {
            for (int i = 0; i < numberOfBirds; i++) {
                var birdObj = Instantiate(birdPrefab);
                birdObj.name = $"Bird_{i:00}";

                var agent = birdObj.GetComponent<BirdAgent>();
                if (!agent) {
                    agent = birdObj.AddComponent<BirdAgent>();
                }
                
                agent.InitializeSwarmBehavior(pathFollowRadius, heightVariation);
                _birdAgents.Add(agent);
                var angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                var radius = Random.Range(0f, spawnRadius);
                var offset = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Random.Range(-heightVariation, heightVariation),
                    Mathf.Sin(angle) * radius
                );

                birdObj.transform.position = spawnPosition + offset;
            }
        }

        private void SetupLineRenderer() {
            var lineObj = Instantiate(pathLineRendererPrefab);
            lineObj.name = "PathLineRenderer_Instance";
            _pathLineRenderer = lineObj.GetComponent<LineRenderer>();

            if (_pathLineRenderer == null) {
                Debug.LogError("PathLineRendererPrefab doesn't have a LineRenderer component!");
                return;
            }

            if (pathMaterial != null) {
                _pathLineRenderer.material = pathMaterial;
            }

            _pathLineRenderer.startColor = pathColor;
            _pathLineRenderer.endColor = pathColor;

            if (_pathLineRenderer.widthCurve == null || _pathLineRenderer.widthCurve.keys.Length == 0) {
                if (pathLineWidthCurve != null && pathLineWidthCurve.keys.Length > 0) {
                    _pathLineRenderer.widthCurve = pathLineWidthCurve;
                }
                else {
                    _pathLineRenderer.startWidth = 2f;
                    _pathLineRenderer.endWidth = 0.5f;
                }
            }

            _pathLineRenderer.positionCount = 0;
        }

        private void HandleMouseInput() {
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;

            var ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit)) {
                Vector2Int gridPos = WorldToGridPositionInternal(hit.point);

                if (IsValidGridPositionInternal(gridPos)) {
                    if (!_hasStartPoint) {
                        SetStartPoint(gridPos);
                    }
                    else {
                        SetEndPointAndCalculatePath(gridPos);
                    }
                }
            }
        }

        private void SetStartPoint(Vector2Int gridPos) {
            ClearVisualization();

            _startGridPos = gridPos;
            _hasStartPoint = true;
            terrainInfo.SetColor(gridPos, Color.green);
            _coloredCells.Add(gridPos);

            UpdateInstructionText("Click on the terrain to set END point");
        }

        private void SetEndPointAndCalculatePath(Vector2Int gridPos) {
            if (_isPathActive) {
                var avgPos = GetAverageBirdPosition();
                var newStartPos = WorldToGridPositionInternal(avgPos);

                if (IsValidGridPositionInternal(newStartPos)) {
                    _startGridPos = newStartPos;
                }
            }

            _endGridPos = gridPos;
            ClearVisualization();

            terrainInfo.SetColor(_startGridPos, Color.green);
            terrainInfo.SetColor(_endGridPos, Color.red);
            _coloredCells.Add(_startGridPos);
            _coloredCells.Add(_endGridPos);

            // Calculate and visualize path
            if (_pathVisualizationCoroutine != null) {
                StopCoroutine(_pathVisualizationCoroutine);
            }

            _pathVisualizationCoroutine = StartCoroutine(CalculateAndVisualizePath());

            UpdateInstructionText("Birds are following the path! Click anywhere to set a new destination.");
        }

        private IEnumerator CalculateAndVisualizePath() {
            _timer.Restart();
            List<Vector3> worldPath = null;

            if (_dotsPathfindingService != null) {
                worldPath = _dotsPathfindingService.CalculatePath(_startGridPos, _endGridPos, birdHeightOffset);
            }

            _timer.Stop();

            if (timeText != null) {
                timeText.text = $"Path found in: {_timer.ElapsedMilliseconds} ms";
            }

            if (worldPath == null || worldPath.Count == 0) {
                UpdateInstructionText("No path found! Try different points.");
                yield break;
            }
            
            if (!_isPathActive) {
                var startWorldPos = worldPath[0];
                SpawnBirds(startWorldPos);
            }

            _isPathActive = true;
            VisualizePath(worldPath, pathLineHeight);
            foreach (var worldPos in worldPath) {
                var gridPos = WorldToGridPositionInternal(worldPos);
                terrainInfo.SetColor(gridPos, pathColor);
                if (!_coloredCells.Contains(gridPos)) {
                    _coloredCells.Add(gridPos);
                }
                yield return new WaitForSeconds(0.02f);
            }

            // Send birds along the path
            SendBirdsAlongPath(worldPath);
        }

        private void VisualizePath(List<Vector3> worldPath, float heightOffset) {
            if (_pathLineRenderer == null || worldPath == null) return;

            _pathLineRenderer.positionCount = worldPath.Count;

            for (int i = 0; i < worldPath.Count; i++) {
                var linePos = worldPath[i];
                linePos.y = worldPath[i].y - birdHeightOffset + heightOffset;
                _pathLineRenderer.SetPosition(i, linePos);
            }
        }

        private void SendBirdsAlongPath(List<Vector3> worldPath) {
            if (_birdAgents.Count == 0) return;
            var shouldTeleport = false;
            foreach (var bird in _birdAgents) {
                var delay = Random.Range(0f, staggerDelay);
                StartCoroutine(DelayedBirdStart(bird, worldPath, delay, shouldTeleport));
            }
        }

        private IEnumerator DelayedBirdStart(BirdAgent bird, List<Vector3> path, float delay, bool teleportToStart) {
            yield return new WaitForSeconds(delay);
            bird.SetPath(path, teleportToStart);
        }

        private Vector3 GetAverageBirdPosition() {
            if (_birdAgents.Count == 0) {
                return terrainInfo.transform.position;
            }

            var sum = Vector3.zero;
            var activeCount = 0;

            foreach (var bird in _birdAgents) {
                if (bird) {
                    sum += bird.CurrentPosition;
                    activeCount++;
                }
            }

            return activeCount > 0 ? sum / activeCount : terrainInfo.transform.position;
        }

        private void ClearVisualization() {
            if (terrainInfo != null && Application.isPlaying) {
                try {
                    foreach (var cell in _coloredCells) {
                        terrainInfo.ResetCellColor(cell);
                    }
                }
                catch (MissingReferenceException) {
                }
            }

            _coloredCells.Clear();

            // Clear line renderer
            if (_pathLineRenderer) {
                try {
                    _pathLineRenderer.positionCount = 0;
                }
                catch (MissingReferenceException) {
                }
            }

            // Destroy markers safely
            SafeDestroyMarker(ref _startMarker);
            SafeDestroyMarker(ref _endMarker);
        }

        private void SafeDestroyMarker(ref GameObject marker) {
            if (marker) {
                try {
                    Destroy(marker);
                }
                catch (MissingReferenceException) {
                    // Already destroyed - ignore
                }

                marker = null;
            }
        }

        private void UpdateInstructionText(string text) {
            if (instructionText) {
                instructionText.text = text;
            }
        }

        /// <summary>
        /// Resets the entire pathfinding system to initial state.
        /// </summary>
        public void ResetPathfinding() {
            _hasStartPoint = false;
            _isPathActive = false;

            ClearVisualization();

            // Destroy all bird agents
            foreach (var bird in _birdAgents) {
                if (bird != null) {
                    Destroy(bird.gameObject);
                }
            }

            _birdAgents.Clear();

            if (_pathVisualizationCoroutine != null) {
                StopCoroutine(_pathVisualizationCoroutine);
                _pathVisualizationCoroutine = null;
            }

            UpdateInstructionText("Click on the terrain to set START point");
        }
    }
}