using TrailerLoadBalance.Web.Models;

namespace TrailerLoadBalance.Web.Services;

/// <summary>
/// Computes tongue weight and per-axle load from a trailer profile plus the user's placed
/// cargo/equipment, using a rigid-beam statics (moment balance) model.
///
/// Coordinate frame: X = inches from the hitch ball toward the rear, Z = inches above the
/// trailer floor. The trailer is modeled as a rigid body supported at two points: the hitch
/// coupler and the axle group (weighted average position of all axles). Both supports, and
/// every mass, have a height above the *ground* (not just the floor) because that height is
/// what interacts with tilt.
///
/// Tilt: a positive TiltDeg means the trailer is nose-down (tongue lower than the axle), the
/// normal/recommended towing and parked attitude; travel trailers are towed and stored level
/// (TiltDeg = 0) per manufacturer/hitch-maker guidance, so this only matters for off-level
/// parking scenarios (e.g. a sloped driveway). The whole body rotates about the axle group's
/// ground contact point (the wheels stay put; tongue height changes to create the tilt).
///
/// For a mass at floor-relative (x, z), its effective horizontal lever arm from the axle
/// contact patch, in the ground/world frame, is:
///     u(x, z) = (axleX - x) * cos(tilt) + (z + floorHeightAboveGround) * sin(tilt)
/// and the hitch's own lever arm is:
///     u_hitch = axleX * cos(tilt) + ballHeightAboveGround * sin(tilt)
/// This is exact rigid-body rotation math (see docs/requirements.md for the full derivation).
/// At tilt = 0 both reduce to the familiar flat "distance from axle" 2D calculation - the
/// ground-height terms only enter once the trailer is off-level, which is why cargo/equipment
/// height (and the trailer's own floor/ball height) matters only in that case.
///
/// The trailer's own dry-weight structure is modeled as a single point mass, positioned so
/// that, at level (tilt = 0), it reproduces the manufacturer's factory tongue weight if known,
/// else an industry-typical 12% of dry weight for a single-axle travel trailer.
/// </summary>
public sealed class LoadCalculationService
{
    private const double DefaultDryTongueWeightFraction = 0.12;

    public LoadCalculationResult Calculate(
        TrailerProfile profile,
        IReadOnlyList<CargoItem> cargo,
        IReadOnlyDictionary<string, double> equipmentWeightOverridesLb,
        IReadOnlyDictionary<string, bool> equipmentInstalled,
        double tiltDeg)
    {
        var axleX = WeightedAxleCentroidX(profile);
        var tiltRad = double.DegreesToRadians(tiltDeg);
        var cosT = Math.Cos(tiltRad);
        var sinT = Math.Sin(tiltRad);

        double LeverArm(double x, double zAboveFloor) =>
            (axleX - x) * cosT + (zAboveFloor + profile.FloorHeightAboveGroundIn) * sinT;

        var hitchLeverArm = axleX * cosT + profile.BallHeightAboveGroundIn * sinT;

        var (dryCgX, dryCgZ) = DryStructureCenterOfGravity(profile, axleX);

        double totalWeight = profile.DryWeightLb;
        double momentSum = profile.DryWeightLb * LeverArm(dryCgX, dryCgZ);

        foreach (var eq in profile.Equipment)
        {
            var installed = equipmentInstalled.GetValueOrDefault(eq.Id, eq.IsInstalledByDefault);
            if (!installed || eq.IsIncludedInDryWeight)
            {
                continue;
            }

            var weight = equipmentWeightOverridesLb.GetValueOrDefault(eq.Id, eq.DefaultWeightLb);
            totalWeight += weight;
            momentSum += weight * LeverArm(eq.XIn, eq.ZIn);
        }

        foreach (var item in cargo)
        {
            totalWeight += item.WeightLb;
            var centerX = item.XIn + item.LengthIn / 2.0;
            var centerZ = item.ZIn + item.HeightIn / 2.0;
            momentSum += item.WeightLb * LeverArm(centerX, centerZ);
        }

        var tongueWeight = hitchLeverArm != 0 ? momentSum / hitchLeverArm : 0;
        var totalAxleLoad = totalWeight - tongueWeight;

        var axleLoads = DistributeAxleLoad(profile, totalAxleLoad);

        return new LoadCalculationResult
        {
            TongueWeightLb = tongueWeight,
            TongueWeightPercentOfTotal = totalWeight > 0 ? tongueWeight / totalWeight * 100.0 : 0,
            TotalWeightLb = totalWeight,
            GvwrLb = profile.GvwrLb,
            TotalAxleLoadLb = totalAxleLoad,
            GawrLb = profile.GawrLb,
            AxleLoads = axleLoads,
            MaxTongueWeightLb = profile.MaxTongueWeightLb,
            EffectiveTiltDeg = tiltDeg,
        };
    }

    /// <summary>
    /// Derives the dry structure's effective (X, Z) center of gravity so that, at level, it
    /// reproduces the profile's known/estimated factory tongue weight exactly. Never stored -
    /// computed fresh so it always stays consistent with the profile's other fields.
    /// </summary>
    private static (double X, double Z) DryStructureCenterOfGravity(TrailerProfile profile, double axleX)
    {
        var tongueFractionAtLevel = profile.FactoryTongueWeightLb.HasValue && profile.DryWeightLb > 0
            ? profile.FactoryTongueWeightLb.Value / profile.DryWeightLb
            : DefaultDryTongueWeightFraction;

        var dryCgX = axleX * (1 - tongueFractionAtLevel);
        return (dryCgX, profile.DryCgHeightAboveFloorIn);
    }

    private static double WeightedAxleCentroidX(TrailerProfile profile)
    {
        var totalRating = profile.Axles.Sum(a => a.RatingLb);
        if (totalRating <= 0)
        {
            return profile.Axles.Average(a => a.PositionFromHitchIn);
        }

        return profile.Axles.Sum(a => a.PositionFromHitchIn * a.RatingLb) / totalRating;
    }

    private static IReadOnlyList<AxleLoadResult> DistributeAxleLoad(TrailerProfile profile, double totalAxleLoad)
    {
        var totalRating = profile.Axles.Sum(a => a.RatingLb);

        return profile.Axles
            .Select(a => new AxleLoadResult
            {
                AxleId = a.Id,
                LoadLb = totalRating > 0
                    ? totalAxleLoad * (a.RatingLb / totalRating)
                    : totalAxleLoad / profile.Axles.Count,
                RatingLb = a.RatingLb,
            })
            .ToList();
    }
}
