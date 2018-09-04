using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NLog;
using RestServer.Exceptions;
using RestServer.Infrastructure.AspNetCore.Middleware;
using RestServer.Model.Config;
using RestServer.Model.Http.Response;
using Util.Extensions;

namespace RestServer.Infrastructure.AspNetCore
{
    internal sealed class RestServerStartup : IStartup
    {
        private readonly ILogger<RestServerStartup> Logger;

        public IHostingEnvironment HostingEnvironment { get; private set; }
        public ServerConfiguration ServerConfiguration { get; internal set; }
        public ServerInfo ServerContext { get; private set; }

        public RestServerStartup(IHostingEnvironment hostingEnvironment, ServerConfiguration serverConfiguration, ServerInfo serverContext, ILogger<RestServerStartup> logger)
        {
            Logger = logger;
            Logger.LogDebug($"{nameof(RestServerStartup)} Constructor invoked");
            HostingEnvironment = hostingEnvironment;
            ServerConfiguration = serverConfiguration;
            ServerContext = serverContext;
        }

        IServiceProvider IStartup.ConfigureServices(IServiceCollection services)
        {
            Logger.LogTrace($"{nameof(IStartup.ConfigureServices)} called");

            ConfigureIpRateLimiting(services);

            ConfigureJwt(services);

            ConfigureMvcCore(services);

            return BuildServiceProvider(services);
        }

        private static IServiceProvider BuildServiceProvider(IServiceCollection services)
        {
            return services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
            });
        }

        private void ConfigureMvcCore(IServiceCollection services)
        {
            services.AddMvcCore(options =>
            {
                options.ReturnHttpNotAcceptable = true;
                options.RespectBrowserAcceptHeader = true;
                options.AllowBindingHeaderValuesToNonStringModelTypes = true;
                options.Filters.Add(new AuthorizeFilter());
            })
            .AddJsonFormatters(options =>
            {
                options.ContractResolver = new CamelCasePropertyNamesContractResolver();
                options.DateFormatHandling = DateFormatHandling.IsoDateFormat;
                options.Formatting = HostingEnvironment.IsDevelopment() ? Formatting.Indented : Formatting.None;
                options.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor;
                options.NullValueHandling = NullValueHandling.Ignore;
            })
            .AddAuthorization()
            .AddXmlSerializerFormatters()
            .AddFormatterMappings()
            .AddCors(options =>
            {
                // Allow all
                options.AddPolicy("CorsPolicy", builder =>
                {
                    builder
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowAnyOrigin()
                        .AllowCredentials();
                });
            }).ConfigureApplicationPartManager(partManager =>
            {
                var customControllerFeature = new CustomControllerFeatureProvider();
                services.AddSingleton<IApplicationFeatureProvider<ControllerFeature>>(customControllerFeature);
                partManager.FeatureProviders.Add(customControllerFeature);
                partManager.PopulateFeature(typeof(ControllerFeature));
            });
        }

        private static void ConfigureJwt(IServiceCollection services)
        {
            var signingConfigurations = new SigningConfigurations("key.pem");
            services.AddSingleton(signingConfigurations);

            var tokenConfigurations = new TokenConfigurations()
            {
                // Audience = "ExampleAudience",
                // Issuer = "ExampleIssuer",
                Seconds = (int) TimeSpan.FromHours(2).TotalSeconds,
            };
            services.AddSingleton(tokenConfigurations);

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = tokenConfigurations.Issuer == null ? false : true,
                    ValidateAudience = tokenConfigurations.Audience == null ? false : true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = tokenConfigurations.Issuer,
                    ValidAudience = tokenConfigurations.Audience,
                    ClockSkew = TimeSpan.FromSeconds(10),
                    IssuerSigningKey = signingConfigurations.Key,
                };
            });
        }

        private static void ConfigureIpRateLimiting(IServiceCollection services)
        {
            // needed to store rate limit counters and ip rules
            services.AddMemoryCache(setupCache =>
            {
                setupCache.CompactOnMemoryPressure = false;
                setupCache.ExpirationScanFrequency = TimeSpan.FromSeconds(30.0d);
            });

            // inject counter and rules stores
            services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
            services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();

            // configure rate limiting
            services.Configure<IpRateLimitOptions>(rateLimitOptions =>
            {
                rateLimitOptions.HttpStatusCode = 429; // Too Many Requests
                rateLimitOptions.ClientWhitelist = new List<string>();
                rateLimitOptions.IpWhitelist = new List<string>();
                rateLimitOptions.GeneralRules = new List<RateLimitRule>()
                {
                    new RateLimitRule
                    {
                        Endpoint = "*",
                        Limit = 15,
                        Period = "1s",
                    },
                    new RateLimitRule
                    {
                        Endpoint = "*",
                        Limit = 20,
                        Period = "3s",
                    },
                    new RateLimitRule
                    {
                        Endpoint = "*",
                        Limit = 100,
                        Period = "5s",
                    },
                    new RateLimitRule
                    {
                        Endpoint = "*",
                        Limit = 1000,
                        Period = "50s",
                    },
                };
                rateLimitOptions.StackBlockedRequests = true;
                rateLimitOptions.RealIpHeader = null;
                rateLimitOptions.ClientWhitelist = new List<string>();
            });
        }

        void IStartup.Configure(IApplicationBuilder app)
        {
            Logger.LogTrace($"{nameof(IStartup.Configure)} called");

            Logger.LogDebug("Adding 'Guid Setter' middleware to pipeline");
            app.Use(async (context, next) =>
            {
                var requestId = Guid.NewGuid().ToString() + Guid.NewGuid().ToString();
                MappedDiagnosticsLogicalContext.Set("RequestId", requestId);
                context.TraceIdentifier = requestId;
                await next.Invoke();
            });

            var forwardedHeadersOptions = new ForwardedHeadersOptions()
            {
                ForwardedHeaders = ForwardedHeaders.All,
            };
            forwardedHeadersOptions.KnownProxies.Add(IPAddress.Parse("127.0.0.1"));
            app.UseForwardedHeaders(forwardedHeadersOptions);

            if (HostingEnvironment.IsDevelopment() && !ServerConfiguration.DisableErrorTraces)
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseMiddleware<UnhandledExceptionHandler>();
            }

            app.UseMiddleware<ApplicationExceptionHandler>();

            app.UseIpRateLimiting();

            app.UseMiddleware<AuthIssueHandlerMiddleware>();
            app.UseAuthentication();

            Logger.LogDebug("Adding 'UseMvc' middleware to pipeline");
            app.UseMvc();
        }
    }
}