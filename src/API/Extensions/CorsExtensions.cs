namespace Api.Extensions;

public static class CorsExtensions
{
    public const string FrontendPolicy = "FrontendPolicy";

    private const string SectionName = "Cors";

    public static IServiceCollection AddFrontendCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var allowedOrigins = configuration
            .GetSection($"{SectionName}:AllowedOrigins")
            .Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy(FrontendPolicy, policy =>
            {
                if (environment.IsDevelopment())
                {
                    policy.SetIsOriginAllowed(IsLocalhost);
                }
                else
                {
                    policy.WithOrigins(allowedOrigins);
                }

                policy.AllowAnyHeader().AllowAnyMethod();
            });
        });

        return services;
    }

    private static bool IsLocalhost(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && uri.Host is "localhost" or "127.0.0.1" or "[::1]";
}
