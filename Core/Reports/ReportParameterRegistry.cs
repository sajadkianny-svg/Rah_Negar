using Rah_Negar.Models.Reports;

namespace Rah_Negar.Core.Reports;

/// <summary>
/// رجیستری مرکزی پارامترهای قابل استفاده در گزارش‌گیری.
/// این کلاس مشخص می‌کند هر ایستگاه چه پارامترهایی دارد
/// و هر پارامتر از کدام ستون دیتابیس خوانده می‌شود.
/// </summary>
public static class ReportParameterRegistry
{
    /// <summary>
    /// بر اساس نام ایستگاه، پارامترهای قابل گزارش همان ایستگاه را برمی‌گرداند.
    /// </summary>
    public static IReadOnlyList<ReportParameterDefinition> GetParameters(string stationName)
    {
        return stationName switch
        {
            "Rasht Station" => GetRashtParameters(),
            "Ramsar Station" => GetRamsarParameters(),
            _ => throw new NotSupportedException("پارامترهای گزارش برای این ایستگاه پشتیبانی نمی‌شود.")
        };
    }

    /// <summary>
    /// پارامترهای قابل گزارش برای Rasht Station.
    /// </summary>
    public static IReadOnlyList<ReportParameterDefinition> GetRashtParameters()
    {
        return
        [
            // Pressure
            CreateDataParameter("in_p", "Inlet Press", ReportParameterCategory.Pressure, "in_p"),
            CreateDataParameter("out_p", "Outlet Press", ReportParameterCategory.Pressure, "out_p"),
            CreateDataParameter("line_f_p", "FirstLine  Press", ReportParameterCategory.Pressure, "line_f_p"),
            CreateDataParameter("line40_p", "40in Press", ReportParameterCategory.Pressure, "line40_p"),
            CreateDataParameter("line30_p", "30in Press", ReportParameterCategory.Pressure, "line30_p"),

            // Flow / Ratio / Recycle
            CreateDataParameter("ratio", "Ratio", ReportParameterCategory.Ratio, "ratio"),
            CreateDataParameter("flow", "GasFlow", ReportParameterCategory.Flow, "flow"),
            CreateDataParameter("rec", "Recycle", ReportParameterCategory.Recycle, "rec"),

            // Temperature
            CreateDataParameter("in_t", "Inlet Temp", ReportParameterCategory.Temperature, "in_t"),
            CreateDataParameter("out_t", "Outlet Temp", ReportParameterCategory.Temperature, "out_t"),
            CreateDataParameter("amb_t", "Ambient Temp", ReportParameterCategory.Temperature, "amb_t"),

            // Unit 1
            CreateStatusParameter("u1_st", "Unit 1 Status", "u1_st"),
            CreateDataParameter("u1_rpm", "Unit 1 RPM", ReportParameterCategory.RPM, "u1_rpm"),

            // Unit 2
            CreateStatusParameter("u2_st", "Unit 2 Status", "u2_st"),
            CreateDataParameter("u2_rpm", "Unit 2 RPM", ReportParameterCategory.RPM, "u2_rpm"),

            // Unit 3
            CreateStatusParameter("u3_st", "Unit 3 Status", "u3_st"),
            CreateDataParameter("u3_rpm", "Unit 3 RPM", ReportParameterCategory.RPM, "u3_rpm"),

            // Unique / Daily
            CreateUniqueParameter("ir_f", "Gas Generator Fuel", ReportParameterCategory.Fuel, "ir_f"),
            CreateUniqueParameter("turbine_fuel", "Turbine Fuel", ReportParameterCategory.Fuel, "turbine_fuel"),
            CreateUniqueParameter("turbine_flow", "Turbine Flow", ReportParameterCategory.Flow, "turbine_flow"),
            CreateUniqueParameter("non_turbine_flow", "Non-Turbine Flow", ReportParameterCategory.Flow, "non_turbine_flow"),
            CreateUniqueParameter("vent", "Vent", ReportParameterCategory.Flow, "vent")
        ];
    }

