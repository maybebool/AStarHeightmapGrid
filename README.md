## A* Algorithm for flying objects 
(under development) 

A modified A* algorithm that introduces a FlyCost variable to produce more realistic flight behavior for flying creatures around large obstacles such as mountains or buildings. The key is that the path avoids a certain height and flies around  
The key was to reproduce the Algorithm used in Horizon Zero Dawn: https://www.guerrilla-games.com/read/putting-the-ai-back-into-air


### How to Play
- Press Enter to create new paths
- Use the UI to clean the paths or rotate the camera

![Example](https://github.com/maybebool/AStarHeightmapGrid/blob/main/Recordings/Image%20Sequence_003_0005.jpg)


### Core Pathfinding Math
-----------------------------------------------------------------------------

My A* implementation uses three cost components for each node \(n\):

1. **G‑Cost** (\(G(n)\))  
   The exact cost from the start node to \(n\).  
   - Moving orthogonally (up/down/left/right) adds **1.0**.  
   - Moving diagonally adds **\(\sqrt2 \approx 1.4\)**.  
   \[
     G(\text{neighbor}) = G(\text{current}) + 
       \begin{cases}
         1.0, & \text{if orthogonal} \\
         \sqrt2, & \text{if diagonal}
       \end{cases}
   \]

2. **Heuristic (H‑Cost)** (\(H(n)\))  
   An admissible estimate of the cost from \(n\) to the goal, using straight‑line distance (Euclidean):
   \[
     H(n) = \sqrt{(x_{n}-x_{\text{end}})^2 + (y_{n}-y_{\text{end}})^2}
   \]

3. **Fly Cost** (\(F_{\text{fly}}(n)\))  
   A penalty (or bonus) based on terrain elevation change between the current node and its neighbor:
   \[
     F_{\text{fly}}(n) 
       = \bigl(h(n) - h(\text{current})\bigr) \times k
   \]
   - \(h(n)\) = terrain height at node \(n\).  
   - \(k\) = your `flyCostMultiplier`.  
   - **Positive** if climbing (penalty), **negative** if descending (bonus).

---

### Total Cost & Node Selection

Each node’s **F‑Cost** is the sum of all three:
\[
  F(n) = G(n) + H(n) + F_{\text{fly}}(n)
\]
At each step, A* picks the open node with the lowest \(F\)-Cost:
```csharp
var currentNode = GetLowestCostNode(); // lowest G+H+Fly

