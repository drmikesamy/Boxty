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
        private const string SharedAssemblyName = "Boxty.SharedApp";

        public static IServiceCollection RegisterServices(this IServiceCollection services, IConfiguration configuration, ref List<Type> moduleTypes, out List<IModule> registeredModules)
        {
            var mapperInterfaceType = typeof(IMapper<,>);

            registeredModules = new List<IModule>();

            RegisterGenericServices(services);

            var baseModule = new BaseModule();
            baseModule.RegisterModuleServices(services, configuration);
            registeredModules.Add(baseModule);

            foreach (var moduleType in moduleTypes)
            {
                var module = CreateModule(moduleType);
                module.RegisterModuleServices(services, configuration);
                registeredModules.Add(module);

                RegisterModuleMappers(services, moduleType.Assembly, mapperInterfaceType);
            }

            services.AddScoped<IAuthorizationHandler, ResourceAccessAuthorizationHandler>();
            services.AddScoped<IAuthorizationHandler, CreateEntityAuthorizationHandler>();
            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
            ResourceAccessPolicyRegistry.AddRequirement(new ResourceAccessRequirement());
            CreateEntityPolicyRegistry.AddRequirement(new CreateEntityRequirement());

            TryRegisterSharedValidators(services);

            services.AddAuthorization(options =>
            {
                options.AddPermissionPoliciesForEntities();
                ResourceAccessPolicyRegistry.BuildPolicy(options);
                CreateEntityPolicyRegistry.BuildPolicy(options);
            });

            return services;
        }
        public static WebApplication ConfigureServicesAndMapEndpoints(this WebApplication app, bool isDevelopment, List<IModule> registeredModules)
        {
            ValidatePermissionProviderRegistration(app.Services);

            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ModuleRegistration");
            logger.LogInformation("Configuring {ModuleCount} modules and mapping endpoints...", registeredModules.Count);

            foreach (var module in registeredModules)
            {
                var moduleName = module.GetType().Name;
                logger.LogDebug("Configuring module: {ModuleName}", moduleName);

                module.ConfigureModuleServices(app, isDevelopment);

                if (moduleName == nameof(BaseModule))
                {
                    continue;
                }

                try
                {
                    var endpointCount = 0;
                    foreach (var endpointType in LoadEndpointInstances(moduleName))
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

            PermissionAutoSeeder.SeedAsync(app.Services, logger).GetAwaiter().GetResult();
            InitializeRolePermissionCache(app, logger).GetAwaiter().GetResult();

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

        private static void RegisterGenericServices(IServiceCollection services)
        {
            services.AddScoped(typeof(IGetAllQuery<,,>), typeof(GetAllQuery<,,>));
            services.AddScoped(typeof(IGetByIdQuery<,,>), typeof(GetByIdQuery<,,>));
            services.AddScoped(typeof(IGetByIdsQuery<,,>), typeof(GetByIdsQuery<,,>));
            services.AddScoped(typeof(IGetPagedQuery<,,>), typeof(GetPagedQuery<,,>));
            services.AddScoped(typeof(ISearchQuery<,,>), typeof(SearchQuery<,,>));
            services.AddScoped(typeof(ICreateCommand<,,>), typeof(CreateCommand<,,>));
            services.AddScoped(typeof(IUpdateCommand<,,>), typeof(UpdateCommand<,,>));
            services.AddScoped(typeof(IDeleteCommand<,>), typeof(DeleteCommand<,>));
            services.AddScoped(typeof(ISendEmailCommand), typeof(SendEmailCommand));
            services.AddScoped(typeof(IEmailService), typeof(EmailService));
        }

        private static IModule CreateModule(Type moduleType)
        {
            var module = Activator.CreateInstance(moduleType) as IModule;
            if (module == null)
            {
                throw new InvalidOperationException($"Module type {moduleType.Name} does not implement IModule interface.");
            }

            return module;
        }

        private static void RegisterModuleMappers(IServiceCollection services, Assembly assembly, Type mapperInterfaceType)
        {
            var mapperTypes = assembly.GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract && type.GetInterfaces()
                    .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == mapperInterfaceType));

            foreach (var mapperType in mapperTypes)
            {
                var interfaceType = mapperType.GetInterfaces()
                    .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == mapperInterfaceType);
                services.AddTransient(interfaceType, mapperType);
            }
        }

        private static void TryRegisterSharedValidators(IServiceCollection services)
        {
            try
            {
                var sharedAssembly = Assembly.Load(SharedAssemblyName);
                services.AddValidatorsFromAssembly(sharedAssembly);
            }
            catch (Exception)
            {
                // Assembly doesn't exist or couldn't be loaded, skip
            }
        }

        private static IEnumerable<IEndpoints> LoadEndpointInstances(string moduleName)
        {
            var endpointAssemblyName = $"Boxty.ServerApp.Modules.{moduleName.Replace("Module", string.Empty)}.Endpoints";
            var endpointAssembly = Assembly.Load(endpointAssemblyName);

            return endpointAssembly.GetTypes()
                .Where(type => type.IsAssignableTo(typeof(IEndpoints)) && type.IsClass)
                .Select(Activator.CreateInstance)
                .Cast<IEndpoints>();
        }

        private static async Task InitializeRolePermissionCache(WebApplication app, ILogger logger)
        {
            try
            {
                logger.LogInformation("Initializing role permission cache after module configuration...");

                using var scope = app.Services.CreateScope();
                var cacheService = scope.ServiceProvider.GetRequiredService<IRolePermissionCacheService>();
                await cacheService.InitAsync();

                logger.LogInformation("Role permission cache initialized successfully");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to initialize role permission cache after module configuration");
            }
        }
    }
}
