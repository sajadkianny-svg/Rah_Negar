namespace Rah_Negar.Core.Reporting.Projection;

/// <summary>A deterministic, side-effect-free calculation boundary. Implementations must not perform IO or read a clock.</summary>
public interface IReportCalculator
{
    ReportProjection Calculate(NormalizedReportInput input);
}

