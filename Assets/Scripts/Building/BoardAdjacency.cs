using UnityEngine;

namespace Building
{
    public static class BoardAdjacency
    {
        public struct Neighbor
        {
            public Vector3Int Offset;
            public BoardOrientation Orientation;

            public Neighbor(int x, int y, int z, BoardOrientation orient)
            {
                Offset = new Vector3Int(x, y, z);
                Orientation = orient;
            }
        }

        // Z-board at (0,0,0): corners (0,0,0)(1,0,0)(1,0,1)(0,0,1) — XZ horizontal plane
        private static readonly Neighbor[] ZNeighbors = new Neighbor[]
        {
            // 4 coplanar Z-neighbors
            new(-1, 0, 0, BoardOrientation.Z),  // shares left edge
            new( 1, 0, 0, BoardOrientation.Z),  // shares right edge
            new( 0, 0,-1, BoardOrientation.Z),  // shares back edge
            new( 0, 0, 1, BoardOrientation.Z),  // shares front edge
            // 4 perpendicular below (y-1)
            new( 0,-1, 0, BoardOrientation.X),  // X-board top = Z back edge
            new( 0,-1, 1, BoardOrientation.X),  // X-board top = Z front edge
            new( 0,-1, 0, BoardOrientation.Y),  // Y-board top = Z left edge
            new( 1,-1, 0, BoardOrientation.Y),  // Y-board top = Z right edge
            // 4 perpendicular above (y)
            new( 0, 0, 0, BoardOrientation.X),  // X-board bottom = Z back edge
            new( 0, 0, 1, BoardOrientation.X),  // X-board bottom = Z front edge
            new( 0, 0, 0, BoardOrientation.Y),  // Y-board bottom = Z left edge
            new( 1, 0, 0, BoardOrientation.Y),  // Y-board bottom = Z right edge
        };

        // X-board at (0,0,0): corners (0,0,0)(1,0,0)(1,1,0)(0,1,0) — XY vertical plane
        private static readonly Neighbor[] XNeighbors = new Neighbor[]
        {
            // 4 coplanar X-neighbors
            new(-1, 0, 0, BoardOrientation.X),  // shares left edge
            new( 1, 0, 0, BoardOrientation.X),  // shares right edge
            new( 0,-1, 0, BoardOrientation.X),  // shares bottom edge
            new( 0, 1, 0, BoardOrientation.X),  // shares top edge
            // 4 perpendicular on z- side
            new( 0, 0,-1, BoardOrientation.Z),  // Z front = X bottom edge
            new( 0, 1,-1, BoardOrientation.Z),  // Z front = X top edge
            new( 0, 0,-1, BoardOrientation.Y),  // Y right = X left edge
            new( 1, 0,-1, BoardOrientation.Y),  // Y right = X right edge
            // 4 perpendicular on z+ side
            new( 0, 0, 0, BoardOrientation.Z),  // Z back = X bottom edge
            new( 0, 1, 0, BoardOrientation.Z),  // Z back = X top edge
            new( 0, 0, 0, BoardOrientation.Y),  // Y left = X left edge
            new( 1, 0, 0, BoardOrientation.Y),  // Y left = X right edge
        };

        // Y-board at (0,0,0): corners (0,0,0)(0,1,0)(0,1,1)(0,0,1) — YZ vertical plane
        private static readonly Neighbor[] YNeighbors = new Neighbor[]
        {
            // 4 coplanar Y-neighbors
            new( 0, 0,-1, BoardOrientation.Y),  // shares back edge
            new( 0, 0, 1, BoardOrientation.Y),  // shares front edge
            new( 0,-1, 0, BoardOrientation.Y),  // shares bottom edge
            new( 0, 1, 0, BoardOrientation.Y),  // shares top edge
            // 4 perpendicular on x- side
            new(-1, 0, 0, BoardOrientation.Z),  // Z right = Y bottom edge
            new(-1, 1, 0, BoardOrientation.Z),  // Z right = Y top edge
            new(-1, 0, 0, BoardOrientation.X),  // X right = Y back edge
            new(-1, 0, 1, BoardOrientation.X),  // X right = Y front edge
            // 4 perpendicular on x+ side
            new( 0, 0, 0, BoardOrientation.Z),  // Z left = Y bottom edge
            new( 0, 1, 0, BoardOrientation.Z),  // Z left = Y top edge
            new( 0, 0, 0, BoardOrientation.X),  // X left = Y back edge
            new( 0, 0, 1, BoardOrientation.X),  // X left = Y front edge
        };

        public static Neighbor[] GetNeighbors(BoardOrientation orient)
        {
            return orient switch
            {
                BoardOrientation.X => XNeighbors,
                BoardOrientation.Y => YNeighbors,
                BoardOrientation.Z => ZNeighbors,
                _ => System.Array.Empty<Neighbor>()
            };
        }
    }
}
