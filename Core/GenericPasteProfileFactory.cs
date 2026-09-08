using Rah_Negar.Foundation.Application.Provisioning;

namespace Rah_Negar.Core;

public static class GenericPasteProfileFactory
{
    public static PasteProfile Create(int unitCount)
        => Create(CanonicalProfileDefinition.Create(GenericProfileIdentity.Create(unitCount), unitCount));

    public static PasteProfile Create(CanonicalProfileDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        int unitCount = definition.UnitCount;

        int gridUnitStart = definition.HasLinePressureColumns ? 6 : 3;
        List<int> statusColumns = [];
        List<int> numericColumns = [0, 1];
        for (int unit = 0; unit < unitCount; unit++)
        {
            statusColumns.Add(gridUnitStart + unit * 2);
            numericColumns.Add(gridUnitStart + unit * 2 + 1);
        }

        int firstSummaryColumn = gridUnitStart + unitCount * 2;
        numericColumns.AddRange(Enumerable.Range(firstSummaryColumn, 6));

        return new PasteProfile
        {
            ExpectedRows = 12,
            ExpectedColumns = gridUnitStart - 1 + unitCount * 2 + 6,
            GridStartColumn = 1,
            HourGridColumnIndex = 0,
            AverageRowIndex = 12,
            StatusSourceColumns = statusColumns,
            NumericSourceColumns = numericColumns,
            AllowedStatuses = ["S", "M", "A", "OH"],
            RatioSourceInGridColumn = 1,
            RatioSourceOutGridColumn = 2,
            RatioTargetGridColumn = firstSummaryColumn + 5,
            FlowGridColumn = firstSummaryColumn + 1,
            UnitStatusGridColumns = statusColumns,
            AverageGridColumns = numericColumns
        };
    }
}
