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
    }
}
