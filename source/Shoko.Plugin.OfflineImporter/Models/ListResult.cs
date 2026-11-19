using System.Collections.Generic;
using System.Linq;

namespace Shoko.Plugin.OfflineImporter.Models;

/// <summary>
/// A page of a list, with the size of the whole list, shaped as the server's
/// APIv3 lists are.
/// </summary>
/// <typeparam name="T">The type of the entries.</typeparam>
public class ListResult<T>
{
    /// <summary>
    /// How many entries the whole list has.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// The entries on the page.
    /// </summary>
    public List<T> List { get; init; } = [];

    /// <summary>
    /// Takes a page of a list.
    /// </summary>
    /// <param name="list">The whole list.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="pageSize">The page size, or <c>0</c> for the whole list on one page.</param>
    /// <returns>The page.</returns>
    public static ListResult<T> FromPage(IReadOnlyList<T> list, int page, int pageSize) => new()
    {
        Total = list.Count,
        List = pageSize <= 0 ? [.. list] : list.Skip(pageSize * (page - 1)).Take(pageSize).ToList(),
    };
}
