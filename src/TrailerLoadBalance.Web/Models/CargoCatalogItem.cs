namespace TrailerLoadBalance.Web.Models;

/// <summary>
/// An entry in the stock cargo palette shown to the user for dragging onto the trailer.
/// Today only the generic customizable box exists; this type exists so the UI already has a
/// place to list future predefined types (with icons, fixed dimensions and default weight).
/// </summary>
public sealed class CargoCatalogItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? IconEmoji { get; init; }
    public required double DefaultLengthIn { get; init; }
    public required double DefaultWidthIn { get; init; }
    public double DefaultHeightIn { get; init; } = CargoDefaults.DefaultHeightIn;
    public required double DefaultWeightLb { get; init; }
    public required string DefaultColor { get; init; }
    public bool IsCustomizable { get; init; }
}
