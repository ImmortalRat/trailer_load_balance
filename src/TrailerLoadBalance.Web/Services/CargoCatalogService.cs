using TrailerLoadBalance.Web.Models;

namespace TrailerLoadBalance.Web.Services;

/// <summary>
/// Stock cargo palette shown in the UI. Today it only holds the generic customizable box;
/// this service exists as the seam where future predefined cargo types (coolers, totes,
/// bikes, etc. with icons/fixed dimensions/default weight) get added without touching the UI.
/// </summary>
public sealed class CargoCatalogService
{
    private readonly IReadOnlyList<CargoCatalogItem> _items =
    [
        new CargoCatalogItem
        {
            Id = "generic-box",
            Name = "Custom Box",
            IconEmoji = "📦",
            DefaultLengthIn = 24,
            DefaultWidthIn = 18,
            DefaultHeightIn = CargoDefaults.DefaultHeightIn,
            DefaultWeightLb = 25,
            DefaultColor = "#4f8ef7",
            IsCustomizable = true,
        },
    ];

    public IReadOnlyList<CargoCatalogItem> GetAll() => _items;
}
