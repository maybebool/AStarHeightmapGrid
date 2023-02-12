using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GridLayoutGroup))]
public class TerrainInfoUI : MonoBehaviour
{
    GridLayoutGroup gridLayout;
    [SerializeField] PatchInfoPanel patchInfoPanelPrefab;
    PatchInfoPanel[,] panels;
    private void Awake()
    {
        gridLayout = GetComponent<GridLayoutGroup>();
    }
    
    public void ShowPatchInfo(PathNode[,] pathNodes)
    {
        int sideLength = pathNodes.GetLength(1);
        gridLayout.cellSize = transform.parent.GetComponent<RectTransform>().sizeDelta / sideLength;
        if (panels == null)
        {
            panels = new PatchInfoPanel[sideLength, sideLength];
            for (int i = 0; i < sideLength; i++)
            {
                for (int j = 0; j < sideLength; j++)
                {
                    panels[j, i] = Instantiate(patchInfoPanelPrefab, transform);
                }
            }
        }
        else if (panels.GetLength(1) > sideLength || panels.GetLength(1) < sideLength)
        {
            PatchInfoPanel[,] newPanels = new PatchInfoPanel[sideLength, sideLength];
            for (int i = 0; i < sideLength; i++)
            {
                for (int j = 0; j < sideLength; j++)
                {
                    Destroy(panels[j, i]);
                    newPanels[j, i] = Instantiate(patchInfoPanelPrefab, transform);
                }
            }
            panels = newPanels;
        }

        for (int i = 0; i < sideLength; i++)
        {
            for (int j = 0; j < sideLength; j++)
            {
                panels[j, i].UpdateCost(pathNodes[j, i].GCost, pathNodes[j, i].FCost, pathNodes[j, i].HCost, pathNodes[j, i].FlyCost);
            }
        }
    }
}
