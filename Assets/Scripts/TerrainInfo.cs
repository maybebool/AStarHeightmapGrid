using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Terrain))]
public class TerrainInfo : MonoBehaviour
{
    [SerializeField] private Terrain terrain;
    [SerializeField] private int samplesPerSide = 40;
    [SerializeField] private Material heatMap;
    [SerializeField] private Material path;
    public int SamplesPerSide { get => samplesPerSide; }

    private GameObject[,] _spawnedCubes;

    private void OnValidate() {
        if (terrain == null) {
            terrain = GetComponent<Terrain>();
        }

        samplesPerSide = Mathf.ClosestPowerOfTwo(samplesPerSide);
    }

    private void Start() {
        SpawnDebugCubes(SampleHeights(samplesPerSide));
    }

    public float[,] SampleHeights(int samplesPerDimension, bool inWorldUnits = true)
    {
      var terrainResolution = terrain.terrainData.heightmapResolution;
      var sampleStepSize = terrainResolution / samplesPerDimension;
      var heights = terrain.terrainData.GetHeights(0, 0, terrainResolution, terrainResolution);
      var returnHeights = new float[samplesPerDimension, samplesPerDimension];
      
      for (int currentSampleY = 0; currentSampleY < samplesPerDimension; currentSampleY++) {
          for (int currentSampleX = 0; currentSampleX < samplesPerDimension; currentSampleX++) {
              float maxHeight = 0;
              for (int y = 0; y <= sampleStepSize; y++) {
                  for (int x = 0; x <= sampleStepSize; x++) {
                      // current origin of the sample
                      // + x / y
                      // get height of this position, if its higher than previously saved value store it.
                      var yPos = currentSampleY * sampleStepSize + y;
                      var xPos = currentSampleX * sampleStepSize + x;
                      
                      if (heights[xPos, yPos] > maxHeight) {
                          maxHeight = heights[xPos,yPos];
                      }
                  }
              }
              returnHeights[currentSampleY, currentSampleX] = inWorldUnits ? maxHeight * terrain.terrainData.heightmapScale.y : maxHeight;
          }
      }
      return returnHeights;
    }

    public void SpawnDebugCubes(float[,] heights) 
    {
        _spawnedCubes = new GameObject[heights.GetLength(0), heights.GetLength(1)];
        for (int y = 0; y < heights.GetLength(1); y++) {
            for (int x = 0; x < heights.GetLength(0); x++) {
                // terrain position in Ws + width or length / heights, length + width / heigths.length / 2
                var sampleLength = terrain.terrainData.size.x / heights.GetLength(0);
                var sampleHalfLength = sampleLength / 2f;
                var cubeSpawnPosition = terrain.transform.position + new Vector3(
                    x * sampleLength + sampleHalfLength, heights[x, y] / 2f, y * sampleLength + sampleHalfLength);
                
                var voxelCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                voxelCube.transform.position = cubeSpawnPosition;
                voxelCube.transform.localScale = new Vector3(sampleLength, heights[x, y], sampleLength);
                _spawnedCubes[x, y] = voxelCube;
            }
        }
    }


    public void SetHeatmap() {
        foreach (var cube in _spawnedCubes) {
            cube.GetComponent<MeshRenderer>().material = heatMap;
        }
    }

    public void SetColor(Vector2Int index, Color color) {
        _spawnedCubes[index.x, index.y].GetComponent<MeshRenderer>().material = path;
        _spawnedCubes[index.x, index.y].GetComponent<MeshRenderer>().material.color = color;
        
    }
}
