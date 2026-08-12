namespace TrailerLoadBalance.Web.Models;

/// <summary>
/// Static, server-bundled description of a trailer's geometry and weight ratings.
/// Coordinate system: X = distance in inches from the hitch ball (0) toward the rear (increasing),
/// Y = distance in inches from the trailer's longitudinal centerline (negative = driver/left side,
/// positive = passenger/right side when facing forward), Z = height in inches above the trailer
/// floor (0 = floor level). For rectangular items, X/Y denote the min-corner (front-left) edge,
/// not the center - see <see cref="CargoItem"/> and <see cref="EquipmentItem"/>.
/// </summary>
public sealed class TrailerProfile
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Manufacturer { get; init; }
    public required string Model { get; init; }
    public int? Year { get; init; }
    public string? SerialNumberPattern { get; init; }

    /// <summary>Overall trailer body length (inches), not including tongue/A-frame.</summary>
    public required double BodyLengthIn { get; init; }

    /// <summary>Overall trailer body exterior width (inches).</summary>
    public required double BodyWidthIn { get; init; }

    /// <summary>Interior height, for reference/display only (inches).</summary>
    public double? InteriorHeightIn { get; init; }

    /// <summary>Interior width, for reference/display and cargo-bounds sanity checks (inches).</summary>
    public double? InteriorWidthIn { get; init; }

    /// <summary>Distance from the hitch ball to the trailer body's front wall (inches).</summary>
    public required double TongueLengthIn { get; init; }

    /// <summary>Height of the trailer floor above the ground (inches). Used only when tilt != 0.</summary>
    public required double FloorHeightAboveGroundIn { get; init; }

    /// <summary>Height of the hitch ball/coupler above the ground (inches). Used only when tilt != 0.</summary>
    public required double BallHeightAboveGroundIn { get; init; }

    /// <summary>
    /// Height of the bare trailer structure's center of gravity above the floor (inches). Used
    /// only when tilt != 0 - at level (tilt = 0) it drops out of the calculation entirely.
    /// </summary>
    public required double DryCgHeightAboveFloorIn { get; init; }

    /// <summary>Gross Vehicle Weight Rating (lb) - max total weight of trailer + cargo + equipment.</summary>
    public required double GvwrLb { get; init; }

    /// <summary>Gross Axle Weight Rating (lb), combined across all axles.</summary>
    public required double GawrLb { get; init; }

    /// <summary>Factory dry/shipping weight (lb), before any cargo or aftermarket equipment.</summary>
    public required double DryWeightLb { get; init; }

    /// <summary>
    /// Manufacturer-published dry (empty) tongue weight (lb), used to derive the dry structure's
    /// effective center-of-gravity X position. Falls back to an industry-typical 12% of dry
    /// weight for a single-axle travel trailer if not known.
    /// </summary>
    public double? FactoryTongueWeightLb { get; init; }

    /// <summary>Advisory ceiling for tongue weight (lb), e.g. hitch/coupler rating. Optional.</summary>
    public double? MaxTongueWeightLb { get; init; }

    public required IReadOnlyList<AxleSpec> Axles { get; init; }

    /// <summary>Optional/aftermarket equipment bundled with this profile (e.g. rooftop A/C).</summary>
    public IReadOnlyList<EquipmentItem> Equipment { get; init; } = [];

    /// <summary>
    /// Default ground-tilt angle (degrees, positive = nose-down/tongue-low) assumed for this
    /// trailer when the user hasn't overridden it. Travel trailers are towed and stored level,
    /// so this defaults to 0.
    /// </summary>
    public double DefaultTiltDeg { get; init; } = 0;

    public double TiltMinDeg { get; init; } = -12;
    public double TiltMaxDeg { get; init; } = 12;

    /// <summary>Usable floor area for cargo placement, in the same coordinate frame. Optional.</summary>
    public CargoBounds? CargoBounds { get; init; }
}

public sealed class CargoBounds
{
    public required double XMinIn { get; init; }
    public required double XMaxIn { get; init; }
    public required double YMinIn { get; init; }
    public required double YMaxIn { get; init; }
}
