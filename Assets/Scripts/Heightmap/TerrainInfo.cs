using UnityEngine;
using UnityEngine.UI;


namespace Heightmap {
    [RequireComponent(typeof(Terrain))]
    public class TerrainInfo : MonoBehaviour
    {
        [SerializeField] private Terrain terrain;
        [SerializeField] private int samplesPerSide = 64;
        [SerializeField] private Material heatMap;
        [SerializeField] private Material path;
        [SerializeField] private Button cleanButton;
        private GameObject[,] _spawnedCubes;
        public float CellSize => terrain.terrainData.size.x / samplesPerSide;
        
        private void OnEnable() {
            Utils.BindButtonPlusParamsAction(cleanButton, CleanButtonClick);
        }

        private void OnDisable() {
            cleanButton.onClick.RemoveAllListeners();
        }

        private void CleanButtonClick() {
            SetHeatmap();
        }

        private void OnValidate() {
            if (terrain == null) {
                terrain = GetComponent<Terrain>();
            }
            samplesPerSide = Mathf.ClosestPowerOfTwo(samplesPerSide);
        }

        private void Start() {
            SpawnDebugCubes(SampleHeights(samplesPerSide));
            SetHeatmap();
        }

        public float[,] SampleHeights(int samplesPerDimension, bool inWorldUnits = true) {
            var resolution = terrain.terrainData.heightmapResolution;
            var stepSize = resolution / samplesPerDimension;
            var heights = terrain.terrainData.GetHeights(0, 0, resolution, resolution);
            var sampledHeights = new float[samplesPerDimension, samplesPerDimension];

            for (int sampleY = 0; sampleY < samplesPerDimension; sampleY++)
            {
                for (int sampleX = 0; sampleX < samplesPerDimension; sampleX++)
                {
                    float maxHeight = FindMaxHeight(heights, sampleY, sampleX, stepSize);
                    sampledHeights[sampleY, sampleX] = inWorldUnits 
                        ? maxHeight * terrain.terrainData.heightmapScale.y 
                        : maxHeight;
                }
            }
            return sampledHeights;
        }

        private float FindMaxHeight(float[,] heights, int sampleY, int sampleX, int stepSize) {
            var maxVal = 0f;
            for (int y = 0; y <= stepSize; y++) {
                for (int x = 0; x <= stepSize; x++) {
                    var posY = sampleY * stepSize + y;
                    var posX = sampleX * stepSize + x;
                    var currentHeight = heights[posX, posY];
                    if (currentHeight > maxVal) {
                        maxVal = currentHeight;
                    }
                }
            }
            return maxVal;
        }


        private void SpawnDebugCubes(float[,] heightMap) {
            var cellSize = terrain.terrainData.size.x / heightMap.GetLength(0);
            var halfCellSize = cellSize / 2f;
            _spawnedCubes = new GameObject[heightMap.GetLength(0), heightMap.GetLength(1)];

            for (int row = 0; row < heightMap.GetLength(1); row++) {
                for (int col = 0; col < heightMap.GetLength(0); col++) {
                    var cubePosition = terrain.transform.position + new Vector3(
                        col * cellSize + halfCellSize,
                        heightMap[col, row] / 2f,
                        row * cellSize + halfCellSize
                    );
            
                    var debugCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    debugCube.transform.position = cubePosition;
                    debugCube.transform.localScale = new Vector3(cellSize, heightMap[col, row], cellSize);

                    _spawnedCubes[col, row] = debugCube;
                }
            }
        }


        private void SetHeatmap() {
            foreach (var cube in _spawnedCubes) {
                cube.GetComponent<MeshRenderer>().material = heatMap;
            }
        }
    
        public void SetColor(Vector2Int index, Color color) {
            var mr = _spawnedCubes[index.x, index.y].GetComponent<MeshRenderer>();
            mr.material = path;
            mr.material.color = color;
        }
    }
}