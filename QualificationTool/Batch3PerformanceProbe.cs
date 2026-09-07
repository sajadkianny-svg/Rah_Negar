using Rah_Negar.Core;
using Rah_Negar.Utils;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Forms;

namespace Rah_Negar.Qualification;

internal static class Batch3PerformanceProbe
{
    public static void Run(string evidencePath)
    {
        string output = Path.GetFullPath(evidencePath);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        Dictionary<string, object?> evidence = new();

        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                DataGridViewDefinitionCache.ClearForTests();
                GridProfile profile = GridProfileProvider.GetProfile("Rasht Station");
                DataGridViewColumnDefinition[] definitions = profile.Columns
                    .Select((column, index) => new DataGridViewColumnDefinition(
                        column.Name, column.HeaderText, column.Width, column.ReadOnly,
                        column.Alignment, index % 2 == 0
                            ? profile.Visual.AlternateBackColor1
                            : profile.Visual.AlternateBackColor2))
                    .ToArray();
                const string key = "qualification-performance-records";
                using DataGridView grid = new();
                Stopwatch first = Stopwatch.StartNew();
                DataGridViewDefinitionCache.EnsureColumns(grid, key, definitions);
                first.Stop();

                Stopwatch repeated = Stopwatch.StartNew();
                for (int i = 0; i < 1000; i++)
                    DataGridViewDefinitionCache.EnsureColumns(grid, key, definitions);
                repeated.Stop();

                (long hits, long misses, int count) = DataGridViewDefinitionCache.GetStatistics();
                evidence["gridProfile"] = "Rasht Station";
                evidence["iterations"] = 1000;
                evidence["columnCount"] = definitions.Length;
                evidence["firstBuildMilliseconds"] = first.Elapsed.TotalMilliseconds;
                evidence["repeatedEnsureMilliseconds"] = repeated.Elapsed.TotalMilliseconds;
                evidence["cacheHits"] = hits;
                evidence["cacheMisses"] = misses;
                evidence["cachedDefinitionCount"] = count;
                evidence["rebuildInvariant"] = misses == 1 && grid.Columns.Count == definitions.Length;
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
            throw new AggregateException(failure);

        evidence["generatedUtc"] = DateTimeOffset.UtcNow;
        evidence["result"] = evidence["rebuildInvariant"] is true ? "PASS" : "FAIL";
        File.WriteAllText(output, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Batch 3 performance probe: {evidence["result"]}; evidence={output}");
    }
}
