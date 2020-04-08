﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Exelor.Infrastructure.Auth.Authentication
{
    public static class AuthenticationService
    {
        public static IServiceCollection AddAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<JwtSettings>(configuration.GetSection(typeof(JwtSettings).Name));
            services.Configure<PasswordHasherSettings>(configuration.GetSection(typeof(PasswordHasherSettings).Name));
            services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
            services.AddScoped<IPasswordHasher, PasswordHasher>();

            services.AddOptions();

            var settings = services.BuildServiceProvider().GetService<IOptions<JwtSettings>>();

            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = settings.Value.SigningCredentials.Key,
                ValidateIssuer = true,
                ValidIssuer = settings.Value.Issuer,
                ValidateAudience = true,
                ValidAudience = settings.Value.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            services
                .AddAuthentication(
                    options =>
                    {
                        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                    })
                .AddJwtBearer(
                    options =>
                    {
                        options.TokenValidationParameters = tokenValidationParameters;
                        options.Events = new JwtBearerEvents
                        {
                            OnMessageReceived = (
                                context) =>
                            {
                                var token = context.HttpContext.Request.Headers["Authorization"];
                                if (token.Count > 0 && token[0].StartsWith(
                                        "Token ",
                                        StringComparison.OrdinalIgnoreCase))
                                {
                                    context.Token = token[0].Substring("Token ".Length).Trim();
                                }

                                return Task.CompletedTask;
                            },
                            OnAuthenticationFailed = context =>
                            {
                                if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                                {
                                    context.Response.Headers.Add(
                                        "Token-Expired",
                                        "true");
                                }

                                return Task.CompletedTask;
                            }
                        };
                    });
            return services;
        }
    }
}