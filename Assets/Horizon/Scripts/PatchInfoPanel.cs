using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PatchInfoPanel : MonoBehaviour
{
    [SerializeField] Text gcost;
    [SerializeField] Text fcost;
    [SerializeField] Text hcost;
    [SerializeField] Text flycost;
    
    public void UpdateCost(float gcost, float fcost, float hcost, float flycost)
    {
        this.gcost.text = gcost >= 30000 ? "X" : gcost.ToString("n2");
        this.fcost.text = fcost.ToString("n2");
        this.hcost.text = hcost >= 30000 ? "X" : hcost.ToString("n2");
        this.flycost.text = flycost.ToString("n2");
    }
}
