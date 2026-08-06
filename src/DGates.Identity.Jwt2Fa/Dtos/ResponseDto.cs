namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>Generic success/message response used by endpoints that don't need to return other data.</summary>
public class ResponseDto
{
    /// <summary>Whether the requested operation succeeded.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>An optional human-readable message describing the outcome.</summary>
    public string? Message { get; set; }
}
