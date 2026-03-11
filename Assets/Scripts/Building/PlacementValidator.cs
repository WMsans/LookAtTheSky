using System.Collections.Generic;
using UnityEngine;

namespace Building
{
    public static class PlacementValidator
    {
        public static bool CanPlaceAt(BuildingGrid grid, Vector3Int pos, BoardOrientation orient)
        {
            if (grid.HasBoard(pos, orient)) return false;
            return HasAnyConnection(grid, pos, orient);
        }

        public static bool CanPlaceOnGround(Vector3 worldPos, LayerMask groundLayer)
        {
            float checkDistance = 0.5f;
            Vector3 checkStart = worldPos + Vector3.up * checkDistance;
            
            if (Physics.Raycast(checkStart, Vector3.down, out RaycastHit hit, checkDistance * 2f, groundLayer))
            {
                return true;
            }
            return false;
        }

        public static bool HasAnyConnection(BuildingGrid grid, Vector3Int pos, BoardOrientation orient)
        {
            var connections = GetConnectedBoards(grid, pos, orient);
            return connections.Count > 0;
        }

        public static List<(Vector3Int pos, BoardOrientation orient)> GetConnectedBoards(
            BuildingGrid grid, Vector3Int pos, BoardOrientation orient)
        {
            var connections = new List<(Vector3Int, BoardOrientation)>();
            
            var offsets = GetAdjacentOffsets(orient);
            
            foreach (var offset in offsets)
            {
                Vector3Int adjacentPos = pos + offset.pos;
                BoardOrientation adjacentOrient = offset.orient;
                
                if (grid.HasBoard(adjacentPos, adjacentOrient))
                {
                    connections.Add((adjacentPos, adjacentOrient));
                }
            }
            
            return connections;
        }

        private static List<(Vector3Int pos, BoardOrientation orient)> GetAdjacentOffsets(BoardOrientation orient)
        {
            var offsets = new List<(Vector3Int, BoardOrientation)>();
            
            for (int i = 0; i < 3; i++)
            {
                BoardOrientation checkOrient = (BoardOrientation)(1 << i);
                
                Vector3Int[] edgeOffsets = GetEdgeOffsets(orient, checkOrient);
                
                foreach (var offset in edgeOffsets)
                {
                    offsets.Add((offset, checkOrient));
                }
            }
            
            return offsets;
        }

        private static Vector3Int[] GetEdgeOffsets(BoardOrientation from, BoardOrientation to)
        {
            var offsets = new List<Vector3Int>();
            
            Vector3Int[] directions = {
                Vector3Int.right, Vector3Int.left,
                Vector3Int.up, Vector3Int.down,
                Vector3Int.forward, Vector3Int.back
            };
            
            foreach (var dir in directions)
            {
                if (SharesEdge(from, to, dir))
                {
                    offsets.Add(dir);
                }
            }
            
            return offsets.ToArray();
        }

        private static bool SharesEdge(BoardOrientation a, BoardOrientation b, Vector3Int direction)
        {
            return true;
        }
    }
}
