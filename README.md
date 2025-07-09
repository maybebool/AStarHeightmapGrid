## A* Algorithm for flying objects 
(under development) 

A modified A* algorithm that introduces a FlyCost variable to produce more realistic flight behavior for flying creatures around large obstacles such as mountains or buildings. The key is that the path avoids a certain height and flies around  
The key was to reproduce the Algorithm used in Horizon Zero Dawn: https://www.guerrilla-games.com/read/putting-the-ai-back-into-air


### How to Play
- Press Enter to create new paths
- Use the UI to clean the paths or rotate the camera
![A* Algorithm Demo](Recordings/Gif/output.gif)
![Example](https://github.com/maybebool/AStarHeightmapGrid/blob/main/Recordings/Image%20Sequence_003_0005.jpg)


### Core Pathfinding Math
-----------------------------------------------------------------------------

| Variable      | Meaning                                                |
|-------------|--------------------------------------------------------|
| **GCost(n)**| Accumulated travel cost from the start node            |
| **HCost(n)**| Straight‑line heuristic to the target node             |
| **FlyCost(n)**| Extra cost for climbing (or bonus for descending)    |

The priority key used to pick the next node is

```csharp
FCost(n) = GCost(n) + FlyCost(n) + HCost(n)
```

### 1  Movement Cost — *GCost*

For a step from the current node **p** to a neighbour **n**
```csharp
var gCost = currentNode.GCost + (isDiagonal ? 1.4f : 1f);
```

*1.4* is an integer‑friendly approximation of √2 for diagonal moves.


### 2  Height Penalty/Bonus — *FlyCost*

The terrain is pre‑sampled into `terrainHeights[x,y]`.  
Moving from `p = (x_p, y_p)` to `n = (x_n, y_n)`:

```csharp
var flyCost = (terrainHeights[neighbour.Index.x, neighbour.Index.y] -
terrainHeights[currentNode.Index.x, currentNode.Index.y]) * flyCostMultiplier;
```

* `deltaHeight > 0` → uphill ⇒ **cost increases**  
* `deltaHeight < 0` → downhill ⇒ **cost decreases**
Set `flyCostMultiplier` to tune how much slope matters.


### 3  Heuristic — *HCost*

Euclidean distance on the grid:

```csharp
neighbour.HCost = Vector2.Distance(neighbour.Index, end.Index);
```

### 4  Main Loop

1. Start: set `GCost(start)=0`, `HCost`, `FlyCost=0`; add **start** to open list.  
2. Pop the open node with the lowest **FCost**.  
3. For each neighbour, apply the update rule above.  
4. Move the processed node to the closed list; repeat until the target node is popped  
   (success) or the open list is empty (no path).

---

### Why a Fly Cost?

- **Realistic flight/drone paths** – avoid steep climbs that waste energy.  
- **Hiking simulations** – favour routes with gentle ascents/descents.  
- **Games** – create natural‑looking paths in hilly terrain without expensive
  3‑D checks.
