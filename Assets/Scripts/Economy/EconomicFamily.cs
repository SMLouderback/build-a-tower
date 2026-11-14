namespace BuildATower
{
    /// <summary>Economic family for VPSF tables (living + non-living stubs for later balancer rows).</summary>
    public enum EconomicFamily
    {
        None = 0,
        Office,
        Hotel,
        Condo,
        Shops,
        Leisure,
        Service,
        Transit,
        Infrastructure
    }

    /// <summary>Tenant economic class for upkeep / clash / elev-wait rules.</summary>
    public enum TenantClass
    {
        Lower = 0,
        Mid = 1,
        Upper = 2
    }
}
