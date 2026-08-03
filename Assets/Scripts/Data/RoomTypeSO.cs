using UnityEngine;

namespace BuildATower
{
    [CreateAssetMenu(menuName = "Build-A-Tower/Room Type", fileName = "RoomType")]
    public class RoomTypeSO : ScriptableObject
    {
        public string id;
        public string displayName;
        public RoomCategory category;
        public Vector2Int size = Vector2Int.one;
        public int buildCost = 1000;
        public Color placeholderColor = Color.gray;
        public IncomeModel incomeModel = IncomeModel.None;
        public int baseIncome;
        [Range(0f, 1f)] public float noiseOutput;
        [Range(0f, 1f)] public float noiseSensitivity;
        public bool requiresHousekeeping;
        public bool hasActiveHours;
        public int activeHoursStart;
        public int activeHoursEnd;
        public bool allowAboveGround = true;
        public bool allowBasement;
        public bool isLobby;
        public bool isScaffolding;
        public bool isStairs;
        public bool isElevatorShaft;
        [Min(0)] public int requiredStars;
        [Min(0)] public int maxOccupants;
        [Min(0)] public int eventCapacity;
        public BuildFamily buildFamily = BuildFamily.None;
        public BuildSubgroup buildSubgroup = BuildSubgroup.None;

        public BuildFamily ResolvedBuildFamily()
        {
            if (buildFamily != BuildFamily.None) return buildFamily;
            if (isStairs || isElevatorShaft) return BuildFamily.Transit;
            return category switch
            {
                RoomCategory.Office => BuildFamily.Office,
                RoomCategory.Hotel => BuildFamily.Hotel,
                RoomCategory.Condo => BuildFamily.Condo,
                RoomCategory.Commercial => BuildFamily.Shops,
                RoomCategory.Service => BuildFamily.Utility,
                _ => BuildFamily.None
            };
        }

        public BuildSubgroup ResolvedBuildSubgroup()
        {
            if (buildSubgroup != BuildSubgroup.None) return buildSubgroup;
            if (ResolvedBuildFamily() != BuildFamily.Shops) return BuildSubgroup.None;
            if (!string.IsNullOrEmpty(id) && id.IndexOf("food", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return BuildSubgroup.Food;
            if (!string.IsNullOrEmpty(displayName) &&
                displayName.IndexOf("restaurant", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return BuildSubgroup.Food;
            if (!string.IsNullOrEmpty(displayName) &&
                displayName.IndexOf("food", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return BuildSubgroup.Food;
            return BuildSubgroup.Retail;
        }
    }
}