    /// <summary>
    /// پارامترهای قابل گزارش برای Ramsar Station.
    /// </summary>
    public static IReadOnlyList<ReportParameterDefinition> GetRamsarParameters()
    {
        return
        [
            // Pressure
            CreateDataParameter("in_p", "Inlet Press", ReportParameterCategory.Pressure, "in_p"),
            CreateDataParameter("out_p", "Outlet Press", ReportParameterCategory.Pressure, "out_p"),

            // Flow / Ratio / Recycle
            CreateDataParameter("flow", "GasFlow", ReportParameterCategory.Flow, "flow"),
            CreateDataParameter("rec", "Recycle", ReportParameterCategory.Recycle, "rec"),
            CreateDataParameter("ratio", "Compression Ratio", ReportParameterCategory.Ratio, "ratio"),

             // Temperature
            CreateDataParameter("in_t", "Inlet Temp", ReportParameterCategory.Temperature, "in_t"),
            CreateDataParameter("out_t", "Outlet Temp", ReportParameterCategory.Temperature, "out_t"),
            CreateDataParameter("amb_t", "Ambient Temp", ReportParameterCategory.Temperature, "amb_t"),

            // Unit 1
            CreateStatusParameter("u1_st", "Unit 1 Status", "u1_st"),
            CreateDataParameter("u1_rpm", "Unit 1 RPM", ReportParameterCategory.RPM, "u1_rpm"),

            // Unit 2
            CreateStatusParameter("u2_st", "Unit 2 Status", "u2_st"),
            CreateDataParameter("u2_rpm", "Unit 2 RPM", ReportParameterCategory.RPM, "u2_rpm"),

            // Unit 3
            CreateStatusParameter("u3_st", "Unit 3 Status", "u3_st"),
            CreateDataParameter("u3_rpm", "Unit 3 RPM", ReportParameterCategory.RPM, "u3_rpm"),

            // Unit 4
            CreateStatusParameter("u4_st", "Unit 4 Status", "u4_st"),
            CreateDataParameter("u4_rpm", "Unit 4 RPM", ReportParameterCategory.RPM, "u4_rpm"),

            // Unique / Daily
            CreateUniqueParameter("ir_f", "Gas Generator Fuel", ReportParameterCategory.Fuel, "ir_f"),
            CreateUniqueParameter("turbine_fuel", "Turbine Fuel", ReportParameterCategory.Fuel, "turbine_fuel"),
            CreateUniqueParameter("turbine_flow", "Turbine Flow", ReportParameterCategory.Flow, "turbine_flow"),
            CreateUniqueParameter("non_turbine_flow", "Non-Turbine Flow", ReportParameterCategory.Flow, "non_turbine_flow"),
            CreateUniqueParameter("vent", "Vent", ReportParameterCategory.Flow, "vent")
        ];
    }

    /// <summary>
    /// ساخت پارامتر عددی مربوط به tbl_data.
    /// </summary>
    private static ReportParameterDefinition CreateDataParameter(string key,string displayName,ReportParameterCategory category,string columnName)
    {
        return new ReportParameterDefinition
        {
            Key = key,
            DisplayName = displayName,
            Category = category,
            DataColumnName = columnName,
            SupportedAggregations =
            [
                ReportAggregationType.Min,
                ReportAggregationType.Max,
                ReportAggregationType.Avg
            ]
        };
    }

    /// <summary>
    /// ساخت پارامتر وضعیت واحدها.
    /// این نوع پارامتر عددی نیست و در محاسبات Min / Max / Avg شرکت نمی‌کند.
    /// </summary>
    private static ReportParameterDefinition CreateStatusParameter(string key,string displayName,string columnName)
    {
        return new ReportParameterDefinition
        {
            Key = key,
            DisplayName = displayName,
            Category = ReportParameterCategory.Status,
            DataColumnName = columnName,
            SupportedAggregations = []
        };
    }

    /// <summary>
    /// ساخت پارامتر تجمیعی مربوط به tbl_unique.
    /// </summary>
    private static ReportParameterDefinition CreateUniqueParameter(string key,string displayName,ReportParameterCategory category,string columnName)
    {
        return new ReportParameterDefinition
        {
            Key = key,
            DisplayName = displayName,
            Category = category,
            UniqueColumnName = columnName,
            SupportedAggregations = [ReportAggregationType.Sum],
            IsCumulative = true
        };
    }
}

