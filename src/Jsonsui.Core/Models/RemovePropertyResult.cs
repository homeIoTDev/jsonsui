using System.Text.Json.Nodes;

namespace Jsonsui.Core.Models;

public enum RemovePropertyFailure
{
    None,
    ParentNotFound,
    ParentNotObject,
    PropertyNotFound,
    InvalidPropertyName
}

public sealed class RemovePropertyResult
{
    public bool IsSuccess { get; init; }
    public RemovePropertyFailure Failure { get; init; } = RemovePropertyFailure.None;
    public JsonNode Root { get; init; } = new JsonObject();

    public static RemovePropertyResult Ok(JsonNode root) => new() { IsSuccess = true, Root = root };

    public static RemovePropertyResult Fail(RemovePropertyFailure failure, JsonNode root)
        => new() { IsSuccess = false, Failure = failure, Root = root };
}
