using UnityEngine;

namespace Building
{
    public enum OccupantType
    {
        Board,
        SmallBlock
    }

    public interface IOccupant
    {
        OccupantType Type { get; }
        Vector3Int Anchor { get; }
        GameObject GameObject { get; }
        Vector3Int[] GetOccupiedCells();
    }
}
