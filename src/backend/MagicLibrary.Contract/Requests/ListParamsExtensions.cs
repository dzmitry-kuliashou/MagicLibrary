using System.Globalization;

namespace MagicLibrary.Contract.Requests;

public static class ListParamsExtensions
{
    public static int GetNormalizedPage(this ListParams parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var page = int.TryParse(
            parameters.Page,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedPage) && parsedPage >= 0
                ? parsedPage
                : 0;
        var pageSize = parameters.GetNormalizedPageSize();

        return Math.Min(page, int.MaxValue / pageSize);
    }

    public static int GetNormalizedPageSize(this ListParams parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return int.TryParse(
            parameters.PageSize,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedPageSize) && parsedPageSize > 0
                ? parsedPageSize
                : 10;
    }
}
