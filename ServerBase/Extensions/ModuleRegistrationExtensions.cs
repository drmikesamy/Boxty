using System.Reflection;
using Boxty.ServerBase.Auth.AuthorizationHandlers;
using Boxty.ServerBase.Auth.Extensions;
using Boxty.ServerBase.Auth.Providers;
using Boxty.ServerBase.Commands;
using Boxty.ServerBase.Endpoints;
using Boxty.ServerBase.Mappers;
using Boxty.ServerBase.Modules;
using Boxty.ServerBase.Queries;
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
            services.AddTransient(typeof(GetAllQuery<,,>), typeof(GetAllQuery<,,>));
            services.AddTransient(typeof(GetByIdQuery<,,>), typeof(GetByIdQuery<,,>));
            services.AddTransient(typeof(GetByIdsQuery<,,>), typeof(GetByIdsQuery<,,>));
            services.AddTransient(typeof(GetPagedQuery<,,>), typeof(GetPagedQuery<,,>));
            services.AddTransient(typeof(SearchQuery<,,>), typeof(SearchQuery<,,>));
            services.AddTransient(typeof(CreateCommand<,,>), typeof(CreateCommand<,,>));
            services.AddTransient(typeof(UpdateCommand<,,>), typeof(UpdateCommand<,,>));
            services.AddTransient(typeof(DeleteCommand<,>), typeof(DeleteCommand<,>));
            services.AddTransient(typeof(CreateSubjectCommand<,,>), typeof(CreateSubjectCommand<,,>));
            services.AddTransient(typeof(CreateTenantCommand<,,>), typeof(CreateTenantCommand<,,>));
            services.AddTransient(typeof(ResetPasswordCommand<,,>), typeof(ResetPasswordCommand<,,>));
            services.AddTransient(typeof(DeleteTenantCommand<,,>), typeof(DeleteTenantCommand<,,>));
            services.AddTransient(typeof(DeleteSubjectCommand<,,>), typeof(DeleteSubjectCommand<,,>));
            services.AddTransient(typeof(ISendEmailCommand), typeof(SendEmailCommand));
            services.AddTransient(typeof(IEmailService), typeof(EmailService));

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
            ResourceAccessPolicyProvider.AddRequirement(new ResourceAccessRequirement());

            foreach (var moduleType in moduleTypes)
            {
                try
                {
                    var sharedAssemblyName = "ComposedHealthApp.SharedApp";
                    Console.WriteLine($"shared assembly is {sharedAssemblyName}");
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
                ResourceAccessPolicyProvider.BuildPolicy(options);
            });

            return services;
        }
        public static WebApplication ConfigureServicesAndMapEndpoints(this WebApplication app, bool isDevelopment, List<IModule> registeredModules)
        {
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
                    var endpointAssemblyName = $"ComposedHealthApp.ServerApp.Modules.{module.GetType().Name.Replace("Module", "")}.Endpoints";
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
    }
}
