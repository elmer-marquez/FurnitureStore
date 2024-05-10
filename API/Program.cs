using API.Configurations;
using API.Services;
using DATA;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using NLog;
using NLog.Web;
using System;
using Microsoft.Extensions.Logging;

namespace API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();

            logger.Debug("init main");

            try
            {

                var builder = WebApplication.CreateBuilder(args);

                // Add services to the container.

                builder.Services.AddControllers();
                // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen((sw) =>
                {
                    sw.SwaggerDoc("v1", new OpenApiInfo
                    {
                        Title = "Furniture_Store_API",
                        Version = "v1"
                    });

                    sw.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
                    {
                        Name = "Authorization",
                        Type = SecuritySchemeType.ApiKey,
                        Scheme = "Bearer",
                        BearerFormat = "JWT",
                        In = ParameterLocation.Header,
                        Description = $@"JWT Authorization header using the Bearer scheme. \r\n
                                         \r\n Enter Prfeix (Bearer), space, and token. 
                                        Example: Bearer ;oasdonl,r-aknlds"
                    });

                    sw.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                            new string[] {}
                        }
                    });
                });

                //me
                builder.Services.AddRouting((options)=>options.LowercaseUrls = true);

                builder.Services.AddDbContext<ApplicationDBContext>((options) =>
                {
                    options.UseSqlite(builder.Configuration.GetConnectionString("DBFStore"));
                });

                builder.Services.Configure<JWTSettings>(builder.Configuration.GetSection("JwtConfig"));

                //Email -----------------------------------------
                //-----------------------------------------------
                builder.Services.Configure<SMTPSettings>(builder.Configuration.GetSection("SMTPSettings"));
                builder.Services.AddSingleton<IEmailSender, EmailService>();

                var key = Encoding.ASCII.GetBytes(builder.Configuration.GetSection("JwtConfig:Secret").Value);
                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false, //esto en produccion debe activarse a true
                    ValidateAudience = false, // si un token lo utiliza una web no lo podran usar en otro lado
                    RequireExpirationTime = false,
                    ValidateLifetime = true
                };

                builder.Services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                }).AddJwtBearer(options =>
                {

                    options.SaveToken = true;
                    options.TokenValidationParameters = tokenValidationParameters;
                });

                builder.Services.AddDefaultIdentity<IdentityUser>(options =>
                {
                    options.SignIn.RequireConfirmedAccount = false;
                    options.SignIn.RequireConfirmedEmail = false;
                    options.SignIn.RequireConfirmedPhoneNumber = false;

                }).AddEntityFrameworkStores<ApplicationDBContext>();

                //NLog
                builder.Logging.ClearProviders();
                builder.Host.UseNLog();

                builder.Services.AddSingleton(tokenValidationParameters);


                var app = builder.Build();

                // Configure the HTTP request pipeline.
                if (app.Environment.IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI();
                }

                app.UseHttpsRedirection();

                app.UseAuthentication();
                app.UseAuthorization();


                app.MapControllers();

                app.Run();
            }catch(Exception e)
            {
                logger.Error(e, "There has an been an error");
            }
            finally
            {
                NLog.LogManager.Shutdown();
            }

        }
    }
}
