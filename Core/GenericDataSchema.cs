using Rah_Negar.Foundation.Application.Provisioning;

namespace Rah_Negar.Core;

public sealed class GenericDataSchema : IStationDataSchema
{
    private readonly CanonicalProfileDefinition _definition;

    public GenericDataSchema(int unitCount)
        : this(CanonicalProfileDefinition.Create(GenericProfileIdentity.Create(unitCount), unitCount))
    {
    }

    public GenericDataSchema(CanonicalProfileDefinition definition)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        UnitCount = definition.UnitCount;
    }

    public int UnitCount { get; }
    public string StationName => _definition.StationName;

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

        if (_definition.HasLinePressureColumns)
        {
            columns.Insert(5, "line_f_p REAL NOT NULL");
            columns.Insert(6, "line40_p REAL NOT NULL");
            columns.Insert(7, "line30_p REAL NOT NULL");
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
