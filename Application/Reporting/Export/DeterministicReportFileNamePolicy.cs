using System.Text;

namespace Rah_Negar.Foundation.Application.Reporting.Export;

public sealed class DeterministicReportFileNamePolicy : IReportFileNamePolicy
{
    public string Create(FinalizedReportExportModel model, ReportExportFormat format)
    {
        ArgumentNullException.ThrowIfNull(model);
        string extension = format == ReportExportFormat.Pdf ? ".pdf" : ".xlsx";
        return string.Join('_', Sanitize(model.StationId), Sanitize(model.PersianPeriodLabel),
            model.PeriodKind.ToString(), Sanitize(model.SchemaVersion)) + extension;
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
