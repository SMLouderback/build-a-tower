namespace BuildATower
{
    public enum AgentRole
    {
        OfficeWorker,
        HotelGuest,
        CondoResident,
        StreetVisitor,
        EventVisitor,
        Maid,
        Handyman,
        Security,
        Criminal
    }

    public enum AgentPhase
    {
        Outside,
        Moving,
        AtHome,
        Working,
        Staying,
        WaitingAtElevator,
        Riding,
        VisitingShop
    }

    public enum CondoJobKind
    {
        None,
        InTower,
        Outside
    }

    public enum CondoOutsidePhase
    {
        None,
        ToWorkCommute,
        Working,
        ReturnCommute
    }
}
