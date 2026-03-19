using System.Collections.Generic;
using UnityEngine;

namespace Building
{
    public class PlacementTriggerManager : MonoBehaviour
    {
        private const float CELL_SIZE = 4f;

        private BuildingGrid _grid;
        private Dictionary<Vector3Int, Dictionary<BoardOrientation, GameObject>> _triggers = new();
        private int _placementLayer;

        private void Start()
        {
            _grid = BuildingGrid.Instance;
            if (_grid == null)
            {
                Debug.LogError("[PlacementTriggerManager] BuildingGrid.Instance not found.");
                return;
            }

            _placementLayer = LayerMask.NameToLayer("PlacementTrigger");
            if (_placementLayer == -1)
            {
                Debug.LogError("[PlacementTriggerManager] Layer 'PlacementTrigger' not found. Run Tools > Building System > Setup Layers.");
                return;
            }

            _grid.OnBoardAdded += HandleBoardAdded;
            _grid.OnBoardRemoved += HandleBoardRemoved;
        }

        private void OnDestroy()
        {
            if (_grid != null)
            {
                _grid.OnBoardAdded -= HandleBoardAdded;
                _grid.OnBoardRemoved -= HandleBoardRemoved;
            }
        }

        private void HandleBoardAdded(Vector3Int pos, BoardOrientation orient)
        {
            bool isFull = orient == (BoardOrientation.X | BoardOrientation.Y | BoardOrientation.Z);

            if (isFull)
            {
                // Remove all triggers at this position
                RemoveTrigger(pos, BoardOrientation.X);
                RemoveTrigger(pos, BoardOrientation.Y);
                RemoveTrigger(pos, BoardOrientation.Z);
            }
            else
            {
                RemoveTrigger(pos, orient);
            }

            // Generate triggers at each valid empty neighbor
            var neighbors = BoardAdjacency.GetNeighbors(orient);
            foreach (var n in neighbors)
            {
                Vector3Int neighborPos = pos + n.Offset;
                BoardOrientation neighborOrient = n.Orientation;

                if (!_grid.HasBoard(neighborPos, neighborOrient))
                {
                    CreateTrigger(neighborPos, neighborOrient);
                }
            }
        }

        private void HandleBoardRemoved(Vector3Int pos, BoardOrientation orient)
        {
            bool isFull = orient == (BoardOrientation.X | BoardOrientation.Y | BoardOrientation.Z);

            if (isFull)
            {
                // Check each individual orientation for neighbors and create triggers
                foreach (var o in new[] { BoardOrientation.X, BoardOrientation.Y, BoardOrientation.Z })
                {
                    if (_grid.HasAnyNeighbor(pos, o))
                    {
                        CreateTrigger(pos, o);
                    }
                }
            }
            else
            {
                if (_grid.HasAnyNeighbor(pos, orient))
                {
                    CreateTrigger(pos, orient);
                }
            }

            CleanupOrphanedTriggers();
        }

        private void CreateTrigger(Vector3Int pos, BoardOrientation orient)
        {
            if (HasTrigger(pos, orient)) return;

            GameObject trigger = new GameObject($"Trigger_{pos}_{orient}");
            trigger.transform.SetParent(transform);
            trigger.layer = _placementLayer;

            trigger.transform.position = GridToWorld(pos, orient);
            trigger.transform.rotation = GetBoardRotation(orient);

            BoxCollider collider = trigger.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(CELL_SIZE, 0.1f, CELL_SIZE);

            TriggerInfo info = trigger.AddComponent<TriggerInfo>();
            info.GridPosition = pos;
            info.Orientation = orient;

            if (!_triggers.ContainsKey(pos))
                _triggers[pos] = new Dictionary<BoardOrientation, GameObject>();
            _triggers[pos][orient] = trigger;
        }

        private void RemoveTrigger(Vector3Int pos, BoardOrientation orient)
        {
            if (!_triggers.TryGetValue(pos, out var orientDict)) return;
            if (!orientDict.TryGetValue(orient, out var trigger)) return;

            if (trigger != null) Destroy(trigger);
            orientDict.Remove(orient);

            if (orientDict.Count == 0)
                _triggers.Remove(pos);
        }

        private bool HasTrigger(Vector3Int pos, BoardOrientation orient)
        {
            return _triggers.TryGetValue(pos, out var orientDict) && orientDict.ContainsKey(orient);
        }

        private void CleanupOrphanedTriggers()
        {
            var toRemove = new List<(Vector3Int, BoardOrientation)>();

            foreach (var kvp in _triggers)
            {
                foreach (var orientKvp in kvp.Value)
                {
                    if (!_grid.HasAnyNeighbor(kvp.Key, orientKvp.Key))
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

        public void ClearAllTriggers()
        {
            foreach (var orientDict in _triggers.Values)
            {
                foreach (var trigger in orientDict.Values)
                {
                    if (trigger != null) Destroy(trigger);
                }
            }
            _triggers.Clear();
        }

        public static Vector3 GridToWorld(Vector3Int gridPos, BoardOrientation orient)
        {
            Vector3 basePos = (Vector3)gridPos * CELL_SIZE;

            return orient switch
            {
                BoardOrientation.X => basePos + new Vector3(CELL_SIZE / 2f, CELL_SIZE / 2f, 0f),
                BoardOrientation.Y => basePos + new Vector3(0f, CELL_SIZE / 2f, CELL_SIZE / 2f),
                BoardOrientation.Z => basePos + new Vector3(CELL_SIZE / 2f, 0f, CELL_SIZE / 2f),
                _ => basePos + new Vector3(CELL_SIZE / 2f, CELL_SIZE / 2f, CELL_SIZE / 2f)
            };
        }

        public static Quaternion GetBoardRotation(BoardOrientation orient)
        {
            // Board prefab & trigger collider are flat in local XZ (thin along Y).
            // Rotate so the flat face matches the board's plane:
            return orient switch
            {
                BoardOrientation.X => Quaternion.Euler(90f, 0f, 0f),             // XZ → XY plane (vertical wall facing Z)
                BoardOrientation.Y => Quaternion.Euler(0f, 0f, 90f),             // XZ → YZ plane (vertical wall facing X)
                BoardOrientation.Z => Quaternion.identity,                        // XZ stays XZ (horizontal floor)
                _ => Quaternion.identity
            };
        }
    }
}
