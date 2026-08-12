namespace TrailerLoadBalance.Web.Models;

public sealed class AxleLoadResult
{
    public required string AxleId { get; init; }
    public required double LoadLb { get; init; }
    public required double RatingLb { get; init; }
    public double PercentOfRating => RatingLb > 0 ? LoadLb / RatingLb * 100.0 : 0;
    public bool IsOverRating => LoadLb > RatingLb;
}

public sealed class LoadCalculationResult
{
    public required double TongueWeightLb { get; init; }
    public required double TongueWeightPercentOfTotal { get; init; }
    public required double TotalWeightLb { get; init; }
    public required double GvwrLb { get; init; }
    public bool IsOverGvwr => TotalWeightLb > GvwrLb;

    public required double TotalAxleLoadLb { get; init; }
    public required double GawrLb { get; init; }
    public bool IsOverGawr => TotalAxleLoadLb > GawrLb;

    public required IReadOnlyList<AxleLoadResult> AxleLoads { get; init; }

    public double? MaxTongueWeightLb { get; init; }
    public bool IsOverMaxTongueWeight => MaxTongueWeightLb.HasValue && TongueWeightLb > MaxTongueWeightLb.Value;

    /// <summary>True if the tongue would lift (zero or negative tongue weight) - the trailer would tip back.</summary>
    public bool IsTongueLifting => TongueWeightLb <= 0;

    public required double EffectiveTiltDeg { get; init; }

    public double RemainingCapacityLb => Math.Max(0, GvwrLb - TotalWeightLb);

    /// <summary>Recommended tongue weight range as % of total, per common towing guidance (10-15%).</summary>
    public bool IsTongueWeightPercentInRecommendedRange =>
        TongueWeightPercentOfTotal is >= 10 and <= 15;
}
