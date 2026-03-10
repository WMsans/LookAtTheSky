using System.Collections.Generic;
using UnityEngine;

namespace Building
{
    public struct BoardKey
    {
        public Vector3Int Cell;
        public BoardFace Face;

        public BoardKey(Vector3Int cell, BoardFace face)
        {
            Cell = cell;
            Face = face;
        }
    }

    public class BuildingGrid : MonoBehaviour
    {
        public static BuildingGrid Instance { get; private set; }

        [SerializeField] private GameObject boardPrefab;

        private readonly Dictionary<BoardKey, GameObject> _boards = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public bool IsEmpty() => _boards.Count == 0;

        /// <summary>
        /// Normalizes a (cell, face) pair to canonical form.
        /// The canonical form stores a shared face on the cell with the lower coordinate.
        /// e.g. Top of (1,0,0) is canonical; Bottom of (1,1,0) normalizes to Top of (1,0,0).
        /// </summary>
        public static BoardKey Canonicalize(Vector3Int cell, BoardFace face)
        {
            switch (face)
            {
                case BoardFace.Bottom:
                    return new BoardKey(cell + Vector3Int.down, BoardFace.Top);
                case BoardFace.South:
                    return new BoardKey(cell + new Vector3Int(0, 0, -1), BoardFace.North);
                case BoardFace.West:
                    return new BoardKey(cell + Vector3Int.left, BoardFace.East);
                default:
                    return new BoardKey(cell, face);
            }
        }

        public bool HasBoard(Vector3Int cell, BoardFace face)
        {
            var key = Canonicalize(cell, face);
            return _boards.ContainsKey(key);
        }

        /// <summary>
        /// Checks if the given face has at least one neighboring board.
        /// Neighbors: other faces of the same cell, or same face type on adjacent cells.
        /// </summary>
        public bool HasNeighbor(Vector3Int cell, BoardFace face)
        {
            // Check all 6 faces of the same cell (excluding the target face itself)
            for (int i = 0; i < 6; i++)
            {
                BoardFace f = (BoardFace)i;
                if (f == face) continue;
                if (HasBoard(cell, f)) return true;
            }

            // Check the same face on each of the 6 neighboring cells
            for (int i = 0; i < 6; i++)
            {
                BoardFace f = (BoardFace)i;
                Vector3Int neighborCell = cell + BoardVisuals.FaceToOffset(f);
                if (HasBoard(neighborCell, face)) return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to place a board. Returns true if successful.
        /// </summary>
        public bool TryPlace(Vector3Int cell, BoardFace face)
        {
            var key = Canonicalize(cell, face);
            if (_boards.ContainsKey(key)) return false;

            if (!IsEmpty() && !HasNeighbor(cell, face)) return false;

            Vector3 pos = BoardVisuals.GetWorldPosition(key.Cell, key.Face);
            Quaternion rot = BoardVisuals.GetWorldRotation(key.Face);
            GameObject board = Instantiate(boardPrefab, pos, rot);
            _boards[key] = board;
            return true;
        }

        /// <summary>
        /// Removes the board at the given face. Returns true if a board was removed.
        /// </summary>
        public bool Remove(Vector3Int cell, BoardFace face)
        {
            var key = Canonicalize(cell, face);
            if (!_boards.TryGetValue(key, out GameObject board)) return false;

            _boards.Remove(key);
            Destroy(board);
            return true;
        }

        /// <summary>
        /// Finds the BoardKey for a placed board GameObject. Used for removal by raycast hit.
        /// </summary>
        public bool TryGetKeyForBoard(GameObject boardObj, out BoardKey key)
        {
            foreach (var kvp in _boards)
            {
                if (kvp.Value == boardObj)
                {
                    key = kvp.Key;
                    return true;
                }
            }
            key = default;
            return false;
        }

        /// <summary>
        /// Returns whether placement would be valid at this location.
        /// </summary>
        public bool IsValidPlacement(Vector3Int cell, BoardFace face)
        {
            var key = Canonicalize(cell, face);
            if (_boards.ContainsKey(key)) return false;
            if (IsEmpty()) return true;
            return HasNeighbor(cell, face);
        }
    }
}
