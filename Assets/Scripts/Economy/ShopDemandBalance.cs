namespace BuildATower
{
    public static class ShopDemandBalance
    {
        public const float OfficeFood = 0.50f;
        public const float OfficeRetail = 0.10f;
        public const float CondoFood = 0.40f;
        public const float CondoRetail = 0.30f;
        public const float HotelFood = 0.70f;
        public const float HotelRetail = 0.25f;
        public const float SpillRate = 0.25f;
        public const float ShopUpkeepRate = 0.50f;
        public const float MatchingStressMax = 6f;
        public const float TowerStressMax = 2f;
        public const int HistoryCapacity = 30;

        public static ShopDemandFamily FamilyFor(RoomTypeSO type) =>
            type != null && type.ResolvedBuildSubgroup() == BuildSubgroup.Retail
                ? ShopDemandFamily.Retail
                : ShopDemandFamily.Food;

        public static ShopDemandTier TierForShop(RoomTypeSO type) =>
            type == null || type.requiredStars <= 0 ? ShopDemandTier.Budget :
            type.requiredStars <= 2 ? ShopDemandTier.Mid :
            ShopDemandTier.Premium;

        public static ShopDemandTier TierForWealth(WealthBand wealth) => wealth switch
        {
            WealthBand.Mid => ShopDemandTier.Mid,
            WealthBand.Upper or WealthBand.Premium => ShopDemandTier.Premium,
            _ => ShopDemandTier.Budget
        };

        public static int DailyUpkeep(RoomTypeSO type) =>
            type == null ? 0 : (int)System.Math.Round(
                System.Math.Max(0, type.baseIncome) * ShopUpkeepRate,
                System.MidpointRounding.AwayFromZero);
    }
}
