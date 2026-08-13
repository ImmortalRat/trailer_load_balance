namespace TrailerLoadBalance.Web.Models;

/// <summary>
/// A user-placed piece of cargo inside the trailer footprint. Position/size are in inches,
/// in the same trailer-local coordinate frame as <see cref="TrailerProfile"/> (X from hitch,
/// Y from centerline, Z from floor). Height is currently fixed (see CargoDefaults) since only
/// a generic box cargo type exists today; it is stored per-item so future stock cargo types
/// with their own heights can override it.
/// </summary>
public sealed class CargoItem
{
    public required string Id { get; init; }
    public required string Name { get; set; }

    /// <summary>Hex color, e.g. "#4f8ef7", used for the rendered rectangle and legend.</summary>
    public required string Color { get; set; }

    public required double XIn { get; set; }
    public required double YIn { get; set; }
    public required double LengthIn { get; set; }
    public required double WidthIn { get; set; }

    /// <summary>Height of the cargo footprint (inches). Fixed at 12in for the generic box today.</summary>
    public double HeightIn { get; set; } = CargoDefaults.DefaultHeightIn;

    /// <summary>Height of the cargo's bottom face above the trailer floor (inches). Floor level = 0.</summary>
    public double ZIn { get; set; } = 0;

    public required double WeightLb { get; set; }
}

public static class CargoDefaults
{
    public const double DefaultHeightIn = 12;
}
