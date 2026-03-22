using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Building
{
    public class BuildingGrid : MonoBehaviour
    {
        public static BuildingGrid Instance { get; private set; }

        public event Action<IOccupant> OnOccupantAdded;
        public event Action<IOccupant> OnOccupantRemoved;

        private Dictionary<Vector3Int, List<IOccupant>> _cellOccupants = new();
        private HashSet<IOccupant> _occupantRegistry = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public bool IsEmpty() => _occupantRegistry.Count == 0;

        public bool HasOccupant(Vector3Int cell, OccupantType type)
        {
            if (!_cellOccupants.TryGetValue(cell, out var list)) return false;
            foreach (var occ in list)
            {
                if (occ.Type == type) return true;
            }
            return false;
        }

        public bool HasBoard(Vector3Int cell, BoardOrientation orient)
        {
            if (!_cellOccupants.TryGetValue(cell, out var list)) return false;
            foreach (var occ in list)
            {
                if (occ is BoardOccupant board && board.Orientation == orient)
                    return true;
            }
            return false;
        }

        public bool HasAnyOccupant(Vector3Int cell)
        {
            return _cellOccupants.ContainsKey(cell) && _cellOccupants[cell].Count > 0;
        }

        public IReadOnlyList<IOccupant> GetOccupants(Vector3Int cell)
        {
            if (_cellOccupants.TryGetValue(cell, out var list))
                return list;
            return Array.Empty<IOccupant>();
        }

        public Vector3Int? GetBoardAnchor(Vector3Int cell, BoardOrientation orient)
        {
            if (!_cellOccupants.TryGetValue(cell, out var list)) return null;
            foreach (var occ in list)
            {
                if (occ is BoardOccupant board && board.Orientation == orient)
                    return board.Anchor;
            }
            return null;
        }

        public BoardOccupant GetBoardOccupant(Vector3Int anchor, BoardOrientation orient)
        {
            if (!_cellOccupants.TryGetValue(anchor, out var list)) return null;
            foreach (var occ in list)
            {
                if (occ is BoardOccupant board &&
                    board.Orientation == orient &&
                    board.Anchor == anchor)
                    return board;
            }
            return null;
        }

        public GameObject GetBoard(Vector3Int anchor, BoardOrientation orient)
        {
            return GetBoardOccupant(anchor, orient)?.GameObject;
        }

        public bool AddBoard(Vector3Int anchor, BoardOrientation orient, GameObject boardObj)
        {
            var occupant = new BoardOccupant(anchor, orient, boardObj);
            var cells = occupant.GetOccupiedCells();

            bool isFull = orient == (BoardOrientation.X | BoardOrientation.Y | BoardOrientation.Z);

            foreach (var cell in cells)
            {
                if (!_cellOccupants.TryGetValue(cell, out var list)) continue;
                foreach (var existing in list)
                {
                    if (existing is not BoardOccupant existingBoard) continue;

                    bool existingIsFull = existingBoard.Orientation ==
                        (BoardOrientation.X | BoardOrientation.Y | BoardOrientation.Z);

                    if (existingIsFull && !isFull) return false;
                    if (isFull) return false;
                    if (existingBoard.Orientation == orient) return false;
                }
            }

            foreach (var cell in cells)
            {
                if (!_cellOccupants.ContainsKey(cell))
                    _cellOccupants[cell] = new List<IOccupant>();
                _cellOccupants[cell].Add(occupant);
            }
            _occupantRegistry.Add(occupant);

            OnOccupantAdded?.Invoke(occupant);
            return true;
        }

        public bool RemoveBoard(Vector3Int anchor, BoardOrientation orient)
        {
            var occupant = GetBoardOccupant(anchor, orient);
            if (occupant == null) return false;

            return RemoveOccupant(occupant);
        }

        public bool AddSmallBlock(Vector3Int cell, Quaternion rotation, GameObject blockObj)
        {
            if (HasOccupant(cell, OccupantType.SmallBlock))
                return false;

            var occupant = new SmallBlockOccupant(cell, rotation, blockObj);

            if (!_cellOccupants.ContainsKey(cell))
                _cellOccupants[cell] = new List<IOccupant>();
            _cellOccupants[cell].Add(occupant);
            _occupantRegistry.Add(occupant);

            OnOccupantAdded?.Invoke(occupant);
            return true;
        }

        public bool RemoveOccupant(IOccupant occupant)
        {
            if (!_occupantRegistry.Remove(occupant))
                return false;

            var cells = occupant.GetOccupiedCells();
            foreach (var cell in cells)
            {
                if (_cellOccupants.TryGetValue(cell, out var list))
                {
                    list.Remove(occupant);
                    if (list.Count == 0)
                        _cellOccupants.Remove(cell);
                }
            }

            if (occupant.GameObject != null)
                Destroy(occupant.GameObject);

            OnOccupantRemoved?.Invoke(occupant);
            return true;
        }

        public bool HasAnyNeighbor(Vector3Int anchor, BoardOrientation orient)
        {
            var neighbors = BoardAdjacency.GetNeighbors(orient);
            foreach (var n in neighbors)
            {
                Vector3Int neighborAnchor = anchor + n.Offset;
                if (HasBoard(neighborAnchor, n.Orientation))
                {
                    var foundAnchor = GetBoardAnchor(neighborAnchor, n.Orientation);
                    if (foundAnchor.HasValue && foundAnchor.Value == neighborAnchor)
                        return true;
                }
            }
            return false;
        }

        public bool HasAnyFaceNeighbor(Vector3Int cell)
        {
            Vector3Int[] offsets =
            {
                Vector3Int.right, Vector3Int.left,
                Vector3Int.up, Vector3Int.down,
                new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
            };

            foreach (var offset in offsets)
            {
                if (HasAnyOccupant(cell + offset))
                    return true;
            }
            return false;
        }

        public IOccupant FindOccupantByGameObject(Vector3Int cell, GameObject obj)
        {
            if (!_cellOccupants.TryGetValue(cell, out var list)) return null;
            foreach (var occ in list)
            {
                if (occ.GameObject == obj)
                    return occ;
            }
            return null;
        }
    }
}
