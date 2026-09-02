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
        public bool isSkyLobby;
        public bool isScaffolding;
        public bool isStairs;
        public bool isElevatorShaft;
        public bool isParkingRamp;
        public LuxuryBand luxuryBand = LuxuryBand.None;
        [Min(0f)] public float cleanMinutes;
        [Min(0)] public int requiredStars;
        [Min(0)] public int maxOccupants;
        [Min(0)] public int eventCapacity;
        public BuildFamily buildFamily = BuildFamily.None;
        public BuildSubgroup buildSubgroup = BuildSubgroup.None;

        public BuildFamily ResolvedBuildFamily()
        {
            if (buildFamily != BuildFamily.None) return buildFamily;
            if (isStairs || isElevatorShaft || isParkingRamp) return BuildFamily.Transit;
            if (!string.IsNullOrEmpty(id) &&
                (id == ParkingStalls.ParkingId ||
                 id == ParkingStalls.ValetId ||
                 id == ParkingStalls.RampId))
                return BuildFamily.Transit;
            return category switch
            {
                RoomCategory.Office => BuildFamily.Office,
                RoomCategory.Hotel => BuildFamily.Hotel,
                RoomCategory.Condo => BuildFamily.Condo,
                RoomCategory.Commercial => BuildFamily.Shops,
                RoomCategory.Service => BuildFamily.Utility,
                RoomCategory.Parking => BuildFamily.Transit,
                _ => BuildFamily.None
            };
        }

        public ElevatorShaftKind ResolvedElevatorKind()
        {
            if (!isElevatorShaft) return ElevatorShaftKind.Normal;
            if (id == "elevator_express") return ElevatorShaftKind.Express;
            if (id == "elevator_service") return ElevatorShaftKind.Service;
            return ElevatorShaftKind.Normal;
        }

        public static RoomTypeSO CreateRuntimeElevator(
            string id,
            string displayName,
            ElevatorShaftKind kind,
            int requiredStars,
            int buildCost = 8000)
        {
            var width = kind == ElevatorShaftKind.Express ? 2 : 1;
            var so = CreateInstance<RoomTypeSO>();
            so.id = id;
            so.displayName = displayName;
            so.category = RoomCategory.Transit;
            so.isElevatorShaft = true;
            so.allowAboveGround = true;
            so.allowBasement = kind == ElevatorShaftKind.Service;
            so.requiredStars = requiredStars;
            so.buildCost = buildCost;
            so.size = new Vector2Int(width, 2);
            so.placeholderColor = kind switch
            {
                ElevatorShaftKind.Express => new Color(0.55f, 0.72f, 0.95f, 1f),
                ElevatorShaftKind.Service => new Color(0.48f, 0.50f, 0.42f, 1f),
                _ => new Color(0.45f, 0.45f, 0.5f, 1f)
            };
            return so;
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
