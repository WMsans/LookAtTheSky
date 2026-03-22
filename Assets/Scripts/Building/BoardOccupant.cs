using UnityEngine;

namespace Building
{
    public class BoardOccupant : IOccupant
    {
        public OccupantType Type => OccupantType.Board;
        public Vector3Int Anchor { get; }
        public GameObject GameObject { get; }
        public BoardOrientation Orientation { get; }

        public BoardOccupant(Vector3Int anchor, BoardOrientation orientation, GameObject gameObject)
        {
            Anchor = anchor;
            Orientation = orientation;
            GameObject = gameObject;
        }

        public Vector3Int[] GetOccupiedCells()
        {
            return GridConstants.GetOccupiedCells(Anchor, Orientation);
        }
    }
}
