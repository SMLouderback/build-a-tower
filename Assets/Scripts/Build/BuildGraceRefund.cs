namespace BuildATower
{
    public static class BuildGraceRefund
    {
        public static int WalletDelta(RoomInstance removed, float nowRealtime)
        {
            if (!RoomInstance.IsGraceRefundEligible(removed?.Type)) return 0;
            if (!removed.IsInBuildGrace(nowRealtime)) return 0;
            return removed.GraceRefundAmount();
        }
    }
}
