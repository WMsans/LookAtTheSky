using UnityEngine;

namespace Building
{
    public class SmallBlockOccupant : IOccupant
    {
        public OccupantType Type => OccupantType.SmallBlock;
        public Vector3Int Anchor { get; }
        public GameObject GameObject { get; }
        public Quaternion Rotation { get; }

        public SmallBlockOccupant(Vector3Int position, Quaternion rotation, GameObject gameObject)
        {
            Anchor = position;
            Rotation = rotation;
            GameObject = gameObject;
        }

        public Vector3Int[] GetOccupiedCells()
        {
            return new Vector3Int[] { Anchor };
        }
    }
}
