using TrailerLoadBalance.Web.Models;
using TrailerLoadBalance.Web.Services;
using Xunit;

namespace TrailerLoadBalance.Tests;

public class LoadCalculationServiceTests
{
    private static TrailerProfile BuildM15Bh() => new()
    {
        Id = "coleman-dutchmen-2012-m15bh",
        Name = "2012 Coleman by Dutchmen M-15BH",
        Manufacturer = "Dutchmen",
        Model = "M-15BH",
        BodyLengthIn = 180,
        BodyWidthIn = 96,
        TongueLengthIn = 45,
        FloorHeightAboveGroundIn = 22,
        BallHeightAboveGroundIn = 18,
        DryCgHeightAboveFloorIn = 19,
        GvwrLb = 3800,
        GawrLb = 3500,
        DryWeightLb = 2715,
        FactoryTongueWeightLb = 317,
        MaxTongueWeightLb = 500,
        Axles = [new AxleSpec { Id = "axle-1", PositionFromHitchIn = 159, RatingLb = 3500 }],
        Equipment =
        [
            new EquipmentItem
            {
                Id = "roof-ac",
                Name = "Rooftop A/C",
                XIn = 80,
                YIn = 0,
                ZIn = 88,
                DefaultWeightLb = 95,
                IsIncludedInDryWeight = false,
                IsInstalledByDefault = true,
            },
        ],
    };

    private static readonly Dictionary<string, double> NoOverrides = [];
    private static readonly Dictionary<string, bool> DefaultInstalled = [];
    private static readonly List<CargoItem> NoCargo = [];

    [Fact]
    public void DryTongueWeight_AtLevel_MatchesFactorySpec()
    {
        var profile = BuildM15Bh();
        var service = new LoadCalculationService();

        // AC not installed, no cargo: reproduces the manufacturer's published dry tongue weight exactly.
        var installed = new Dictionary<string, bool> { ["roof-ac"] = false };
        var result = service.Calculate(profile, NoCargo, NoOverrides, installed, tiltDeg: 0);

        Assert.Equal(317.0, result.TongueWeightLb, precision: 0);
        Assert.Equal(2715.0, result.TotalWeightLb, precision: 0);
    }

    [Theory]
    [InlineData(-10, 233.8, 8.3, 2576.2)]
    [InlineData(0, 364.2, 12.96, 2445.8)]
    [InlineData(6, 439.5, 15.6, 2370.5)]
    [InlineData(10, 489.5, 17.4, 2320.5)]
    public void TongueAndAxleWeight_AcrossTiltRange_MatchesDerivedValues(
        double tiltDeg, double expectedTongueLb, double expectedPct, double expectedAxleLb)
    {
        var profile = BuildM15Bh();
        var service = new LoadCalculationService();

        var result = service.Calculate(profile, NoCargo, NoOverrides, DefaultInstalled, tiltDeg);

        Assert.True(Math.Abs(result.TongueWeightLb - expectedTongueLb) < 0.5,
            $"tongue weight {result.TongueWeightLb} vs expected {expectedTongueLb}");
        Assert.True(Math.Abs(result.TongueWeightPercentOfTotal - expectedPct) < 0.2,
            $"tongue % {result.TongueWeightPercentOfTotal} vs expected {expectedPct}");
        Assert.True(Math.Abs(result.TotalAxleLoadLb - expectedAxleLb) < 0.5,
            $"axle load {result.TotalAxleLoadLb} vs expected {expectedAxleLb}");
        Assert.Equal(2810.0, result.TotalWeightLb, precision: 0);
    }

