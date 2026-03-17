using System;
using System.Collections.Generic;
using UnityEngine;

namespace Building
{
    public class BuildingGrid : MonoBehaviour
    {
        public static BuildingGrid Instance { get; private set; }

        private const BoardOrientation FULL = BoardOrientation.X | BoardOrientation.Y | BoardOrientation.Z;

        public event Action<Vector3Int, BoardOrientation> OnBoardAdded;
        public event Action<Vector3Int, BoardOrientation> OnBoardRemoved;

        private Dictionary<Vector3Int, BoardOrientation> _grid = new();
        private Dictionary<Vector3Int, Dictionary<BoardOrientation, GameObject>> _boardRegistry = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public bool IsEmpty() => _grid.Count == 0;

        public bool HasBoard(Vector3Int pos, BoardOrientation orient)
        {
            return _grid.TryGetValue(pos, out var flags) && (flags & orient) != 0;
        }

        public bool AddBoard(Vector3Int pos, BoardOrientation orient, GameObject boardObj)
        {
            // Check if this exact orientation is already occupied
            if (HasBoard(pos, orient)) return false;

            // FullCell conflict checks
            if (_grid.TryGetValue(pos, out var existingFlags))
            {
                bool placingFull = orient == FULL;
                bool existingHasFull = existingFlags == FULL;

                // Can't place a panel where a FullCell exists
                if (existingHasFull && !placingFull) return false;
                // Can't place a FullCell where any panel exists
                if (placingFull && existingFlags != BoardOrientation.None) return false;
            }

            if (_grid.ContainsKey(pos))
                _grid[pos] |= orient;
            else
                _grid[pos] = orient;

            if (!_boardRegistry.ContainsKey(pos))
                _boardRegistry[pos] = new Dictionary<BoardOrientation, GameObject>();
            _boardRegistry[pos][orient] = boardObj;

            OnBoardAdded?.Invoke(pos, orient);
            return true;
        }

        public bool RemoveBoard(Vector3Int pos, BoardOrientation orient)
        {
            if (!HasBoard(pos, orient)) return false;

            _grid[pos] &= ~orient;
            if (_grid[pos] == BoardOrientation.None)
                _grid.Remove(pos);

            if (_boardRegistry.TryGetValue(pos, out var orientDict))
            {
                if (orientDict.TryGetValue(orient, out var obj))
                {
                    if (obj != null) Destroy(obj);
                    orientDict.Remove(orient);
                }
                if (orientDict.Count == 0)
                    _boardRegistry.Remove(pos);
            }

            OnBoardRemoved?.Invoke(pos, orient);
            return true;
        }

        public GameObject GetBoard(Vector3Int pos, BoardOrientation orient)
        {
            if (_boardRegistry.TryGetValue(pos, out var orientDict) &&
                orientDict.TryGetValue(orient, out var obj))
                return obj;
            return null;
        }

        public bool HasAnyNeighbor(Vector3Int pos, BoardOrientation orient)
        {
            var neighbors = BoardAdjacency.GetNeighbors(orient);
            foreach (var n in neighbors)
            {
                if (HasBoard(pos + n.Offset, n.Orientation))
                    return true;
            }
            return false;
        }
    }
}
