namespace KeycloakClient.Models;

public sealed record KeycloakGetUsersParams
{
    public string? Search { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool? Exact { get; set; }
    public bool? BriefRepresentation { get; set; }
    public bool? Enabled { get; set; }
    public int? First { get; set; }
    public int? Max { get; set; }

    public string ToQueryString()
    {
        var parameters = new List<string>();

        if (!string.IsNullOrEmpty(Search))
            parameters.Add($"search={Uri.EscapeDataString(Search)}");

        if (!string.IsNullOrEmpty(Username))
            parameters.Add($"username={Uri.EscapeDataString(Username)}");

        if (!string.IsNullOrEmpty(Email))
            parameters.Add($"email={Uri.EscapeDataString(Email)}");

        if (!string.IsNullOrEmpty(FirstName))
            parameters.Add($"firstName={Uri.EscapeDataString(FirstName)}");

        if (!string.IsNullOrEmpty(LastName))
            parameters.Add($"lastName={Uri.EscapeDataString(LastName)}");

        if (Exact.HasValue)
            parameters.Add($"exact={Exact.Value.ToString().ToLowerInvariant()}");

        if (BriefRepresentation.HasValue)
            parameters.Add($"briefRepresentation={BriefRepresentation.Value.ToString().ToLowerInvariant()}");

        if (First.HasValue)
            parameters.Add($"first={First.Value}");

        if (Max.HasValue)
            parameters.Add($"max={Max.Value}");

        if (Enabled.HasValue)
            parameters.Add($"enabled={Enabled.Value.ToString().ToLowerInvariant()}");

        return parameters.Count > 0 ? "?" + string.Join("&", parameters) : string.Empty;
    }
}
