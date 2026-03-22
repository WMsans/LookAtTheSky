using System.Collections.Generic;
using UnityEngine;

namespace Building
{
    public class PlacementTriggerManager : MonoBehaviour
    {
        private const BoardOrientation FULL = BoardOrientation.X | BoardOrientation.Y | BoardOrientation.Z;

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

        private void HandleBoardAdded(Vector3Int anchor, BoardOrientation orient)
        {
            bool isFull = orient == FULL;

            if (isFull)
            {
                // Remove all triggers at this anchor
                RemoveTrigger(anchor, BoardOrientation.X);
                RemoveTrigger(anchor, BoardOrientation.Y);
                RemoveTrigger(anchor, BoardOrientation.Z);
            }
            else
            {
                RemoveTrigger(anchor, orient);
            }

            // Generate triggers at each valid empty neighbor anchor
            var neighbors = BoardAdjacency.GetNeighbors(orient);
            foreach (var n in neighbors)
            {
                Vector3Int neighborAnchor = anchor + n.Offset;
                BoardOrientation neighborOrient = n.Orientation;

                if (!_grid.HasBoard(neighborAnchor, neighborOrient))
                {
                    CreateTrigger(neighborAnchor, neighborOrient);
                }
            }
        }

        private void HandleBoardRemoved(Vector3Int anchor, BoardOrientation orient)
        {
            bool isFull = orient == FULL;

            if (isFull)
            {
                foreach (var o in new[] { BoardOrientation.X, BoardOrientation.Y, BoardOrientation.Z })
                {
                    if (_grid.HasAnyNeighbor(anchor, o))
                    {
                        CreateTrigger(anchor, o);
                    }
                }
            }
            else
            {
                if (_grid.HasAnyNeighbor(anchor, orient))
                {
                    CreateTrigger(anchor, orient);
                }
            }

            CleanupOrphanedTriggers();
        }

        private void CreateTrigger(Vector3Int anchor, BoardOrientation orient)
        {
            if (HasTrigger(anchor, orient)) return;

            GameObject trigger = new GameObject($"Trigger_{anchor}_{orient}");
            trigger.transform.SetParent(transform);
            trigger.layer = _placementLayer;

            trigger.transform.position = GridToWorld(anchor, orient);
            trigger.transform.rotation = GetBoardRotation(orient);

            BoxCollider collider = trigger.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            // Trigger collider matches board world size (4x0.1x4), not cell size
            collider.size = new Vector3(GridConstants.BOARD_WORLD_SIZE, 0.1f, GridConstants.BOARD_WORLD_SIZE);

            TriggerInfo info = trigger.AddComponent<TriggerInfo>();
            info.GridPosition = anchor;
            info.Orientation = orient;

            if (!_triggers.ContainsKey(anchor))
                _triggers[anchor] = new Dictionary<BoardOrientation, GameObject>();
            _triggers[anchor][orient] = trigger;
        }

        private void RemoveTrigger(Vector3Int anchor, BoardOrientation orient)
        {
            if (!_triggers.TryGetValue(anchor, out var orientDict)) return;
            if (!orientDict.TryGetValue(orient, out var trigger)) return;

            if (trigger != null) Destroy(trigger);
            orientDict.Remove(orient);

            if (orientDict.Count == 0)
                _triggers.Remove(anchor);
        }

        private bool HasTrigger(Vector3Int anchor, BoardOrientation orient)
        {
            return _triggers.TryGetValue(anchor, out var orientDict) && orientDict.ContainsKey(orient);
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

            foreach (var (anchor, orient) in toRemove)
            {
                RemoveTrigger(anchor, orient);
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

        /// <summary>
        /// Convert an anchor grid position to world-space center of the board.
        /// Board spans BOARD_SPAN cells (2) in each planar axis, so center is offset
        /// by half the board world size (2 units) along each planar axis.
        /// </summary>
        public static Vector3 GridToWorld(Vector3Int anchor, BoardOrientation orient)
        {
            float cs = GridConstants.CELL_SIZE;
            float half = GridConstants.BOARD_WORLD_SIZE / 2f; // 2 units

            Vector3 basePos = new Vector3(anchor.x * cs, anchor.y * cs, anchor.z * cs);

            return orient switch
            {
                // X-board (XY plane): center in X and Y, at z face
                BoardOrientation.X => basePos + new Vector3(half, half, 0f),
                // Y-board (YZ plane): center in Y and Z, at x face
                BoardOrientation.Y => basePos + new Vector3(0f, half, half),
                // Z-board (XZ plane): center in X and Z, at y face
                BoardOrientation.Z => basePos + new Vector3(half, 0f, half),
                // FullCell: center in all axes
                _ => basePos + new Vector3(half, half, half)
            };
        }

        public static Quaternion GetBoardRotation(BoardOrientation orient)
        {
            return orient switch
            {
                BoardOrientation.X => Quaternion.Euler(90f, 0f, 0f),
                BoardOrientation.Y => Quaternion.Euler(0f, 0f, 90f),
                BoardOrientation.Z => Quaternion.identity,
                _ => Quaternion.identity
            };
        }
    }
}
