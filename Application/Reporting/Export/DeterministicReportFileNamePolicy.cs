using System.Text;

namespace Rah_Negar.Foundation.Application.Reporting.Export;

public sealed class DeterministicReportFileNamePolicy : IReportFileNamePolicy
{
    public string Create(FinalizedReportExportModel model, ReportExportFormat format)
    {
        ArgumentNullException.ThrowIfNull(model);
        return Create(model.StationId, model.PersianPeriodLabel, model.PeriodKind,
            model.SchemaVersion, format);
    }

    /// <summary>
    /// Read-model overload used by the Pilot metadata observer. It applies the same naming
    /// policy without constructing, finalizing, rendering, or persisting a report snapshot.
    /// </summary>
    public string Create(
        string stationId,
        string persianPeriodLabel,
        Core.Reporting.Projection.ReportPeriodKind periodKind,
        string schemaVersion,
        ReportExportFormat format)
    {
        string extension = format == ReportExportFormat.Pdf ? ".pdf" : ".xlsx";
        return string.Join('_', Sanitize(stationId), Sanitize(persianPeriodLabel),
            periodKind.ToString(), Sanitize(schemaVersion)) + extension;
    }

    private static string Sanitize(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (char character in value.Normalize(NormalizationForm.FormC))
            result.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-');
        string normalized = result.ToString().Trim('-');
        return normalized.Length == 0 ? "report" : normalized;
    }
}
