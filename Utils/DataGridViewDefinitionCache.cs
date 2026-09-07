using System.Collections.Concurrent;
using System.Drawing;
using System.Windows.Forms;

namespace Rah_Negar.Utils;

/// <summary>
/// Caches immutable grid column definitions while keeping DataGridView controls
/// owned by their form. A column instance cannot be shared between controls, but
/// its definition can; this avoids rebuilding the same profile metadata on every
/// form visit and makes cache reuse measurable.
/// </summary>
public sealed record DataGridViewColumnDefinition(
    string Name,
    string HeaderText,
    int Width,
    bool ReadOnly,
    DataGridViewContentAlignment Alignment,
    Color BackColor);

public static class DataGridViewDefinitionCache
{
    private static readonly ConcurrentDictionary<string, IReadOnlyList<DataGridViewColumnDefinition>> Definitions = new(StringComparer.Ordinal);
    private static long _hits;
    private static long _misses;

    public static IReadOnlyList<DataGridViewColumnDefinition> GetOrAdd(
        string key,
        IReadOnlyList<DataGridViewColumnDefinition> definitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(definitions);

        if (Definitions.TryGetValue(key, out IReadOnlyList<DataGridViewColumnDefinition>? cached))
        {
            Interlocked.Increment(ref _hits);
            return cached;
        }

        IReadOnlyList<DataGridViewColumnDefinition> immutable = definitions.ToArray();
        cached = Definitions.GetOrAdd(key, immutable);
        if (ReferenceEquals(cached, immutable))
            Interlocked.Increment(ref _misses);
        else
            Interlocked.Increment(ref _hits);
        return cached;
    }

    public static bool TryGet(string key, out IReadOnlyList<DataGridViewColumnDefinition>? definitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Definitions.TryGetValue(key, out definitions);
    }

    public static bool EnsureColumns(
        DataGridView grid,
        string key,
        IReadOnlyList<DataGridViewColumnDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(grid);
        IReadOnlyList<DataGridViewColumnDefinition> cached = GetOrAdd(key, definitions);

        if (grid.Columns.Count == cached.Count &&
            grid.Columns.Cast<DataGridViewColumn>().Select((column, index) =>
                column.Name == cached[index].Name && column.HeaderText == cached[index].HeaderText)
                .All(match => match))
        {
            return false;
        }

        grid.Columns.Clear();
        foreach (DataGridViewColumnDefinition definition in cached)
        {
            DataGridViewTextBoxColumn column = new()
            {
                Name = definition.Name,
                HeaderText = definition.HeaderText,
                Width = definition.Width,
                ReadOnly = definition.ReadOnly,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                MinimumWidth = 25
            };
            column.DefaultCellStyle.Alignment = definition.Alignment;
            column.DefaultCellStyle.BackColor = definition.BackColor;
            grid.Columns.Add(column);
        }

        return true;
    }

    public static (long Hits, long Misses, int DefinitionCount) GetStatistics() =>
        (Interlocked.Read(ref _hits), Interlocked.Read(ref _misses), Definitions.Count);

    public static void ClearForTests()
    {
        Definitions.Clear();
        Interlocked.Exchange(ref _hits, 0);
        Interlocked.Exchange(ref _misses, 0);
    }
}
