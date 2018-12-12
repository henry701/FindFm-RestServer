using System;
using System.Collections.Generic;
using System.Net;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NLog;
using RestServer.Infrastructure.AspNetCore.Middleware;
using RestServer.Model.Config;

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
                options.ModelMetadataDetailsProviders.Add(new CustomRequiredBindingMetadataProvider());
            })
            .AddDataAnnotations()
            .SetCompatibilityVersion(CompatibilityVersion.Latest)
            .AddJsonFormatters(options =>
            {
                options.ContractResolver = new CamelCasePropertyNamesContractResolver();
                options.DateFormatHandling = DateFormatHandling.IsoDateFormat;
                options.Formatting = HostingEnvironment.IsDevelopment() ? Formatting.Indented : Formatting.None;
                options.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor;
                options.NullValueHandling = NullValueHandling.Ignore;
            })
            .AddJsonOptions(options =>
            {
                options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                options.SerializerSettings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
                options.SerializerSettings.Formatting = HostingEnvironment.IsDevelopment() ? Formatting.Indented : Formatting.None;
                options.SerializerSettings.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor;
                options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
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

        private void ConfigureJwt(IServiceCollection services)
        {
            var signingConfigurations = new SigningConfigurations("key.pem");
            services.AddSingleton(signingConfigurations);

            var tokenConfigurations = new TokenConfigurations()
            {
                // Audience = "ExampleAudience",
                // Issuer = "ExampleIssuer",
                Seconds = HostingEnvironment.IsDevelopment() ? 
                            (int) TimeSpan.FromDays(834).TotalSeconds : 
                            (int) TimeSpan.FromHours(2).TotalSeconds,
            };
            services.AddSingleton(tokenConfigurations);

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = tokenConfigurations.Issuer == null ? false : true,
                    ValidateAudience = tokenConfigurations.Audience == null ? false : true,
                    ValidIssuer = tokenConfigurations.Issuer,
                    ValidAudience = tokenConfigurations.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = signingConfigurations.Key == null ? false : true,
                    ClockSkew = TimeSpan.FromSeconds(10),
                    IssuerSigningKey = signingConfigurations.Key,
                };
            });
        }

        private void ConfigureIpRateLimiting(IServiceCollection services)
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
                rateLimitOptions.HttpStatusCode = ServerConfiguration.IpRateLimitOptions.HttpStatusCode;
                rateLimitOptions.ClientWhitelist = ServerConfiguration.IpRateLimitOptions.ClientWhitelist;
                rateLimitOptions.IpWhitelist = ServerConfiguration.IpRateLimitOptions.IpWhitelist;
                rateLimitOptions.GeneralRules = ServerConfiguration.IpRateLimitOptions.GeneralRules;
                rateLimitOptions.StackBlockedRequests = ServerConfiguration.IpRateLimitOptions.StackBlockedRequests;
                rateLimitOptions.RealIpHeader = ServerConfiguration.IpRateLimitOptions.RealIpHeader;
                rateLimitOptions.IpPolicyPrefix = ServerConfiguration.IpRateLimitOptions.IpPolicyPrefix;
                rateLimitOptions.EndpointWhitelist = ServerConfiguration.IpRateLimitOptions.EndpointWhitelist;
                rateLimitOptions.EnableEndpointRateLimiting = ServerConfiguration.IpRateLimitOptions.EnableEndpointRateLimiting;
                rateLimitOptions.QuotaExceededMessage = ServerConfiguration.IpRateLimitOptions.QuotaExceededMessage;
                rateLimitOptions.RateLimitCounterPrefix = ServerConfiguration.IpRateLimitOptions.RateLimitCounterPrefix;
                rateLimitOptions.ClientIdHeader = ServerConfiguration.IpRateLimitOptions.ClientIdHeader;
                rateLimitOptions.DisableRateLimitHeaders = ServerConfiguration.IpRateLimitOptions.DisableRateLimitHeaders;
            });
        }

        void IStartup.Configure(IApplicationBuilder app)
        {
            Logger.LogTrace($"{nameof(IStartup.Configure)} called");

            Logger.LogDebug("Adding the Exception middleware to pipeline");
            if (HostingEnvironment.IsDevelopment() && !ServerConfiguration.DisableErrorTraces)
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseMiddleware<UnhandledExceptionHandler>();
            }

            Logger.LogDebug("Adding 'Guid Setter' middleware to pipeline");
            app.Use(async (context, next) =>
            {
                var requestId = Guid.NewGuid().ToString() + Guid.NewGuid().ToString();
                MappedDiagnosticsLogicalContext.Set("RequestId", requestId);
                context.TraceIdentifier = requestId;
                await next.Invoke();
            });

            Logger.LogDebug("Adding the 'Forwarded Headers' middleware to pipeline");
            var forwardedHeadersOptions = new ForwardedHeadersOptions()
            {
                ForwardedHeaders = ForwardedHeaders.All,
            };
            forwardedHeadersOptions.KnownProxies.Add(IPAddress.Parse("127.0.0.1"));
            app.UseForwardedHeaders(forwardedHeadersOptions);

            Logger.LogDebug("Adding the 'Ip Rate Limiting' middleware to pipeline");
            app.UseIpRateLimiting();

            Logger.LogDebug("Adding the ApplicationExceptionHandler middleware to pipeline");
            app.UseMiddleware<ApplicationExceptionHandler>();

            Logger.LogDebug("Adding the 'Network Logger' middleware to pipeline");
            app.UseMiddleware<BasicNetworkLoggerMiddleware>();

            Logger.LogDebug("Adding the AuthIssueHandler middleware to pipeline");
            app.UseMiddleware<AuthIssueHandlerMiddleware>();
            app.UseAuthentication();

            Logger.LogDebug("Adding 'UseMvc' middleware to pipeline");
            app.UseMvc();
        }
    }
}