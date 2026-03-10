using UnityEngine;

namespace Building
{
    public static class BoardVisuals
    {
        public const float CellSize = 4f;

        /// <summary>
        /// Returns the world-space center of the given face on a grid cell.
        /// Cell center is (cell + 0.5) * CellSize, then offset by half cell size
        /// in the face's normal direction.
        /// </summary>
        public static Vector3 GetWorldPosition(Vector3Int cell, BoardFace face)
        {
            Vector3 center = ((Vector3)cell + Vector3.one * 0.5f) * CellSize;

            float half = CellSize * 0.5f;
            switch (face)
            {
                case BoardFace.Top:    center.y += half; break;
                case BoardFace.Bottom: center.y -= half; break;
                case BoardFace.North:  center.z += half; break;
                case BoardFace.South:  center.z -= half; break;
                case BoardFace.East:   center.x += half; break;
                case BoardFace.West:   center.x -= half; break;
            }

            return center;
        }

        /// <summary>
        /// Returns the rotation for a board quad placed on the given face.
        /// Top faces are flat on the XZ plane (identity rotation).
        /// </summary>
        public static Quaternion GetWorldRotation(BoardFace face)
        {
            switch (face)
            {
                case BoardFace.Top:    return Quaternion.identity;
                case BoardFace.Bottom: return Quaternion.Euler(180f, 0f, 0f);
                case BoardFace.North:  return Quaternion.Euler(90f, 0f, 0f);
                case BoardFace.South:  return Quaternion.Euler(-90f, 0f, 0f);
                case BoardFace.East:   return Quaternion.Euler(0f, 0f, -90f);
                case BoardFace.West:   return Quaternion.Euler(0f, 0f, 90f);
                default:               return Quaternion.identity;
            }
        }

        /// <summary>
        /// Converts a world position to a grid cell coordinate.
        /// </summary>
        public static Vector3Int WorldToCell(Vector3 worldPos)
        {
            return Vector3Int.FloorToInt(worldPos / CellSize);
        }

        /// <summary>
        /// Converts a world-space normal to the closest BoardFace by comparing
        /// absolute components.
        /// </summary>
        public static BoardFace NormalToFace(Vector3 normal)
        {
            float absX = Mathf.Abs(normal.x);
            float absY = Mathf.Abs(normal.y);
            float absZ = Mathf.Abs(normal.z);

            if (absY >= absX && absY >= absZ)
                return normal.y >= 0f ? BoardFace.Top : BoardFace.Bottom;

            if (absX >= absY && absX >= absZ)
                return normal.x >= 0f ? BoardFace.East : BoardFace.West;

            return normal.z >= 0f ? BoardFace.North : BoardFace.South;
        }

        /// <summary>
        /// Returns the opposite face (Top↔Bottom, North↔South, East↔West).
        /// </summary>
        public static BoardFace OppositeFace(BoardFace face)
        {
            switch (face)
            {
                case BoardFace.Top:    return BoardFace.Bottom;
                case BoardFace.Bottom: return BoardFace.Top;
                case BoardFace.North:  return BoardFace.South;
                case BoardFace.South:  return BoardFace.North;
                case BoardFace.East:   return BoardFace.West;
                case BoardFace.West:   return BoardFace.East;
                default:               return face;
            }
        }

        /// <summary>
        /// Returns the neighbor cell offset for the given face direction.
        /// </summary>
        public static Vector3Int FaceToOffset(BoardFace face)
        {
            switch (face)
            {
                case BoardFace.Top:    return Vector3Int.up;
                case BoardFace.Bottom: return Vector3Int.down;
                case BoardFace.North:  return new Vector3Int(0, 0, 1);
                case BoardFace.South:  return new Vector3Int(0, 0, -1);
                case BoardFace.East:   return Vector3Int.right;
                case BoardFace.West:   return Vector3Int.left;
                default:               return Vector3Int.zero;
            }
        }
    }
}
