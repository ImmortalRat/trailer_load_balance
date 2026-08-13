namespace TrailerLoadBalance.Web.Models;

/// <summary>
/// A fixed-position piece of equipment bundled with a trailer profile (e.g. a rooftop A/C).
/// Weight is configurable by the user at runtime; position is fixed by the profile since it
/// describes a physically installed component. Unlike <see cref="CargoItem"/>, X/Y/Z here
/// describe the item's center of mass directly (there is no separate "footprint" to anchor from).
/// </summary>
public sealed class EquipmentItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>Center-of-mass position along trailer length, from hitch ball (inches).</summary>
    public required double XIn { get; init; }

    /// <summary>Center-of-mass position across trailer width, from centerline (inches, + = right/passenger side).</summary>
    public double YIn { get; init; } = 0;

    /// <summary>Center-of-mass height above the trailer floor (inches). Roof-mounted items sit near roof height.</summary>
    public required double ZIn { get; init; }

    /// <summary>Default estimated weight (lb). Editable by the user since actual units vary.</summary>
    public required double DefaultWeightLb { get; init; }

    public bool WeightIsConfigurable { get; init; } = true;
    public double MinWeightLb { get; init; } = 0;
    public double MaxWeightLb { get; init; } = 500;

    /// <summary>
    /// Whether this equipment's weight is already baked into the profile's DryWeightLb.
    /// False for aftermarket add-ons (e.g. a roof A/C not present when the trailer was weighed
    /// at the factory) - those are added on top of dry weight instead.
    /// </summary>
    public bool IsIncludedInDryWeight { get; init; }

    /// <summary>Whether this equipment is assumed present unless the user removes it.</summary>
    public bool IsInstalledByDefault { get; init; } = true;

    /// <summary>Optional free-text note shown in the UI (e.g. explaining an estimated weight).</summary>
    public string? Notes { get; init; }
}
