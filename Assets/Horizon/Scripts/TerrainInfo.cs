// using System;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
//
// [RequireComponent(typeof(Terrain))]
// public class TerrainInfo : MonoBehaviour
// {
//     [SerializeField] int samplesPerDimension = 4;
//     Terrain terrain;
//
//     [SerializeField] Transform cubePrefab;
//     public Transform[,] cubes;
//
//     // Start is called before the first frame update
//     private void Awake()
//     {
//         terrain = GetComponent<Terrain>();
//     }
//
//     void Start()
//     {
//         float[,] data = SampleHeights(samplesPerDimension);
//         DebugLogHeights(data);
//         DrawCubes(data, samplesPerDimension);
//     }
//
//     internal void SetAllColor(Color color)
//     {
//         if(cubes != null)
//         {
//             for (int i = 0; i < cubes.GetLength(0); i++)
//             {
//                 for (int j = 0; j < cubes.GetLength(1); j++)
//                 {
//                     SetColor(i, j, color);
//                 }
//             }
//         }
//     }
//
//     internal void SetColor(int x, int y, Color color)
//     {
//         cubes[x,y].GetComponent<MeshRenderer>().material.color = color;
//     }
//     internal void SetColor(Vector2Int index, Color color)
//     {
//         SetColor(index.x, index.y, color);
//     }
//
//     public float[,] SampleHeights(int samplesPerDimension, bool inWorldUnits = true)
//     {
//         float[,] samples = new float[samplesPerDimension, samplesPerDimension];
//         int resolution = terrain.terrainData.heightmapResolution;
//         int length = resolution / samplesPerDimension;
//         float[,] heights = terrain.terrainData.GetHeights(0, 0, resolution, resolution);
//         for (int i = 0; i < samplesPerDimension; i++) // Samples Y
//         {
//             for (int j = 0; j < samplesPerDimension; j++) // Samples X
//             {
//                 float maxHeight = 0;
//                 for (int k = 0; k < length; k++) // heights Y
//                 {
//                     for (int l = 0; l < length; l++) // heights X
//                     {
//                         int x = j * length + l;
//                         int y = i * length + k;
//                         if (heights[x, y] > maxHeight) maxHeight = heights[x, y];
//                     }
//                 }
//                 samples[j, i] = inWorldUnits ? maxHeight * terrain.terrainData.heightmapScale.y : maxHeight;
//             }
//         }
//         return samples;
//     }
//
//     public void DebugLogHeights(float[,] heights)
//     {
//         string output = "<b>Heights:</b>";
//         for (int i = 0; i < heights.GetLength(1); i++)
//         {
//             output += "\n";
//             for (int j = 0; j < heights.GetLength(0); j++)
//             {
//                 output += heights[j, i].ToString("n2") + " ";
//             }
//         }
//         Debug.Log(output);
//     }
//
//     public void DrawCubes(float[,] heights, int samplesPerDimension )
//     {
//         cubes = new Transform[samplesPerDimension, samplesPerDimension];
//         float width = terrain.terrainData.size.x;
//         float length = width / samplesPerDimension;
//         float halfLength = length / 2f;
//         for (int i = 0; i < samplesPerDimension; i++) // z
//         {
//             for (int j = 0; j < samplesPerDimension; j++) // x
//             {
//                 float x = j * length;
//                 float z = i * length;
//                 float height = heights[i, j];
//                 cubes[j, i] = Instantiate(cubePrefab, new Vector3(x + halfLength, height / 2f, z + halfLength), Quaternion.identity);
//                 cubes[j, i].localScale = new Vector3(length, height, length);
//             }
//         }
//     }
// }
