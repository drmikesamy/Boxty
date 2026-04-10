using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Boxty.SharedBase.Helpers;
using Boxty.SharedBase.Interfaces;
using Boxty.ServerBase.Config;
using Boxty.ServerBase.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

namespace Boxty.ServerBase.Modules
{
    public class BaseModule : IModule
    {
        public IServiceCollection RegisterModuleServices(IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<AppOptions>(configuration);

            services.AddDatabaseDeveloperPageExceptionFilter();
            services.AddCors(options =>
            {
                options.AddPolicy("Client",
                    policy => policy
                        .WithOrigins(configuration["Cors:AllowedClientOrigin"] ?? throw new InvalidOperationException("AllowedClientOrigin not configured."),
                            configuration["Cors:AllowedServerOrigin"] ?? throw new InvalidOperationException("AllowedServerOrigin not configured."))
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials());
            });

            bool.TryParse(configuration["Jwt:RequireHttpsMetadata"], out bool requireHttpsMetadata);

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MetadataAddress = configuration["Jwt:MetadataAddress"] ?? "";
                    options.RequireHttpsMetadata = requireHttpsMetadata;
                    options.Audience = configuration["Jwt:Audience"];
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = configuration["Jwt:Issuer"],
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        RoleClaimType = "role"
                    };
                });

            services.AddOpenApi();
            var azureStorageConnectionString = configuration.GetConnectionString("AzureBlobStorage") ?? throw new InvalidOperationException("Connection string 'AzureBlobStorage' not found.");
            var blobServiceClient = new BlobServiceClient(azureStorageConnectionString);
            var properties = blobServiceClient.GetProperties();
            properties.Value.Cors =
                new[]
                {
                    new BlobCorsRule
                    {
                        MaxAgeInSeconds = 1000,
                        AllowedHeaders = configuration["Cors:AllowedHeaders"],
                        AllowedOrigins = $"{configuration["Cors:AllowedClientOrigin"]}, {configuration["Cors:AllowedServerOrigin"]}",
                        ExposedHeaders = configuration["Cors:ExposedHeaders"],
                        AllowedMethods = configuration["Cors:AllowedMethods"],
                    }
                };
            blobServiceClient.SetProperties(properties);
            services.AddSingleton(x => blobServiceClient);

            services.AddHttpContextAccessor();
            services.AddHttpClient();

            services.AddSingleton<IRolePermissionCacheService, RolePermissionCacheService>();
            services.AddScoped<IUserClaimsReader, UserClaimsReader>();

            return services;
        }

        public WebApplication ConfigureModuleServices(WebApplication app, bool isDevelopment)
        {
            var logger = app.Services.GetRequiredService<ILogger<BaseModule>>();
            logger.LogInformation("Configuring base module services...");

            // Add request logging middleware early in the pipeline
            if (isDevelopment)
            {
                // For development, we want to see all requests
                app.Use(async (context, next) =>
                {
                    var requestLogger = context.RequestServices.GetRequiredService<ILogger<BaseModule>>();
                    requestLogger.LogDebug("Processing request: {Method} {Path}{QueryString}",
                        context.Request.Method,
                        context.Request.Path,
                        context.Request.QueryString);
                    await next();
                });
            }

            app.UseCors("Client");
            logger.LogDebug("CORS configured for Client origin");

            app.UseAuthentication();
            app.UseAuthorization();
            logger.LogDebug("Authentication and authorization configured");

            app.MapPost("/api/admin/permissions/refresh", async (IRolePermissionCacheService cacheService) =>
            {
                await cacheService.InitAsync();
                return Results.Ok(new
                {
                    message = "Permission cache refreshed",
                    refreshedAtUtc = DateTime.UtcNow
                });
            })
            .RequireAuthorization(policy => policy.RequireRole("administrator"));

            if (isDevelopment)
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
                logger.LogInformation("Development environment: OpenAPI and Scalar API reference enabled");
            }
            else
            {
                app.UseHttpsRedirection();
                logger.LogInformation("Production environment: HTTPS redirection enabled");
            }

            logger.LogInformation("Base module configuration completed");
            return app;
        }
    }
}
