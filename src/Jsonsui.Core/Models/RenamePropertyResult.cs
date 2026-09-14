using System.Text.Json.Nodes;

namespace Jsonsui.Core.Models;

public enum RenamePropertyFailure
{
    None,
    ParentNotFound,
    ParentNotObject,
    PropertyNotFound,
    PropertyAlreadyExists,
    InvalidPropertyName
}

public sealed class RenamePropertyResult
{
    public bool IsSuccess { get; init; }
    public RenamePropertyFailure Failure { get; init; } = RenamePropertyFailure.None;
    public JsonNode Root { get; init; } = new JsonObject();

    public static RenamePropertyResult Ok(JsonNode root) => new() { IsSuccess = true, Root = root };

    public static RenamePropertyResult Fail(RenamePropertyFailure failure, JsonNode root)
        => new() { IsSuccess = false, Failure = failure, Root = root };
}
