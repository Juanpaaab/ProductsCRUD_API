using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ProductsCRUD_API.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly int[] ConstraintErrorNumbers = [547, 2601, 2627];

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, ex);
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, Exception ex)
    {
        var isConstraintViolation = ex is SqlException sqlEx && ConstraintErrorNumbers.Contains(sqlEx.Number);

        var problem = new ProblemDetails
        {
            Status = isConstraintViolation ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError,
            Title = isConstraintViolation ? "Data validation error" : "An unexpected error occurred",
            Detail = isConstraintViolation ? "The request conflicts with existing data rules." : null,
            Instance = context.Request.Path
        };

        context.Response.StatusCode = problem.Status.Value;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
}
