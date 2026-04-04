using Prometheus;
using SolidShortener.Api.Middlewares;

namespace SolidShortener.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseMiddleware<ErrorHandlingMiddleware>();
        app.UseMiddleware<RateLimitingMiddleware>();

        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "SolidShortener API V1");
            c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
        });

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseHttpMetrics();

        app.MapControllers();
        app.MapMetrics();

        return app;
    }
}