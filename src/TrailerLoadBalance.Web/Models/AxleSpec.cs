namespace TrailerLoadBalance.Web.Models;

/// <summary>A single physical axle on the trailer.</summary>
public sealed class AxleSpec
{
    public required string Id { get; init; }

    /// <summary>Distance from the hitch ball to this axle's centerline (inches).</summary>
    public required double PositionFromHitchIn { get; init; }

    /// <summary>Weight rating for this individual axle (lb).</summary>
    public required double RatingLb { get; init; }
}
