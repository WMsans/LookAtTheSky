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

        // Per-cell orientation flags (every cell a board touches has its flag set)
        private Dictionary<Vector3Int, BoardOrientation> _grid = new();

        // Anchor-only registry: only the anchor cell maps to the GameObject
        private Dictionary<Vector3Int, Dictionary<BoardOrientation, GameObject>> _boardRegistry = new();

        // Reverse lookup: (cell, orient) -> anchor position
        private Dictionary<(Vector3Int, BoardOrientation), Vector3Int> _anchorMap = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public bool IsEmpty() => _boardRegistry.Count == 0;

        /// <summary>
        /// Returns true if the given cell has the given orientation flag set.
        /// This is a per-cell query — any cell a board occupies will return true.
        /// </summary>
        public bool HasBoard(Vector3Int pos, BoardOrientation orient)
        {
            return _grid.TryGetValue(pos, out var flags) && (flags & orient) != 0;
        }

        /// <summary>
        /// Returns the anchor position of the board occupying cell `pos` at `orient`,
        /// or null if no board occupies that cell at that orientation.
        /// </summary>
        public Vector3Int? GetAnchor(Vector3Int cell, BoardOrientation orient)
        {
            if (_anchorMap.TryGetValue((cell, orient), out var anchor))
                return anchor;
            return null;
        }

        /// <summary>
        /// Place a board at the given anchor position and orientation.
        /// The board will occupy multiple cells (2x2 for panels, 2x2x2 for FullCell).
        /// </summary>
        public bool AddBoard(Vector3Int anchor, BoardOrientation orient, GameObject boardObj)
        {
            var cells = GridConstants.GetOccupiedCells(anchor, orient);

            bool isFull = orient == FULL;

            // Validate all cells are free
            foreach (var cell in cells)
            {
                if (_grid.TryGetValue(cell, out var existingFlags))
                {
                    bool existingHasFull = existingFlags == FULL;

                    // Can't place a panel where a FullCell exists
                    if (existingHasFull && !isFull) return false;
                    // Can't place a FullCell where any panel exists
                    if (isFull && existingFlags != BoardOrientation.None) return false;
                    // Can't place if this orientation is already occupied
                    if ((existingFlags & orient) != 0) return false;
                }
            }

            // Set orientation flag in all occupied cells
            foreach (var cell in cells)
            {
                if (_grid.ContainsKey(cell))
                    _grid[cell] |= orient;
                else
                    _grid[cell] = orient;

                _anchorMap[(cell, orient)] = anchor;
            }

            // Store GameObject at anchor only
            if (!_boardRegistry.ContainsKey(anchor))
                _boardRegistry[anchor] = new Dictionary<BoardOrientation, GameObject>();
            _boardRegistry[anchor][orient] = boardObj;

            OnBoardAdded?.Invoke(anchor, orient);
            return true;
        }

        /// <summary>
        /// Remove a board at the given anchor position and orientation.
        /// Clears flags in all occupied cells.
        /// </summary>
        public bool RemoveBoard(Vector3Int anchor, BoardOrientation orient)
        {
            // Verify the board exists at this anchor
            if (!_boardRegistry.TryGetValue(anchor, out var orientDict) ||
                !orientDict.ContainsKey(orient))
                return false;

            var cells = GridConstants.GetOccupiedCells(anchor, orient);

            // Clear orientation flag in all occupied cells
            foreach (var cell in cells)
            {
                if (_grid.TryGetValue(cell, out var flags))
                {
                    flags &= ~orient;
                    if (flags == BoardOrientation.None)
                        _grid.Remove(cell);
                    else
                        _grid[cell] = flags;
                }

                _anchorMap.Remove((cell, orient));
            }

            // Destroy GameObject and clean up registry
            if (orientDict.TryGetValue(orient, out var obj))
            {
                if (obj != null) Destroy(obj);
                orientDict.Remove(orient);
            }
            if (orientDict.Count == 0)
                _boardRegistry.Remove(anchor);

            OnBoardRemoved?.Invoke(anchor, orient);
            return true;
        }

        /// <summary>
        /// Get the GameObject for the board at the given anchor and orientation.
        /// </summary>
        public GameObject GetBoard(Vector3Int anchor, BoardOrientation orient)
        {
            if (_boardRegistry.TryGetValue(anchor, out var orientDict) &&
                orientDict.TryGetValue(orient, out var obj))
                return obj;
            return null;
        }

        /// <summary>
        /// Returns true if any neighbor of the board at anchor/orient exists.
        /// Uses anchor-to-anchor adjacency offsets.
        /// </summary>
        public bool HasAnyNeighbor(Vector3Int anchor, BoardOrientation orient)
        {
            var neighbors = BoardAdjacency.GetNeighbors(orient);
            foreach (var n in neighbors)
            {
                Vector3Int neighborAnchor = anchor + n.Offset;
                // Check if a board exists at that anchor by checking the anchor cell
                if (HasBoard(neighborAnchor, n.Orientation))
                {
                    // Verify it's actually an anchor (not just an occupied cell from a different board)
                    var foundAnchor = GetAnchor(neighborAnchor, n.Orientation);
                    if (foundAnchor.HasValue && foundAnchor.Value == neighborAnchor)
                        return true;
                }
            }
            return false;
        }
    }
}
