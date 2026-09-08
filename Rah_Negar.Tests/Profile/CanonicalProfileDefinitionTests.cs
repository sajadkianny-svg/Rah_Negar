using Rah_Negar.Core;
using Rah_Negar.Foundation.Application.Provisioning;

namespace Rah_Negar.Tests.Profile;

public sealed class CanonicalProfileDefinitionTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Canonical_profile_accepts_only_supported_unit_counts(int unitCount)
    {
        CanonicalProfileDefinition profile = CanonicalProfileDefinition.Create("ایستگاه آزمایشی", unitCount);
        Assert.Equal(unitCount, profile.UnitCount);
        Assert.Equal("ایستگاه آزمایشی", profile.StationName);
        Assert.False(string.IsNullOrWhiteSpace(profile.ProfileId));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(35)]
    public void Canonical_profile_rejects_unsupported_unit_counts(int unitCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalProfileDefinition.Create("ایستگاه آزمایشی", unitCount));
        Assert.False(TargetStationProfileRules.IsUnitCountSupported(unitCount));
    }

    [Fact]
    public void Supported_optional_parameters_are_explicit_and_do_not_create_arbitrary_columns()
    {
        CanonicalProfileDefinition profile = CanonicalProfileDefinition.Create(
            "ایستگاه شمالی", 5, ["line_f_p", "not_a_column"]);

        Assert.Equal(["line_f_p"], profile.OptionalParameters);
        Assert.True(profile.Supports("in_p"));
        Assert.True(profile.Supports("line_f_p"));
        Assert.False(profile.Supports("not_a_column"));
    }

    [Fact]
    public void Canonical_definition_drives_unit_grid_and_report_shapes()
    {
        CanonicalProfileDefinition profile = CanonicalProfileDefinition.Create("ایستگاه مرکزی", 5);
        GridProfile grid = GenericGridProfileFactory.Create(profile);

        Assert.Equal(5, grid.Columns.Count(x => x.Name.EndsWith("_st", StringComparison.Ordinal)));
        Assert.Equal(5, grid.Columns.Count(x => x.Name.EndsWith("_rpm", StringComparison.Ordinal)));
        Assert.Equal(19, grid.Columns.Count);
        Assert.Equal(12, grid.Visual.DataRowCount);
    }
}
