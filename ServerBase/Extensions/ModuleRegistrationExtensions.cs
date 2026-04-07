using System.Reflection;
using Boxty.ServerBase.Auth.AuthorizationHandlers;
using Boxty.ServerBase.Auth.Policies;
using Boxty.ServerBase.Commands;
using Boxty.ServerBase.Endpoints;
using Boxty.ServerBase.Mappers;
using Boxty.ServerBase.Modules;
using Boxty.ServerBase.Queries;
using Boxty.ServerBase.Queries.ModuleQueries;
using Boxty.ServerBase.Services;
using Boxty.ServerBase.Services.Interfaces;
using Boxty.ServerBase.Auth.Requirements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FluentValidation;

namespace Boxty.ServerBase.Extensions
{
    public static class ModuleRegistrationExtensions
    {
        public static IServiceCollection RegisterServices(this IServiceCollection services, IConfiguration configuration, ref List<Type> moduleTypes, out List<IModule> registeredModules)
        {
            var mapperInterfaceType = typeof(IMapper<,>);

            registeredModules = new List<IModule>();

            // Register open generic query/command handlers for all modules
            services.AddScoped(typeof(IGetAllQuery<,,>), typeof(GetAllQuery<,,>));
            services.AddScoped(typeof(IGetByIdQuery<,,>), typeof(GetByIdQuery<,,>));
            services.AddScoped(typeof(IGetByIdsQuery<,,>), typeof(GetByIdsQuery<,,>));
            services.AddScoped(typeof(IGetPagedQuery<,,>), typeof(GetPagedQuery<,,>));
            services.AddScoped(typeof(ISearchQuery<,,>), typeof(SearchQuery<,,>));
            services.AddScoped(typeof(ICreateCommand<,,>), typeof(CreateCommand<,,>));
            services.AddScoped(typeof(IUpdateCommand<,,>), typeof(UpdateCommand<,,>));
            services.AddScoped(typeof(IDeleteCommand<,>), typeof(DeleteCommand<,>));
            services.AddScoped(typeof(ICreateSubjectCommand<,,>), typeof(CreateSubjectCommand<,,>));
            services.AddScoped(typeof(ICreateTenantCommand<,,>), typeof(CreateTenantCommand<,,>));
            services.AddScoped(typeof(IResetPasswordCommand<,,>), typeof(ResetPasswordCommand<,,>));
            services.AddScoped(typeof(IDeleteTenantCommand<,,>), typeof(DeleteTenantCommand<,,>));
            services.AddScoped(typeof(IDeleteSubjectCommand<,,>), typeof(DeleteSubjectCommand<,,>));
            services.AddScoped<IGetAllRolesWithPermissionsQuery, GetAllRolesWithPermissionsFromKeycloakQuery>();
            services.AddScoped(typeof(ISendEmailCommand), typeof(SendEmailCommand));
            services.AddScoped(typeof(IEmailService), typeof(EmailService));
            services.AddScoped<IGetSubjectByIdQuery, GetSubjectByIdQuery>();
            services.AddScoped<IGetTenantByIdQuery, GetTenantByIdQuery>();

            var baseModule = new BaseModule();
            baseModule.RegisterModuleServices(services, configuration);
            registeredModules.Add(baseModule);

            foreach (var moduleType in moduleTypes)
            {
                //Create the module and register the module services
                var module = Activator.CreateInstance(moduleType) as IModule;
                if (module == null)
                {
                    throw new InvalidOperationException($"Module type {moduleType.Name} does not implement IModule interface.");
                }
                module.RegisterModuleServices(services, configuration);
                registeredModules.Add(module);

                //Register mappers & validators for each module
                var mapperTypes = moduleType.Assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces()
                        .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == mapperInterfaceType))
                    .ToList();

                foreach (var mapperType in mapperTypes)
                {
                    var interfaceType = mapperType.GetInterfaces()
                        .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == mapperInterfaceType);
                    services.AddTransient(interfaceType, mapperType);
                }
            }

            services.AddScoped<IAuthorizationHandler, ResourceAccessAuthorizationHandler>();
            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
            ResourceAccessPolicyRegistry.AddRequirement(new ResourceAccessRequirement());

            foreach (var moduleType in moduleTypes)
            {
                try
                {
                    var sharedAssemblyName = "Boxty.SharedApp";
                    if (!string.IsNullOrEmpty(sharedAssemblyName))
                    {
                        var moduleSharedAssembly = Assembly.Load(sharedAssemblyName);
                        services.AddValidatorsFromAssembly(moduleSharedAssembly);
                    }
                }
                catch (Exception)
                {
                    // Assembly doesn't exist or couldn't be loaded, skip
                }
            }

            services.AddAuthorization(options =>
            {
                options.AddPermissionPoliciesForEntities();
                ResourceAccessPolicyRegistry.BuildPolicy(options);
            });

            return services;
        }
        public static WebApplication ConfigureServicesAndMapEndpoints(this WebApplication app, bool isDevelopment, List<IModule> registeredModules)
        {
            ValidatePermissionProviderRegistration(app.Services);

            var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("ModuleRegistration");
            logger.LogInformation("Configuring {ModuleCount} modules and mapping endpoints...", registeredModules.Count);

            foreach (var module in registeredModules)
            {
                var moduleName = module.GetType().Name;
                logger.LogDebug("Configuring module: {ModuleName}", moduleName);

                module.ConfigureModuleServices(app, isDevelopment);

                if (module.GetType().Name == "BaseModule")
                {
                    continue;
                }

                try
                {
                    var endpointAssemblyName = $"Boxty.ServerApp.Modules.{module.GetType().Name.Replace("Module", "")}.Endpoints";
                    var endpointAssembly = Assembly.Load(endpointAssemblyName);

                    var endpointTypes = endpointAssembly.GetTypes()
                                                    .Where(x => x.IsAssignableTo(typeof(IEndpoints)) && x.IsClass)
                                                    .Select(Activator.CreateInstance)
                                                    .Cast<IEndpoints>();

                    var endpointCount = 0;
                    foreach (var endpointType in endpointTypes)
                    {
                        endpointType.MapEndpoints(app);
                        endpointCount++;
                    }

                    logger.LogInformation("Module {ModuleName}: Mapped {EndpointCount} endpoint groups", moduleName, endpointCount);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to load endpoints for module {ModuleName}", moduleName);
                }
            }

            logger.LogInformation("Module configuration and endpoint mapping completed");
            return app;
        }

        private static void ValidatePermissionProviderRegistration(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var permissionQueries = scope.ServiceProvider.GetServices<IGetAllRolesWithPermissionsQuery>().ToList();

            if (permissionQueries.Count == 0)
            {
                throw new InvalidOperationException(
                    "No IGetAllRolesWithPermissionsQuery implementation registered. " +
                    "Register a permission provider to enable fine-grained server authorization.");
            }

            if (permissionQueries.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Multiple IGetAllRolesWithPermissionsQuery implementations registered ({permissionQueries.Count}). " +
                    "Register exactly one permission provider.");
            }
        }
    }
}
