using System;
using System.Collections.Generic;
using UnityEngine;

namespace Building
{
    public class BuildingGrid : MonoBehaviour
    {
        public static BuildingGrid Instance { get; private set; }

        private Dictionary<Vector3Int, BoardOrientation> grid = new();
        private Dictionary<Vector3Int, Dictionary<BoardOrientation, GameObject>> boardRegistry = new();

        public event Action<Vector3Int, BoardOrientation> OnBoardAdded;
        public event Action<Vector3Int, BoardOrientation> OnBoardRemoved;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public bool HasBoard(Vector3Int pos, BoardOrientation orient)
        {
            return grid.TryGetValue(pos, out var existing) && (existing & orient) != 0;
        }

        public void AddBoard(Vector3Int pos, BoardOrientation orient)
        {
            if (HasBoard(pos, orient)) return;

            if (grid.ContainsKey(pos))
                grid[pos] |= orient;
            else
                grid[pos] = orient;

            OnBoardAdded?.Invoke(pos, orient);
        }

        public void RemoveBoard(Vector3Int pos, BoardOrientation orient)
        {
            if (!HasBoard(pos, orient)) return;

            grid[pos] &= ~orient;
            if (grid[pos] == BoardOrientation.None)
                grid.Remove(pos);

            OnBoardRemoved?.Invoke(pos, orient);
        }

        public GameObject GetBoard(Vector3Int pos, BoardOrientation orient)
        {
            if (!boardRegistry.TryGetValue(pos, out var orientDict)) return null;
            if (!orientDict.TryGetValue(orient, out var board)) return null;
            return board;
        }

        public void RegisterBoard(Vector3Int pos, BoardOrientation orient, GameObject board)
        {
            if (!boardRegistry.ContainsKey(pos))
                boardRegistry[pos] = new Dictionary<BoardOrientation, GameObject>();
            boardRegistry[pos][orient] = board;
        }

        public void UnregisterBoard(Vector3Int pos, BoardOrientation orient)
        {
            if (!boardRegistry.TryGetValue(pos, out var orientDict)) return;
            orientDict.Remove(orient);
            if (orientDict.Count == 0)
                boardRegistry.Remove(pos);
        }

        public BoardOrientation GetOrientationsAt(Vector3Int pos)
        {
            return grid.TryGetValue(pos, out var orient) ? orient : BoardOrientation.None;
        }

        public void Clear()
        {
            foreach (var orientDict in boardRegistry.Values)
            {
                foreach (var board in orientDict.Values)
                {
                    if (board != null) Destroy(board);
                }
            }
            grid.Clear();
            boardRegistry.Clear();
        }
    }
}
