using System.Text.RegularExpressions;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Plugin.OfflineImporter.Abstractions.Models;
using Xunit;

namespace Shoko.Plugin.OfflineImporter.Tests;

/// <summary>
/// Checks the AniDB values the importer takes from ID tags against what its
/// old patterns, kept here as they were, used to find.
/// </summary>
public partial class AnidbTagParityTests
{
    [GeneratedRegex(@"\[\s*(?<anidb>anidb(?:[\- \.]?\s*ids?)?)[\-= ](?:(?<=\k<anidb>[\-= ]|,)\s*(?:(?<episodeRange>(?:a?\d+(?:[=\- \.]|(?=e)))?e?\d+(?:['@]\d+(?:\-\d+)?%?)?)|a(?<animeId>\d+))\s*(?=\]|,),?)+\]|\(\s*(?<anidb>anidb(?:[\- \.]?\s*ids?)?)[\-= ](?:(?<=\k<anidb>[\-= ]|,)\s*(?:(?<episodeRange>(?:a?\d+(?:[=\- \.]|(?=e)))?e?\d+(?:['@]\d+(?:\-\d+)?%?)?)|a(?<animeId>\d+))\s*(?=\)|,),?)+\)|\{\s*(?<anidb>anidb(?:[\- \.]?\s*ids?)?)[\-= ](?:(?<=\k<anidb>[\-= ]|,)\s*(?:(?<episodeRange>(?:a?\d+(?:[=\- \.]|(?=e)))?e?\d+(?:['@]\d+(?:\-\d+)?%?)?)|a(?<animeId>\d+))\s*(?=\}|,),?)+\}", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex OldFileRegex();

    [GeneratedRegex(@"\[(?<anidb>anidb(?:[\- \.]?id)?)[\-= ](?:(?<=\k<anidb>[\-= ]|,)\s*(?<animeId>\d+)\s*(?=\]|,),?)+\]|\((?<anidb>anidb(?:[\- \.]?id)?)[\-= ](?:(?<=\k<anidb>[\-= ]|,)\s*(?<animeId>\d+)\s*(?=\)|,),?)+\)|\{(?<anidb>anidb(?:[\- \.]?id)?)[\-= ](?:(?<=\k<anidb>[\-= ]|,)\s*(?<animeId>\d+)\s*(?=\}|,),?)+\}", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex OldFolderRegex();

    // Names the importer reads as its old patterns did, in the file name
    // (anime and episode list) and in a folder name (anime).
    public static TheoryData<string> Unchanged =>
    [
        "Show - 01 [anidb-123].mkv",
        "Show - 01 [anidb-a123].mkv",
        "Show - 01 [anidb-a123,456].mkv",
        "Show - 01 (anidbids 5,).mkv",
        "Show - 01 [anidb-5,,6].mkv",
        "Show - 01 [anidb-x] [anidb-7].mkv",
        "Show - 01 [foo [anidb-8]].mkv",
        "Show - 01 [AniDB-A9].mkv",
        "Show - 01 [anidb_id-5].mkv",
        "Show - 01 [anidb-5, ].mkv",
        "Show [anidb-123]",
        "Show [anidbid-123]",
        "Show [ anidb-123]",
        "Show [anidb ids-123]",
        "Show {anidb=a5}",
        "Show (anidb-5,)",
        "Show {tmdb=5} [anidb-7]",
    ];

    // Names the importer now reads otherwise, on purpose: it takes every
    // episode in the file name's AniDB tags rather than the last value of the
    // first tag, the first anime anywhere in the name (a combined value's
    // included) rather than the last `a` value of the first tag, the first
    // value of a folder tag rather than the last, and the new prefixes. A
    // part is written as the range it stands for.
    public static TheoryData<string, int, string, string> Changed => new()
    {
        { "Show - 01 [anidb-a123, 456, 123-457@0-50%].mkv", 0, "123 123-457@0-50%", "123 456,123-457@0-50%" },
        { "Show - 01 [ anidb id=e5'50%,e6'50%].mkv", 0, "- e6'50%", "- e5'0-50%,e6'0-50%" },
        { "Show - 01 {anidb.id-5 , 6}.mkv", 0, "- 6", "- 5,6" },
        { "Show - 01 {anidb.id-5 , 6}.mkv", 1, "6", "5" },
        { "Show - 01 [anidb-1] [anidb-a2].mkv", 0, "- 1", "2 1" },
        { "Show - 01 [anidb-123 456].mkv", 0, "- 123 456", "123 123 456" },
        { "Show [anidb id 123,456]", 0, "- 456", "- 123,456" },
        { "Show [anidb id 123,456]", 1, "456", "123" },
        { "Show [anidb-123] [anidb-456]", 0, "- 123", "- 123,456" },
        { "Show - 01 [anidb-s1] [anidb-7].mkv", 0, "- 7", "1 7" },
        { "Show - 01 [anidb-s1] [anidb-7].mkv", 1, "7", "1" },
        { "Show - 01 [anidb-tv1-5,a2].mkv", 0, "- -", "1 a1-5" },
        { "Show - 01 {anidb=s5e6} [anidb-a9,8].mkv", 0, "9 8", "5 a5e6,8" },
        { "Show [anidb-a17000]", 1, "-", "17000" },
        { "Show [anidb-tv5] [anidb-6]", 0, "- 6", "5 6" },
        { "Show [anidb-tv5] [anidb-6]", 1, "6", "5" },
        { "Show - 01 [anidb-m5'50].mkv", 0, "- -", "- e5'0-50" },
    };

    [Theory]
    [MemberData(nameof(Unchanged))]
    public void FileName_GivesTheSameAnimeAndEpisodes(string name)
        => Assert.Equal(Old(name, 0), New(name, 0));

    [Theory]
    [MemberData(nameof(Unchanged))]
    public void FolderName_GivesTheSameAnime(string name)
    {
        // An `a` value in a folder tag now names the anime too.
        if (name.Contains("-a", StringComparison.OrdinalIgnoreCase) || name.Contains("=a", StringComparison.OrdinalIgnoreCase))
            Assert.Equal(("-", Old(name, 0).Split(' ')[0]), (Old(name, 1), New(name, 1)));
        else
            Assert.Equal(Old(name, 1), New(name, 1));
    }

    [Theory]
    [MemberData(nameof(Changed))]
    public void Name_IsReadOtherwiseOnPurpose(string name, int depth, string before, string now)
    {
        Assert.Equal(before, Old(name, depth));
        Assert.Equal(now, New(name, depth));
    }

    /// <summary>
    /// What the importer's old patterns took from a file name (the anime and
    /// the episode list) or a folder name (the anime).
    /// </summary>
    private static string Old(string name, int depth)
    {
        if (depth is 0)
        {
            var file = OldFileRegex().Match(name);
            return $"{(file.Groups["animeId"].Success ? file.Groups["animeId"].Value : "-")} {(file.Groups["episodeRange"].Success ? file.Groups["episodeRange"].Value : "-")}";
        }

        var folder = OldFolderRegex().Match(name);
        return folder.Success ? folder.Groups["animeId"].Value : "-";
    }

    /// <summary>
    /// What the importer takes now, from the same parse the matching service
    /// returns.
    /// </summary>
    private static string New(string name, int depth)
    {
        string[] segments = depth is 0 ? [name] : [name, "file.mkv"];
        var tags = FileNameIds.ReadTags(segments);
        var match = ParsedFileResult.Empty;
        match.IDs = FileNameIds.ToIDs(tags);
        var (animeID, episodeList) = OfflineImporter.GetTaggedAnidbIDs(match, tags);
        return depth is 0 ? $"{animeID?.ToString() ?? "-"} {episodeList ?? "-"}" : animeID?.ToString() ?? "-";
    }
}
