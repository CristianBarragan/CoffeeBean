namespace CoffeeBeanery.GraphQL.Core.Foundation.Common;
public readonly record struct Result<T>(T? Value, string? Error){ public bool IsSuccess=>Error is null; public static Result<T> Ok(T v)=>new(v,null); public static Result<T> Fail(string e)=>new(default,e); }
