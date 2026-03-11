using System.Collections.Generic;
using UnityEngine;

namespace Building
{
    public class PlacementTriggerManager : MonoBehaviour
    {
        [SerializeField] private float triggerSize = 0.5f;
        
        private BuildingGrid grid;
        private Dictionary<Vector3Int, Dictionary<BoardOrientation, GameObject>> triggers = new();
        private int placementLayer;
        private int boardLayer;

        private void Start()
        {
            grid = BuildingGrid.Instance;
            if (grid == null)
            {
                Debug.LogError("BuildingGrid not found!");
                return;
            }

            placementLayer = LayerMask.NameToLayer("PlacementTrigger");
            boardLayer = LayerMask.NameToLayer("Board");
            
            grid.OnBoardAdded += HandleBoardAdded;
            grid.OnBoardRemoved += HandleBoardRemoved;
        }

        private void OnDestroy()
        {
            if (grid != null)
            {
                grid.OnBoardAdded -= HandleBoardAdded;
                grid.OnBoardRemoved -= HandleBoardRemoved;
            }
        }

        private void HandleBoardAdded(Vector3Int pos, BoardOrientation orient)
        {
            RemoveTrigger(pos, orient);
            GenerateTriggersForBoard(pos, orient);
        }

        private void HandleBoardRemoved(Vector3Int pos, BoardOrientation orient)
        {
            CleanupOrphanedTriggers();
        }

        public void GenerateTriggersForBoard(Vector3Int pos, BoardOrientation orient)
        {
            var adjacentPositions = GetAdjacentTriggerPositions(pos, orient);
            
            foreach (var (triggerPos, triggerOrient) in adjacentPositions)
            {
                if (!grid.HasBoard(triggerPos, triggerOrient))
                {
                    CreateTrigger(triggerPos, triggerOrient);
                }
            }
        }

        private List<(Vector3Int pos, BoardOrientation orient)> GetAdjacentTriggerPositions(
            Vector3Int pos, BoardOrientation orient)
        {
            var positions = new List<(Vector3Int, BoardOrientation)>();
            
            for (int i = 0; i < 3; i++)
            {
                BoardOrientation checkOrient = (BoardOrientation)(1 << i);
                
                Vector3Int[] directions = {
                    Vector3Int.right, Vector3Int.left,
                    Vector3Int.up, Vector3Int.down,
                    Vector3Int.forward, Vector3Int.back
                };
                
                foreach (var dir in directions)
                {
                    positions.Add((pos + dir, checkOrient));
                }
            }
            
            return positions;
        }

        private void CreateTrigger(Vector3Int pos, BoardOrientation orient)
        {
            if (HasTrigger(pos, orient)) return;

            GameObject trigger = new GameObject($"Trigger_{pos}_{orient}");
            trigger.transform.SetParent(transform);
            trigger.layer = placementLayer;
            
            Vector3 worldPos = GridToWorld(pos, orient);
            trigger.transform.position = worldPos;
            
            BoxCollider collider = trigger.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = Vector3.one * triggerSize;
            
            TriggerInfo info = trigger.AddComponent<TriggerInfo>();
            info.GridPosition = pos;
            info.Orientation = orient;
            
            if (!triggers.ContainsKey(pos))
                triggers[pos] = new Dictionary<BoardOrientation, GameObject>();
            triggers[pos][orient] = trigger;
        }

        public void RemoveTrigger(Vector3Int pos, BoardOrientation orient)
        {
            if (!triggers.TryGetValue(pos, out var orientDict)) return;
            if (!orientDict.TryGetValue(orient, out var trigger)) return;
            
            if (trigger != null) Destroy(trigger);
            orientDict.Remove(orient);
            
            if (orientDict.Count == 0)
                triggers.Remove(pos);
        }

        private bool HasTrigger(Vector3Int pos, BoardOrientation orient)
        {
            return triggers.TryGetValue(pos, out var orientDict) && orientDict.ContainsKey(orient);
        }

        public void CleanupOrphanedTriggers()
        {
            var toRemove = new List<(Vector3Int, BoardOrientation)>();
            
            foreach (var kvp in triggers)
            {
                foreach (var orientKvp in kvp.Value)
                {
                    if (!HasAnyAdjacentBoard(kvp.Key, orientKvp.Key))
                    {
                        toRemove.Add((kvp.Key, orientKvp.Key));
                    }
                }
            }
            
            foreach (var (pos, orient) in toRemove)
            {
                RemoveTrigger(pos, orient);
            }
        }

        private bool HasAnyAdjacentBoard(Vector3Int pos, BoardOrientation orient)
        {
            var adjacentPositions = GetAdjacentTriggerPositions(pos, orient);
            
            foreach (var (adjPos, adjOrient) in adjacentPositions)
            {
                if (grid.HasBoard(adjPos, adjOrient))
                    return true;
            }
            
            return false;
        }

        private Vector3 GridToWorld(Vector3Int gridPos, BoardOrientation orient)
        {
            const float CELL_SIZE = 4f;
            Vector3 basePos = (Vector3)gridPos * CELL_SIZE;
            
            return orient switch
            {
                BoardOrientation.X => basePos + new Vector3(2f, 2f, 0f),
                BoardOrientation.Y => basePos + new Vector3(0f, 2f, 2f),
                BoardOrientation.Z => basePos + new Vector3(2f, 0f, 2f),
                _ => basePos
            };
        }

        public void ClearAllTriggers()
        {
            foreach (var orientDict in triggers.Values)
            {
                foreach (var trigger in orientDict.Values)
                {
                    if (trigger != null) Destroy(trigger);
                }
            }
            triggers.Clear();
        }
    }
}
