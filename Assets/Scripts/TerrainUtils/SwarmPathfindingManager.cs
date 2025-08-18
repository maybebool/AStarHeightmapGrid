using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using PathFinderDOTS.Services;
using TerrainUtils.Pathfinding;
using TMPro;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TerrainUtils {
    public class SwarmPathfindingManager : MonoBehaviour {
        
        public enum PathfindingModeType {
            MouseClick,
            TargetFollow
        }

        [Header("Pathfinding Mode")]
        [SerializeField] private PathfindingModeType currentModeType = PathfindingModeType.MouseClick;
        [SerializeField] private Transform targetToFollow; // For target follow mode
        
        [Header("Target Follow Settings")]
        [SerializeField] private float minRecalculationInterval = 0.5f;
        [SerializeField] private float targetMoveThreshold = 2f;
        [SerializeField] private float swarmMoveThreshold = 3f;
        [SerializeField] private bool usePredictiveTargeting = true;
        [SerializeField] private float predictionTime = 1f;

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
        [SerializeField] private TextMeshProUGUI modeText;

        [Header("Visualization Settings")] 
        [SerializeField] private GameObject pathLineRendererPrefab;
        [SerializeField] private float pathLineHeight = 15f;
        [SerializeField] private Material pathMaterial;
        [SerializeField] private Color pathColor = Color.blue;
        [SerializeField] private AnimationCurve pathLineWidthCurve;
        [SerializeField] private bool showDebugVisualization = false;

        // Core services
        private DOTSPathfindingService _dotsPathfindingService;
        
        // Pathfinding modes
        private IPathfindingMode _currentMode;
        private MouseClickPathfindingMode _mouseClickMode;
        private TargetFollowPathfindingMode _targetFollowMode;
        private PathfindingModeContext _modeContext;
        
        // Swarm management
        private List<BirdAgent> _birdAgents = new();
        private Camera _mainCamera;
        private Stopwatch _timer;
        private LineRenderer _pathLineRenderer;
        
        // Path state
        private bool _isPathActive = false;
        private PathRequest _lastPathRequest;
        private Coroutine _pathVisualizationCoroutine;
        private List<Vector2Int> _pathColoredCells = new();

        private void Awake() {
            ValidateSettings();
            InitializeServices();
        }

        private void Start() {
            _mainCamera = Camera.main;
            _timer = new Stopwatch();

            SetupLineRenderer();
            
            // Initialize pathfinding modes after camera is available
            InitializePathfindingModes();
            
            // Activate initial mode
            SetPathfindingMode(currentModeType);
        }

        private void Update() {
            // Update current pathfinding mode
            _currentMode?.UpdateMode();
            
            // Update mode display
            UpdateModeDisplay();
            
            // Handle mode switching input (example: Tab key)
            if (Input.GetKeyDown(KeyCode.Tab)) {
                TogglePathfindingMode();
            }
        }

        private void OnValidate() {
            samplesPerDimension = Mathf.Clamp(Mathf.ClosestPowerOfTwo(samplesPerDimension), 2, 512);
            numberOfBirds = Mathf.Clamp(numberOfBirds, 1, 20);
        }

        private void OnDrawGizmos() {
            if (showDebugVisualization && _currentMode != null) {
                _currentMode.DrawDebugVisualization();
            }
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
                _dotsPathfindingService = gameObject.AddComponent<DOTSPathfindingService>();
                _dotsPathfindingService.Initialize(
                    samplesPerDimension,
                    terrainInfo.CellSize,
                    flyCostMultiplier,
                    terrainInfo
                );
            }
        }

        private void InitializePathfindingModes() {
            // Ensure camera is available
            if (_mainCamera == null) {
                _mainCamera = Camera.main;
                if (_mainCamera == null) {
                    Debug.LogError("No main camera found! Pathfinding modes require a camera.");
                    return;
                }
            }
            
            // Create mode context
            _modeContext = new PathfindingModeContext {
                MainCamera = _mainCamera,
                TerrainInfo = terrainInfo,
                PathfindingService = _dotsPathfindingService,
                SwarmCenterTransform = transform,
                BirdHeightOffset = birdHeightOffset,
                UpdateInstructionText = UpdateInstructionText,
                GetAverageBirdPosition = GetAverageBirdPosition,
                IsPathActive = () => _isPathActive
            };
            
            // Initialize modes
            _mouseClickMode = new MouseClickPathfindingMode();
            _mouseClickMode.Initialize(_modeContext);
            _mouseClickMode.OnPathRequested += HandlePathRequest;
            _mouseClickMode.OnClearPath += ClearCurrentPath;
            
            // Initialize target follow mode with settings
            var targetFollowSettings = new TargetFollowPathfindingMode.RecalculationSettings {
                MinRecalculationInterval = minRecalculationInterval,
                TargetMoveThreshold = targetMoveThreshold,
                SwarmMoveThreshold = swarmMoveThreshold,
                UsePredictiveTargeting = usePredictiveTargeting,
                PredictionTime = predictionTime,
                UseDistanceBasedThrottling = true,
                MaxThrottleDistance = 50f,
                MaxThrottleMultiplier = 3f
            };
            
            _targetFollowMode = new TargetFollowPathfindingMode(targetFollowSettings);
            _targetFollowMode.Initialize(_modeContext);
            _targetFollowMode.OnPathRequested += HandlePathRequest;
            _targetFollowMode.OnClearPath += ClearCurrentPath;
            
            // Set initial target if provided
            if (targetToFollow != null) {
                _targetFollowMode.SetTarget(targetToFollow);
            }
        }

        public void SetPathfindingMode(PathfindingModeType modeType) {
            // Check if modes are initialized
            if (_mouseClickMode == null || _targetFollowMode == null) {
                Debug.LogWarning("Pathfinding modes not initialized yet. Attempting to initialize...");
                InitializePathfindingModes();
                
                // If still not initialized, abort
                if (_mouseClickMode == null || _targetFollowMode == null) {
                    Debug.LogError("Failed to initialize pathfinding modes!");
                    return;
                }
            }
            
            // Deactivate current mode
            _currentMode?.OnDeactivate();
            
            // Switch to new mode
            currentModeType = modeType;
            
            switch (modeType) {
                case PathfindingModeType.MouseClick:
                    _currentMode = _mouseClickMode;
                    break;
                case PathfindingModeType.TargetFollow:
                    _currentMode = _targetFollowMode;
                    // Update target if changed in inspector
                    if (targetToFollow != null && _targetFollowMode.GetTarget() != targetToFollow) {
                        _targetFollowMode.SetTarget(targetToFollow);
                    }
                    break;
            }
            
            // Activate new mode
            _currentMode?.OnActivate();
            
            Debug.Log($"Switched to {_currentMode?.ModeName} pathfinding mode");
        }

        public void TogglePathfindingMode() {
            var nextMode = currentModeType == PathfindingModeType.MouseClick 
                ? PathfindingModeType.TargetFollow 
                : PathfindingModeType.MouseClick;
            
            SetPathfindingMode(nextMode);
        }

        public void SetFollowTarget(Transform target) {
            targetToFollow = target;
            if (_currentMode == _targetFollowMode) {
                _targetFollowMode.SetTarget(target);
            }
        }

        private void HandlePathRequest(PathRequest request) {
            _lastPathRequest = request;
            
            // Clear previous visualization
            ClearPathVisualization();
            
            // Calculate and visualize path
            if (_pathVisualizationCoroutine != null) {
                StopCoroutine(_pathVisualizationCoroutine);
            }
            
            _pathVisualizationCoroutine = StartCoroutine(CalculateAndVisualizePath(request));
        }

        private IEnumerator CalculateAndVisualizePath(PathRequest request) {
            _timer.Restart();
            
            // Calculate path using DOTS service
            List<Vector3> worldPath = _dotsPathfindingService.CalculatePath(
                request.StartGridPos, 
                request.EndGridPos, 
                request.HeightOffset
            );
            
            _timer.Stop();

            // Update timing display
            if (timeText != null) {
                timeText.text = $"Path found in: {_timer.ElapsedMilliseconds} ms";
            }

            if (worldPath == null || worldPath.Count == 0) {
                UpdateInstructionText("No path found! Try different points.");
                yield break;
            }
            
            // Spawn birds if first path
            if (!_isPathActive) {
                var startWorldPos = worldPath[0];
                SpawnBirds(startWorldPos);
            }

            _isPathActive = true;
            
            // Visualize path line
            VisualizePath(worldPath, pathLineHeight);
            
            // Animate path cells (optional, can be disabled for performance)
            if (currentModeType == PathfindingModeType.MouseClick) {
                foreach (var worldPos in worldPath) {
                    var gridPos = _dotsPathfindingService.WorldToGridPosition(worldPos);
                    terrainInfo.SetColor(new Vector2Int(gridPos.x, gridPos.y), pathColor);
                    _pathColoredCells.Add(new Vector2Int(gridPos.x, gridPos.y));
                    yield return new WaitForSeconds(0.02f);
                }
            }

            // Send birds along the path
            SendBirdsAlongPath(worldPath);
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
                } else {
                    _pathLineRenderer.startWidth = 2f;
                    _pathLineRenderer.endWidth = 0.5f;
                }
            }

            _pathLineRenderer.positionCount = 0;
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
            
            var shouldTeleport = !_isPathActive;
    
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

        private void ClearCurrentPath() {
            ClearPathVisualization();
            _isPathActive = false;
        }

        private void ClearPathVisualization() {
            // Clear colored cells
            if (terrainInfo && Application.isPlaying) {
                foreach (var cell in _pathColoredCells) {
                    try {
                        terrainInfo.ResetCellColor(cell);
                    } catch (MissingReferenceException) {
                        // Handle destroyed objects
                    }
                }
            }
            _pathColoredCells.Clear();

            // Clear path line
            if (_pathLineRenderer) {
                _pathLineRenderer.positionCount = 0;
            }
        }

        private void UpdateInstructionText(string text) {
            if (instructionText) {
                instructionText.text = text;
            }
        }

        private void UpdateModeDisplay() {
            if (modeText != null && _currentMode != null) {
                modeText.text = $"Mode: {_currentMode.ModeName} | {_currentMode.GetStatusInfo()}";
            }
        }

        public void ResetPathfinding() {
            // Deactivate current mode
            _currentMode?.OnDeactivate();
            
            _isPathActive = false;

            ClearPathVisualization();
            
            // Destroy all birds
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

            // Reactivate current mode
            _currentMode?.OnActivate();
        }

        private void OnDestroy() {
            _mouseClickMode?.Cleanup();
            _targetFollowMode?.Cleanup();
        }
    }
}