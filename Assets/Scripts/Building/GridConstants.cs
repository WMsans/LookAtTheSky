namespace Building
{
    public static class GridConstants
    {
        public const float CELL_SIZE = 2f;
        public const int BOARD_SPAN = 2;
        public const float BOARD_WORLD_SIZE = CELL_SIZE * BOARD_SPAN; // 4f

        private const BoardOrientation FULL = BoardOrientation.X | BoardOrientation.Y | BoardOrientation.Z;

        /// <summary>
        /// Returns all grid cells occupied by a board anchored at the given position.
        /// Boards span BOARD_SPAN cells in each of their two planar axes.
        /// FullCell spans BOARD_SPAN cells in all three axes.
        /// </summary>
        public static UnityEngine.Vector3Int[] GetOccupiedCells(
            UnityEngine.Vector3Int anchor, BoardOrientation orient)
        {
            if (orient == FULL)
            {
                // 2x2x2 = 8 cells
                return new UnityEngine.Vector3Int[]
                {
                    anchor,
                    anchor + new UnityEngine.Vector3Int(1, 0, 0),
                    anchor + new UnityEngine.Vector3Int(0, 1, 0),
                    anchor + new UnityEngine.Vector3Int(0, 0, 1),
                    anchor + new UnityEngine.Vector3Int(1, 1, 0),
                    anchor + new UnityEngine.Vector3Int(1, 0, 1),
                    anchor + new UnityEngine.Vector3Int(0, 1, 1),
                    anchor + new UnityEngine.Vector3Int(1, 1, 1),
                };
            }

            // 2x2 = 4 cells in the board's planar axes
            return orient switch
            {
                // Z-board (XZ plane): expand along X and Z
                BoardOrientation.Z => new UnityEngine.Vector3Int[]
                {
                    anchor,
                    anchor + new UnityEngine.Vector3Int(1, 0, 0),
                    anchor + new UnityEngine.Vector3Int(0, 0, 1),
                    anchor + new UnityEngine.Vector3Int(1, 0, 1),
                },
                // X-board (XY plane): expand along X and Y
                BoardOrientation.X => new UnityEngine.Vector3Int[]
                {
                    anchor,
                    anchor + new UnityEngine.Vector3Int(1, 0, 0),
                    anchor + new UnityEngine.Vector3Int(0, 1, 0),
                    anchor + new UnityEngine.Vector3Int(1, 1, 0),
                },
                // Y-board (YZ plane): expand along Y and Z
                BoardOrientation.Y => new UnityEngine.Vector3Int[]
                {
                    anchor,
                    anchor + new UnityEngine.Vector3Int(0, 1, 0),
                    anchor + new UnityEngine.Vector3Int(0, 0, 1),
                    anchor + new UnityEngine.Vector3Int(0, 1, 1),
                },
                _ => new UnityEngine.Vector3Int[] { anchor }
            };
        }

        /// <summary>
        /// Convert a small block's single-cell grid position to its world-space center.
        /// Small blocks occupy 1 cell = CELL_SIZE^3 world units.
        /// </summary>
        public static UnityEngine.Vector3 SmallBlockGridToWorld(UnityEngine.Vector3Int cell)
        {
            float half = CELL_SIZE / 2f;
            return new UnityEngine.Vector3(
                cell.x * CELL_SIZE + half,
                cell.y * CELL_SIZE + half,
                cell.z * CELL_SIZE + half
            );
        }
    }
}
