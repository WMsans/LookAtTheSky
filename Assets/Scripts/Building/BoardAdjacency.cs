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

        // Z-board at anchor (0,0,0): XZ horizontal plane
        // Board spans cells (0,0,0)(1,0,0)(0,0,1)(1,0,1)
        // World footprint: x[0..4], z[0..4] at y=0
        private static readonly Neighbor[] ZNeighbors = new Neighbor[]
        {
            // 4 coplanar Z-neighbors (offset by 2 = BOARD_SPAN along planar axes)
            new(-2,  0,  0, BoardOrientation.Z),  // shares left edge
            new( 2,  0,  0, BoardOrientation.Z),  // shares right edge
            new( 0,  0, -2, BoardOrientation.Z),  // shares back edge
            new( 0,  0,  2, BoardOrientation.Z),  // shares front edge
            // 4 perpendicular X-boards (XY plane, sharing back/front edges)
            new( 0, -2,  0, BoardOrientation.X),  // X-board top edge = Z back edge
            new( 0,  0,  0, BoardOrientation.X),  // X-board bottom edge = Z back edge
            new( 0, -2,  2, BoardOrientation.X),  // X-board top edge = Z front edge
            new( 0,  0,  2, BoardOrientation.X),  // X-board bottom edge = Z front edge
            // 4 perpendicular Y-boards (YZ plane, sharing left/right edges)
            new( 0, -2,  0, BoardOrientation.Y),  // Y-board top edge = Z left edge
            new( 0,  0,  0, BoardOrientation.Y),  // Y-board bottom edge = Z left edge
            new( 2, -2,  0, BoardOrientation.Y),  // Y-board top edge = Z right edge
            new( 2,  0,  0, BoardOrientation.Y),  // Y-board bottom edge = Z right edge
        };

        // X-board at anchor (0,0,0): XY vertical plane (faces Z axis)
        // Board spans cells (0,0,0)(1,0,0)(0,1,0)(1,1,0)
        // World footprint: x[0..4], y[0..4] at z=0
        private static readonly Neighbor[] XNeighbors = new Neighbor[]
        {
            // 4 coplanar X-neighbors
            new(-2,  0,  0, BoardOrientation.X),  // shares left edge
            new( 2,  0,  0, BoardOrientation.X),  // shares right edge
            new( 0, -2,  0, BoardOrientation.X),  // shares bottom edge
            new( 0,  2,  0, BoardOrientation.X),  // shares top edge
            // 4 perpendicular Z-boards (XZ plane, sharing bottom/top edges)
            new( 0,  0, -2, BoardOrientation.Z),  // Z front edge = X bottom edge
            new( 0,  0,  0, BoardOrientation.Z),  // Z back edge = X bottom edge
            new( 0,  2, -2, BoardOrientation.Z),  // Z front edge = X top edge
            new( 0,  2,  0, BoardOrientation.Z),  // Z back edge = X top edge
            // 4 perpendicular Y-boards (YZ plane, sharing left/right edges)
            new( 0,  0, -2, BoardOrientation.Y),  // Y front edge = X left edge
            new( 0,  0,  0, BoardOrientation.Y),  // Y back edge = X left edge
            new( 2,  0, -2, BoardOrientation.Y),  // Y front edge = X right edge
            new( 2,  0,  0, BoardOrientation.Y),  // Y back edge = X right edge
        };

        // Y-board at anchor (0,0,0): YZ vertical plane (faces X axis)
        // Board spans cells (0,0,0)(0,1,0)(0,0,1)(0,1,1)
        // World footprint: y[0..4], z[0..4] at x=0
        private static readonly Neighbor[] YNeighbors = new Neighbor[]
        {
            // 4 coplanar Y-neighbors
            new( 0,  0, -2, BoardOrientation.Y),  // shares back edge
            new( 0,  0,  2, BoardOrientation.Y),  // shares front edge
            new( 0, -2,  0, BoardOrientation.Y),  // shares bottom edge
            new( 0,  2,  0, BoardOrientation.Y),  // shares top edge
            // 4 perpendicular Z-boards (XZ plane, sharing bottom/top edges)
            new(-2,  0,  0, BoardOrientation.Z),  // Z right edge = Y bottom edge
            new( 0,  0,  0, BoardOrientation.Z),  // Z left edge = Y bottom edge
            new(-2,  2,  0, BoardOrientation.Z),  // Z right edge = Y top edge
            new( 0,  2,  0, BoardOrientation.Z),  // Z left edge = Y top edge
            // 4 perpendicular X-boards (XY plane, sharing back/front edges)
            new(-2,  0,  0, BoardOrientation.X),  // X right edge = Y back edge
            new( 0,  0,  0, BoardOrientation.X),  // X left edge = Y back edge
            new(-2,  0,  2, BoardOrientation.X),  // X right edge = Y front edge
            new( 0,  0,  2, BoardOrientation.X),  // X left edge = Y front edge
        };

        private static Neighbor[] _fullNeighbors;

        private static Neighbor[] BuildFullNeighbors()
        {
            var set = new System.Collections.Generic.HashSet<(int, int, int, BoardOrientation)>();
            var result = new System.Collections.Generic.List<Neighbor>();

            void AddUnique(Neighbor[] neighbors)
            {
                foreach (var n in neighbors)
                {
                    var key = (n.Offset.x, n.Offset.y, n.Offset.z, n.Orientation);
                    if (set.Add(key))
                        result.Add(n);
                }
            }

            AddUnique(XNeighbors);
            AddUnique(YNeighbors);
            AddUnique(ZNeighbors);

            return result.ToArray();
        }

        public static Neighbor[] GetNeighbors(BoardOrientation orient)
        {
            if (orient == (BoardOrientation.X | BoardOrientation.Y | BoardOrientation.Z))
            {
                _fullNeighbors ??= BuildFullNeighbors();
                return _fullNeighbors;
            }

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