    [Fact]
    public void AtLevelTilt_HeightOfMassesDoesNotAffectResult()
    {
        // At tilt = 0 the model must collapse to the flat 2D calc: moving a mass's Z should
        // have zero effect on tongue/axle weight.
        var profile = BuildM15Bh();
        var service = new LoadCalculationService();

        var lowCargo = new List<CargoItem>
        {
            new() { Id = "a", Name = "Box", Color = "#fff", XIn = 100, YIn = 0, LengthIn = 10, WidthIn = 10, HeightIn = 12, ZIn = 0, WeightLb = 50 },
        };
        var highCargo = new List<CargoItem>
        {
            new() { Id = "a", Name = "Box", Color = "#fff", XIn = 100, YIn = 0, LengthIn = 10, WidthIn = 10, HeightIn = 12, ZIn = 60, WeightLb = 50 },
        };

        var resultLow = service.Calculate(profile, lowCargo, NoOverrides, DefaultInstalled, tiltDeg: 0);
        var resultHigh = service.Calculate(profile, highCargo, NoOverrides, DefaultInstalled, tiltDeg: 0);

        Assert.Equal(resultLow.TongueWeightLb, resultHigh.TongueWeightLb, precision: 6);
    }

    [Fact]
    public void OffLevelTilt_HigherMassShiftsTongueWeightMore()
    {
        // Off level, a taller item's height must matter (more so than a low one), per the
        // rigid-body rotation model - this is the whole reason the app stores Z.
        var profile = BuildM15Bh();
        var service = new LoadCalculationService();

        var lowCargo = new List<CargoItem>
        {
            new() { Id = "a", Name = "Box", Color = "#fff", XIn = 100, YIn = 0, LengthIn = 10, WidthIn = 10, HeightIn = 12, ZIn = 0, WeightLb = 50 },
        };
        var highCargo = new List<CargoItem>
        {
            new() { Id = "a", Name = "Box", Color = "#fff", XIn = 100, YIn = 0, LengthIn = 10, WidthIn = 10, HeightIn = 12, ZIn = 60, WeightLb = 50 },
        };

        var resultLow = service.Calculate(profile, lowCargo, NoOverrides, DefaultInstalled, tiltDeg: 6);
        var resultHigh = service.Calculate(profile, highCargo, NoOverrides, DefaultInstalled, tiltDeg: 6);

        Assert.True(resultHigh.TongueWeightLb > resultLow.TongueWeightLb);
    }

    [Fact]
    public void UninstalledEquipment_IsExcludedFromTotals()
    {
        var profile = BuildM15Bh();
        var service = new LoadCalculationService();

        var installed = service.Calculate(profile, NoCargo, NoOverrides, new Dictionary<string, bool> { ["roof-ac"] = true }, 0);
        var notInstalled = service.Calculate(profile, NoCargo, NoOverrides, new Dictionary<string, bool> { ["roof-ac"] = false }, 0);

        Assert.Equal(95.0, installed.TotalWeightLb - notInstalled.TotalWeightLb, precision: 0);
    }

    [Fact]
    public void TwoAxleProfile_SplitsLoadProportionalToRating()
    {
        var profile = new TrailerProfile
        {
            Id = "test-tandem",
            Name = "Test Tandem",
            Manufacturer = "Test",
            Model = "Tandem",
            BodyLengthIn = 240,
            BodyWidthIn = 96,
            TongueLengthIn = 48,
            FloorHeightAboveGroundIn = 22,
            BallHeightAboveGroundIn = 18,
            DryCgHeightAboveFloorIn = 19,
            GvwrLb = 7000,
            GawrLb = 7000,
            DryWeightLb = 4500,
            FactoryTongueWeightLb = 540,
            Axles =
            [
                new AxleSpec { Id = "front", PositionFromHitchIn = 190, RatingLb = 3500 },
                new AxleSpec { Id = "rear", PositionFromHitchIn = 220, RatingLb = 3500 },
            ],
        };
        var service = new LoadCalculationService();

        var result = service.Calculate(profile, NoCargo, NoOverrides, DefaultInstalled, tiltDeg: 0);

        Assert.Equal(2, result.AxleLoads.Count);
        // Equal ratings -> equal split (industry-standard equalizer assumption).
        Assert.Equal(result.AxleLoads[0].LoadLb, result.AxleLoads[1].LoadLb, precision: 6);
        Assert.Equal(result.TotalAxleLoadLb, result.AxleLoads[0].LoadLb + result.AxleLoads[1].LoadLb, precision: 6);
    }
}
