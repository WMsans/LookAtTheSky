using UnityEngine;

namespace Inventory
{
    [CreateAssetMenu(fileName = "NewBuildingItem", menuName = "Building/Building Item")]
    public class BuildingItemSO : ScriptableObject
    {
        public string itemName;
        public Sprite icon;
        public int maxStackSize = 64;
        public GameObject prefab;
        public PlacementMode placementMode;
    }
}
