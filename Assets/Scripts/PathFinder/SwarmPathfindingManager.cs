using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using PathFinderDOTS.Services;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace PathFinder {
    public class SwarmPathfindingManager : MonoBehaviour {
        [Header("DOTS Integration")] [SerializeField]
        private bool useDOTSPathfinding = true;

        private DOTSPathfindingService _dotsPathfindingService;


        [Header("Bird Swarm Settings")] [SerializeField]
        private GameObject birdPrefab;

        [SerializeField] private int numberOfBirds = 5;
        [SerializeField] private float spawnRadius = 5f;
        [SerializeField] private float birdHeightOffset = 10f;

        [Header("Swarm Behavior")] [SerializeField]
        private float pathFollowRadius = 3f;

        [SerializeField] private float heightVariation = 2f;
        [SerializeField] private float staggerDelay = 0.5f;

        [Header("Pathfinding Settings")] [SerializeField]
        private int samplesPerDimension = 4;

        [SerializeField] private float flyCostMultiplier = 1.25f;
        [SerializeField] private TerrainInfo terrainInfo;

        [Header("UI Elements")] [SerializeField]
        private TextMeshProUGUI timeText;

        [SerializeField] private TextMeshProUGUI instructionText;

        [Header("Visualization Settings")] [SerializeField]
        private GameObject pathLineRendererPrefab;

        [SerializeField] private float pathLineHeight = 15f;
        [SerializeField] private Material pathMaterial;
        [SerializeField] private Color pathColor = Color.blue;
        [SerializeField] private AnimationCurve pathLineWidthCurve;

        // Services and components
        private PathfindingService _pathfindingService;
        private List<BirdAgent> _birdAgents = new List<BirdAgent>();
        private Camera _mainCamera;
        private Stopwatch _timer;
        private LineRenderer _pathLineRenderer; // Actual instance used at runtime

        // State management
        private Vector2Int _startGridPos;
        private Vector2Int _endGridPos;
        private bool _hasStartPoint = false;
        private bool _isPathActive = false;

        // Visualization tracking
        private GameObject _startMarker;
        private GameObject _endMarker;
        private List<Vector2Int> _coloredCells = new List<Vector2Int>();
        private Coroutine _pathVisualizationCoroutine;

        private void Awake() {
            ValidateSettings();
            InitializeServices();
        }

        private void Start() {
            _mainCamera = Camera.main;
            _timer = new Stopwatch();

            // Don't spawn birds immediately - wait for first path
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
                Debug.LogError(
                    "PathLineRendererPrefab is required! Please assign a GameObject with LineRenderer component.");
            }
        }

        private void InitializeServices() {
            if (terrainInfo != null) {
                if (useDOTSPathfinding) {
                    // Create DOTS pathfinding service
                    _dotsPathfindingService = gameObject.AddComponent<DOTSPathfindingService>();
                    // Initialize it with the same settings
                    _dotsPathfindingService.Initialize(
                        samplesPerDimension,
                        terrainInfo.CellSize,
                        flyCostMultiplier,
                        terrainInfo
                    );
                }
                else {
                    // Use original pathfinding
                    _pathfindingService = new PathfindingService(
                        samplesPerDimension,
                        flyCostMultiplier,
                        terrainInfo,
                        terrainInfo.transform.position
                    );
                }
            }
        }

        // Add these helper methods to handle both DOTS and regular pathfinding:
        private Vector2Int WorldToGridPositionInternal(Vector3 worldPos) {
            if (useDOTSPathfinding && _dotsPathfindingService != null) {
                return _dotsPathfindingService.WorldToGridPosition(worldPos);
            }
            else if (_pathfindingService != null) {
                return _pathfindingService.WorldToGridPosition(worldPos);
            }

            return Vector2Int.zero;
        }

        private bool IsValidGridPositionInternal(Vector2Int pos) {
            if (useDOTSPathfinding && _dotsPathfindingService != null) {
                return _dotsPathfindingService.IsValidGridPosition(pos);
            }
            else if (_pathfindingService != null) {
                return _pathfindingService.IsValidGridPosition(pos);
            }

            return false;
        }

        private Vector3 GridToWorldPositionInternal(Vector2Int gridPos, float yOffset = 0) {
            if (useDOTSPathfinding && _dotsPathfindingService != null) {
                return _dotsPathfindingService.GridToWorldPosition(gridPos, yOffset);
            }
            else if (_pathfindingService != null) {
                return _pathfindingService.GridToWorldPosition(gridPos, yOffset);
            }

            return Vector3.zero;
        }

        private void SpawnBirds(Vector3 spawnPosition) {
            for (int i = 0; i < numberOfBirds; i++) {
                GameObject birdObj = Instantiate(birdPrefab);
                birdObj.name = $"Bird_{i:00}";

                BirdAgent agent = birdObj.GetComponent<BirdAgent>();
                if (!agent) {
                    agent = birdObj.AddComponent<BirdAgent>();
                }

                // Initialize swarm behavior for natural spreading
                agent.InitializeSwarmBehavior(pathFollowRadius, heightVariation);

                _birdAgents.Add(agent);

                // Position birds in a random spread around the spawn position
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float radius = Random.Range(0f, spawnRadius);
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Random.Range(-heightVariation, heightVariation),
                    Mathf.Sin(angle) * radius
                );

                birdObj.transform.position = spawnPosition + offset;
            }
        }

        private void SetupLineRenderer() {
            // Instantiate the required LineRenderer prefab
            GameObject lineObj = Instantiate(pathLineRendererPrefab);
            lineObj.name = "PathLineRenderer_Instance";
            _pathLineRenderer = lineObj.GetComponent<LineRenderer>();

            if (_pathLineRenderer == null) {
                Debug.LogError(
                    "PathLineRendererPrefab doesn't have a LineRenderer component! Please assign a prefab with LineRenderer.");
                return;
            }

            // Apply settings to the LineRenderer instance if they are configured
            if (pathMaterial != null) {
                _pathLineRenderer.material = pathMaterial;
            }

            _pathLineRenderer.startColor = pathColor;
            _pathLineRenderer.endColor = pathColor;

            // Only override width settings if not already configured in prefab
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

            Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
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

            // Color the start cell
            terrainInfo.SetColor(gridPos, Color.green);
            _coloredCells.Add(gridPos);

            UpdateInstructionText("Click on the terrain to set END point");
        }

        private void SetEndPointAndCalculatePath(Vector2Int gridPos) {
            // If path is active, use average bird position as new start
            if (_isPathActive) {
                Vector3 avgPos = GetAverageBirdPosition();
                Vector2Int newStartPos = WorldToGridPositionInternal(avgPos);

                if (IsValidGridPositionInternal(newStartPos)) {
                    _startGridPos = newStartPos;
                }
            }

            _endGridPos = gridPos;

            // Clear previous visualization
            ClearVisualization();

            // Color start and end cells
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

            // Calculate path using the appropriate service
            List<PathNode> nodePath = null;

            if (useDOTSPathfinding && _dotsPathfindingService != null) {
                // Use DOTS pathfinding
                nodePath = _dotsPathfindingService.CalculatePath(_startGridPos, _endGridPos);
            }
            else if (_pathfindingService != null) {
                // Use original pathfinding
                nodePath = _pathfindingService.CalculatePath(_startGridPos, _endGridPos);
            }

            _timer.Stop();

            if (timeText != null) {
                timeText.text = $"Path found in: {_timer.ElapsedMilliseconds} ms";
            }

            if (nodePath == null || nodePath.Count == 0) {
                UpdateInstructionText("No path found! Try different points.");
                yield break;
            }

            // Spawn birds at start position if this is the first path
            if (!_isPathActive) {
                // Get the world position of the start point with height
                float[,] heights = terrainInfo.SampleHeights(samplesPerDimension);
                Vector3 startWorldPos = GridToWorldPositionInternal(_startGridPos,
                    heights[_startGridPos.x, _startGridPos.y] + birdHeightOffset);
                SpawnBirds(startWorldPos);
            }

            _isPathActive = true;

            // Convert to world positions for visualization and bird movement
            List<Vector3> worldPath = null;

            if (useDOTSPathfinding && _dotsPathfindingService != null) {
                worldPath = _dotsPathfindingService.ConvertPathToWorldPositions(nodePath, birdHeightOffset);
            }
            else if (_pathfindingService != null) {
                worldPath = _pathfindingService.ConvertPathToWorldPositions(nodePath, birdHeightOffset);
            }

            if (worldPath == null) {
                UpdateInstructionText("Failed to convert path to world positions.");
                yield break;
            }

            // Visualize with line renderer
            VisualizePath(worldPath, pathLineHeight);

            // Color the terrain cells along the path
            foreach (var node in nodePath) {
                terrainInfo.SetColor(node.Index, pathColor);
                if (!_coloredCells.Contains(node.Index)) {
                    _coloredCells.Add(node.Index);
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
                // Adjust height for line renderer visibility
                Vector3 linePos = worldPath[i];
                linePos.y = worldPath[i].y - birdHeightOffset + heightOffset;
                _pathLineRenderer.SetPosition(i, linePos);
            }
        }

        private void SendBirdsAlongPath(List<Vector3> worldPath) {
            // Birds only exist after first path is created
            if (_birdAgents.Count == 0) return;

            // Determine if birds should teleport to start or continue from current position
            bool shouldTeleport = false; // Never teleport since birds spawn at start

            foreach (var bird in _birdAgents) {
                // Add random delay for more natural, staggered movement
                float delay = Random.Range(0f, staggerDelay);
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

            Vector3 sum = Vector3.zero;
            int activeCount = 0;

            foreach (var bird in _birdAgents) {
                if (bird != null) {
                    sum += bird.CurrentPosition;
                    activeCount++;
                }
            }

            return activeCount > 0 ? sum / activeCount : terrainInfo.transform.position;
        }

        private void ClearVisualization() {
            // Reset terrain colors - with extra safety checks
            if (terrainInfo != null && Application.isPlaying) {
                try {
                    foreach (var cell in _coloredCells) {
                        terrainInfo.ResetCellColor(cell);
                    }
                }
                catch (MissingReferenceException) {
                    // Objects were destroyed - ignore and continue
                }
            }

            _coloredCells.Clear();

            // Clear line renderer
            if (_pathLineRenderer != null) {
                try {
                    _pathLineRenderer.positionCount = 0;
                }
                catch (MissingReferenceException) {
                    // LineRenderer was destroyed - ignore
                }
            }


            // Destroy markers safely
            SafeDestroyMarker(ref _startMarker);
            SafeDestroyMarker(ref _endMarker);
        }

        private void SafeDestroyMarker(ref GameObject marker) {
            if (marker != null) {
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
            if (instructionText != null) {
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

        private void OnDestroy() {
            // Don't perform cleanup when exiting play mode - Unity handles it
            // This prevents MissingReferenceExceptions when objects are destroyed in unpredictable order
        }

        private void OnApplicationQuit() {
            // Called before OnDestroy when quitting the application
            // No cleanup needed here either - Unity handles it
        }
    }
}