using System;
using System.Net;
using System.Runtime.CompilerServices;
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
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NLog;
using RestServer.Exceptions;
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

            var signingConfigurations = new SigningConfigurations();
            services.AddSingleton(signingConfigurations);

            var tokenConfigurations = new TokenConfigurations()
            {
                Audience = "ExampleAudience",
                Issuer = "ExampleIssuer",
                Seconds = (int) TimeSpan.FromHours(1).TotalSeconds,
            };
            services.AddSingleton(tokenConfigurations);

            services.AddAuthentication(authOptions =>
            {
                authOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                authOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(bearerOptions =>
            {
                var paramsValidation = bearerOptions.TokenValidationParameters;
                paramsValidation.IssuerSigningKey = signingConfigurations.Key;
                paramsValidation.ValidAudience = tokenConfigurations.Audience;
                paramsValidation.ValidIssuer = tokenConfigurations.Issuer;
                paramsValidation.RequireExpirationTime = true;

                // Valida a assinatura de um token recebido
                paramsValidation.ValidateIssuerSigningKey = true;

                // Verifica se um token recebido ainda é válido
                paramsValidation.ValidateLifetime = true;

                // Tempo de tolerância para a expiração de um token (utilizado
                // caso haja problemas de sincronismo de horário entre diferentes
                // computadores envolvidos no processo de comunicação)
                paramsValidation.ClockSkew = TimeSpan.FromSeconds(5);
            });

            var bearerPolicy = new AuthorizationPolicyBuilder()
                        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                        .RequireAuthenticatedUser()
                        .Build();

            services.AddMvcCore(options =>
            {
                options.ReturnHttpNotAcceptable = true;
                options.RespectBrowserAcceptHeader = true;
                options.Filters.Add(new CustomAuthorizeFilter(bearerPolicy));
            })
            .AddAuthorization(auth =>
            {
                auth.AddPolicy("Bearer", bearerPolicy);
                auth.DefaultPolicy = bearerPolicy;
            })
            .AddJsonFormatters(options =>
            {
                options.ContractResolver = new CamelCasePropertyNamesContractResolver();
                options.DateFormatHandling = DateFormatHandling.IsoDateFormat;
                options.Formatting = HostingEnvironment.IsDevelopment() ? Formatting.Indented : Formatting.None;
                options.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor;
                options.NullValueHandling = NullValueHandling.Ignore;
            })
            .AddXmlSerializerFormatters()
            .AddFormatterMappings(options =>
            {
                
            })
            .AddCors(options =>
            {
                // Allow all
                options.AddDefaultPolicy(builder =>
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
                services.Add(new ServiceDescriptor(typeof(IApplicationFeatureProvider<ControllerFeature>),
                    provider => customControllerFeature,
                    ServiceLifetime.Singleton)
                );
                partManager.FeatureProviders.Add(customControllerFeature);
                partManager.PopulateFeature(typeof(ControllerFeature));
            });

            return services.BuildServiceProvider();
        }

        void IStartup.Configure(IApplicationBuilder app)
        {
            Logger.LogTrace($"{nameof(IStartup.Configure)} called");

            Logger.LogDebug("Adding 'Guid Setter' middleware to pipeline");
            app.Use(async (context, next) =>
            {
                var requestId = Guid.NewGuid().ToString() + Guid.NewGuid().ToString();
                MappedDiagnosticsLogicalContext.Set("RequestId", requestId);
                await next.Invoke();
            });

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.All
            });

            app.Use(async (context, next) =>
            {
                // TODO: Exception Middleware clauses by class ^-^
                try
                {
                    await next.Invoke();
                }
                catch (ApplicationException exception)
                {
                    ResponseBody errorBody;
                    if (exception is ValidationException validationException)
                    {
                        context.Response.StatusCode = 422;
                        errorBody = new ResponseBody()
                        {
                            Success = false,
                            Code = ResponseCode.ValidationFailure,
                            Message = validationException.Message,
                        };
                        await context.WriteResultAsync(new ObjectResult(errorBody));
                    }
                }
            });

            if (HostingEnvironment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.Use(async (context, next) =>
                {
                    try
                    {
                        await next.Invoke();
                    }
                    catch(Exception exception)
                    {
                        ResponseBody errorBody;
                        Logger.LogError(exception, "Unexpected exception occured!");
                        errorBody = new ResponseBody()
                        {
                            Success = false,
                            Code = ResponseCode.GenericFailure,
                            Message = "Internal Server Error",
                        };
                        context.Response.StatusCode = (int) HttpStatusCode.InternalServerError;
                        await context.WriteResultAsync(new ObjectResult(errorBody));
                    }
                });
            }

            Logger.LogDebug("Adding 'UseMvc' middleware to pipeline");
            app.UseMvc();
        }
    }
}