using Microsoft.AspNetCore.Mvc;

namespace MagicLibrary.Contract.Requests;

public class ListParams
{
    [FromQuery(Name = "p")]
    public string Page { get; set; } = "0";

    [FromQuery(Name = "ps")]
    public string PageSize { get; set; } = "10";
}
