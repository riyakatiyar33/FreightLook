namespace Models.Constants;

public static class SlabCodes
{
    public const string Minimum = "M";
    public const string Plus45 = "+45";
    public const string Plus100 = "+100";
    public const string Plus250 = "+250";
    public const string Plus300 = "+300";
    public const string Plus500 = "+500";
    public const string Plus1000 = "+1000";

    /// <summary>All slab codes in ascending weight order.</summary>
    public static readonly IReadOnlyList<string> All = [Minimum, Plus45, Plus100, Plus250, Plus300, Plus500, Plus1000];
}

public static class SurchargeTypes
{
    public const string Fuel = "FSC";
    public const string Security = "SSC";
    public const string SecurityAlt = "SEC";
    public const string XRay = "XRAY";
    public const string Misc = "MISC";
    public const string Cartage = "CTG";
    public const string Ams = "AMS";
    public const string Awb = "AWB";

    public static readonly IReadOnlyList<string> All = [Fuel, Security, SecurityAlt, XRay, Misc, Cartage, Ams, Awb];
}

public static class SurchargeBasis
{
    /// <summary>Charged per chargeable kg.</summary>
    public const string PerKg = "KG";

    /// <summary>Flat amount per shipment (AWB).</summary>
    public const string PerShipment = "SHIPMENT";

    public static readonly IReadOnlyList<string> All = [PerKg, PerShipment];
}
