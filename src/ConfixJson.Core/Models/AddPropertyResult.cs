using System.Text.Json.Nodes;

namespace ConfixJson.Core.Models;

public enum PropertyAddFailure
{
    None,
    ParentNotFound,
    ParentNotObject,
    PropertyAlreadyExists,
    InvalidPropertyName,
    InvalidOperation
}

public sealed class AddPropertyResult
{
    public bool IsSuccess { get; init; }
    public PropertyAddFailure Failure { get; init; } = PropertyAddFailure.None;
    public JsonNode Root { get; init; } = new JsonObject();
    public string? ErrorMessage { get; init; }

    public static AddPropertyResult Ok(JsonNode root) => new() { IsSuccess = true, Root = root };

    public static AddPropertyResult Fail(PropertyAddFailure failure, JsonNode root, string? message = null)
        => new() { IsSuccess = false, Failure = failure, Root = root, ErrorMessage = message };
}
