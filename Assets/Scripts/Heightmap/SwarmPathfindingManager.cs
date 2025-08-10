using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace Heightmap {
    /// <summary>
    /// Manages bird swarm pathfinding with interactive mouse controls.
    /// Uses PathfindingService for calculations and BirdAgents for movement.
    /// </summary>
    public class SwarmPathfindingManager : MonoBehaviour {
        [Header("Bird Swarm Settings")]
        [SerializeField] private GameObject birdPrefab;
        [SerializeField] private int numberOfBirds = 5;
        [SerializeField] private float spawnRadius = 5f;
        [SerializeField] private float birdHeightOffset = 10f;
        
        [Header("Pathfinding Settings")]
        [SerializeField] private int samplesPerDimension = 4;
        [SerializeField] private float flyCostMultiplier = 1.25f;
        [SerializeField] private TerrainInfo terrainInfo;
        
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private TextMeshProUGUI instructionText;
        
        [Header("Visualization Settings")]
        [SerializeField] private GameObject markerPrefab;
        [SerializeField] private float markerHeight = 27.0f;
        [SerializeField] private LineRenderer pathLineRenderer;
        [SerializeField] private float pathLineHeight = 15f;
        [SerializeField] private Material pathMaterial;
        [SerializeField] private Color pathColor = Color.blue;
        [SerializeField] private AnimationCurve pathLineWidthCurve;
        
        // Services and components
        private PathfindingService _pathfindingService;
        private List<BirdAgent> _birdAgents = new List<BirdAgent>();
        private Camera _mainCamera;
        private Stopwatch _timer;
        
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
            
            SpawnBirds();
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
                Debug.LogWarning("Bird prefab is not assigned. Birds will not spawn.");
            }
        }
        
        private void InitializeServices() {
            if (terrainInfo != null) {
                _pathfindingService = new PathfindingService(
                    samplesPerDimension, 
                    flyCostMultiplier, 
                    terrainInfo, 
                    terrainInfo.transform.position
                );
            }
        }
        
        private void SpawnBirds() {
            if (birdPrefab == null) return;
            
            for (int i = 0; i < numberOfBirds; i++) {
                GameObject birdObj = Instantiate(birdPrefab);
                birdObj.name = $"Bird_{i:00}";
                
                BirdAgent agent = birdObj.GetComponent<BirdAgent>();
                if (agent == null) {
                    agent = birdObj.AddComponent<BirdAgent>();
                }
                
                _birdAgents.Add(agent);
                
                // Position birds in a circle formation
                float angle = (360f / numberOfBirds) * i * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * spawnRadius,
                    birdHeightOffset,
                    Mathf.Sin(angle) * spawnRadius
                );
                
                Vector3 centerPos = terrainInfo.transform.position + 
                    new Vector3(terrainInfo.CellSize * samplesPerDimension / 2, 0, 
                               terrainInfo.CellSize * samplesPerDimension / 2);
                
                birdObj.transform.position = centerPos + offset;
            }
        }
        
        private void SetupLineRenderer() {
            if (pathLineRenderer == null) {
                GameObject lineObj = new GameObject("PathLineRenderer");
                pathLineRenderer = lineObj.AddComponent<LineRenderer>();
            }
            
            pathLineRenderer.material = pathMaterial ?? new Material(Shader.Find("Sprites/Default"));
            pathLineRenderer.startColor = pathColor;
            pathLineRenderer.endColor = pathColor;
            pathLineRenderer.startWidth = 2f;
            pathLineRenderer.endWidth = 0.5f;
            
            if (pathLineWidthCurve == null || pathLineWidthCurve.keys.Length == 0) {
                pathLineWidthCurve = AnimationCurve.Linear(0, 1, 1, 0.3f);
            }
            pathLineRenderer.widthCurve = pathLineWidthCurve;
            pathLineRenderer.positionCount = 0;
        }
        
        private void HandleMouseInput() {
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;
            
            Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit)) {
                Vector2Int gridPos = _pathfindingService.WorldToGridPosition(hit.point);
                
                if (_pathfindingService.IsValidGridPosition(gridPos)) {
                    if (!_hasStartPoint) {
                        SetStartPoint(gridPos);
                    } else {
                        SetEndPointAndCalculatePath(gridPos);
                    }
                }
            }
        }
        
        private void SetStartPoint(Vector2Int gridPos) {
            ClearVisualization();
            
            _startGridPos = gridPos;
            _hasStartPoint = true;
            
            // Create start marker
            Vector3 markerPos = _pathfindingService.GridToWorldPosition(gridPos, markerHeight);
            CreateMarker(ref _startMarker, markerPos, Color.black);
            
            // Color the start cell
            terrainInfo.SetColor(gridPos, Color.black);
            _coloredCells.Add(gridPos);
            
            UpdateInstructionText("Click on the terrain to set END point");
        }
        
        private void SetEndPointAndCalculatePath(Vector2Int gridPos) {
            // If path is active, use average bird position as new start
            if (_isPathActive) {
                Vector3 avgPos = GetAverageBirdPosition();
                Vector2Int newStartPos = _pathfindingService.WorldToGridPosition(avgPos);
                
                if (_pathfindingService.IsValidGridPosition(newStartPos)) {
                    _startGridPos = newStartPos;
                }
            }
            
            _endGridPos = gridPos;
            
            // Clear previous visualization
            ClearVisualization();
            
            // Create new markers
            Vector3 startMarkerPos = _pathfindingService.GridToWorldPosition(_startGridPos, markerHeight);
            Vector3 endMarkerPos = _pathfindingService.GridToWorldPosition(_endGridPos, markerHeight);
            CreateMarker(ref _startMarker, startMarkerPos, Color.green);
            CreateMarker(ref _endMarker, endMarkerPos, Color.red);
            
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
            
            // Calculate path using the service
            List<PathNode> nodePath = _pathfindingService.CalculatePath(_startGridPos, _endGridPos);
            
            _timer.Stop();
            
            if (timeText != null) {
                timeText.text = $"Path found in: {_timer.ElapsedMilliseconds} ms";
            }
            
            if (nodePath == null || nodePath.Count == 0) {
                UpdateInstructionText("No path found! Try different points.");
                yield break;
            }
            
            _isPathActive = true;
            
            // Convert to world positions for visualization and bird movement
            List<Vector3> worldPath = _pathfindingService.ConvertPathToWorldPositions(nodePath, birdHeightOffset);
            
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
            if (pathLineRenderer == null || worldPath == null) return;
            
            pathLineRenderer.positionCount = worldPath.Count;
            
            for (int i = 0; i < worldPath.Count; i++) {
                // Adjust height for line renderer visibility
                Vector3 linePos = worldPath[i];
                linePos.y = worldPath[i].y - birdHeightOffset + heightOffset;
                pathLineRenderer.SetPosition(i, linePos);
            }
        }
        
        private void SendBirdsAlongPath(List<Vector3> worldPath) {
            foreach (var bird in _birdAgents) {
                // Add slight random delay for natural movement
                float delay = Random.Range(0f, 0.5f);
                StartCoroutine(DelayedBirdStart(bird, worldPath, delay));
            }
        }
        
        private IEnumerator DelayedBirdStart(BirdAgent bird, List<Vector3> path, float delay) {
            yield return new WaitForSeconds(delay);
            bird.SetPath(path, true);
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
        
        private void CreateMarker(ref GameObject marker, Vector3 position, Color color) {
            if (marker != null) {
                Destroy(marker);
            }
            
            if (markerPrefab != null) {
                marker = Instantiate(markerPrefab, position, Quaternion.identity);
                Renderer renderer = marker.GetComponent<Renderer>();
                if (renderer != null) {
                    renderer.material.color = color;
                }
            } else {
                marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.transform.position = position;
                marker.transform.localScale = Vector3.one * 2f;
                marker.GetComponent<Renderer>().material.color = color;
            }
        }
        
        private void ClearVisualization() {
            // Reset terrain colors - with extra safety checks
            if (terrainInfo != null && Application.isPlaying) {
                try {
                    foreach (var cell in _coloredCells) {
                        terrainInfo.ResetCellColor(cell);
                    }
                } catch (MissingReferenceException) {
                    // Objects were destroyed - ignore and continue
                }
            }
            _coloredCells.Clear();
            
            // Clear line renderer
            if (pathLineRenderer != null) {
                try {
                    pathLineRenderer.positionCount = 0;
                } catch (MissingReferenceException) {
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
                } catch (MissingReferenceException) {
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
            
            foreach (var bird in _birdAgents) {
                if (bird != null) {
                    bird.StopMovement();
                }
            }
            
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