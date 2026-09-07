using Rah_Negar.Foundation.Application.Provisioning;

namespace Rah_Negar.Core;

public sealed class GenericDataSchema : IStationDataSchema
{
    public GenericDataSchema(int unitCount)
    {
        if (!TargetStationProfileRules.IsUnitCountSupported(unitCount))
            throw new ArgumentOutOfRangeException(nameof(unitCount));

        UnitCount = unitCount;
    }

    public int UnitCount { get; }
    public string StationName => GenericProfileIdentity.Create(UnitCount);

    public string GetCreateTableSql()
    {
        List<string> columns =
        [
            "id INTEGER PRIMARY KEY AUTOINCREMENT",
            "date_rep INTEGER NOT NULL",
            "time_rep TEXT NOT NULL",
            "in_p REAL NOT NULL",
            "out_p REAL NOT NULL"
        ];

        for (int unit = 1; unit <= UnitCount; unit++)
        {
            columns.Add($"u{unit}_st TEXT NOT NULL");
            columns.Add($"u{unit}_rpm INTEGER NOT NULL");
        }

        columns.AddRange([
            "rec REAL NOT NULL",
            "flow REAL NOT NULL",
            "in_t REAL NOT NULL",
            "out_t REAL NOT NULL",
            "amb_t REAL NOT NULL",
            "ratio REAL NOT NULL"]);

        return $"CREATE TABLE IF NOT EXISTS tbl_data ({string.Join(",", columns)});";
    }

    public List<string> GetIndexSqlList() =>
    ["CREATE INDEX IF NOT EXISTS idx_tbl_data_date_time ON tbl_data(date_rep, time_rep);"];
}
