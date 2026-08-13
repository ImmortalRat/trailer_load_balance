namespace TrailerLoadBalance.Web.Models;

/// <summary>
/// Everything that's saved to the browser's localStorage so a session survives a reload and
/// stays in sync across tabs. Deliberately small/flat so it round-trips through JS interop as
/// plain JSON without extra converters.
/// </summary>
public sealed class PersistedState
{
    public string? ProfileId { get; set; }
    public double TiltDeg { get; set; }
    public Dictionary<string, double> EquipmentWeightOverridesLb { get; set; } = [];
    public Dictionary<string, bool> EquipmentInstalled { get; set; } = [];
    public List<CargoItem> Cargo { get; set; } = [];
}
