using blog.Rewrite;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.AzureAD.UI;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using System;
using WebEssentials.AspNetCore.OutputCaching;
using WebMarkupMin.AspNetCore2;
using WebMarkupMin.Core;
using IWmmLogger = WebMarkupMin.Core.Loggers.ILogger;
using WmmNullLogger = WebMarkupMin.Core.Loggers.NullLogger;

namespace blog
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseIISIntegration()
                .Build();

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddApplicationInsightsTelemetry();

            services.AddSingleton(Configuration.GetSection("blog").Get<BlogSettings>());
            services.AddSingleton<IBlogService, AzureStorageBlogService>();

            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.AddOutputCaching(options =>
            {
                options.Profiles["default"] = new OutputCacheProfile
                {
                    UseAbsoluteExpiration = true,
                    Duration = 5 * 60
                };
            });

            services.AddAuthentication(AzureADDefaults.AuthenticationScheme)
                    .AddAzureAD(options => Configuration.Bind("AzureAd", options));

            services.Configure<OpenIdConnectOptions>(AzureADDefaults.OpenIdScheme, options =>
            {
                options.Authority += "/v2.0/";
                options.TokenValidationParameters.ValidateIssuer = false;
            });

            services.AddControllersWithViews();

            services
                .AddWebMarkupMin(options =>
                {
                    options.AllowMinificationInDevelopmentEnvironment = true;
                    options.DisablePoweredByHttpHeaders = true;
                })
                .AddHtmlMinification(options =>
                {
                    options.MinificationSettings.RemoveOptionalEndTags = false;
                    options.MinificationSettings.WhitespaceMinificationMode = WhitespaceMinificationMode.Safe;
                });
            services.AddSingleton<IWmmLogger, WmmNullLogger>(); // Used by HTML minifier

            services.AddWebOptimizer(pipeline =>
            {
                pipeline.MinifyJsFiles();
                pipeline.CompileScssFiles()
                        .InlineImages(1);
            });

            services.AddSingleton<ITelemetryInitializer, ArinTelemetry>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseStatusCodePages("text/plain", "Status code page, status code: {0}");
            }

            app.UseStatusCodePagesWithReExecute("/error/{0}");

            app.UseWebOptimizer();

            app.UseMiddleware<ArinMiddleware>();

            app.UseStaticFiles(new StaticFileOptions()
            {
                OnPrepareResponse = (context) =>
                {
                    var time = TimeSpan.FromDays(365);
                    context.Context.Response.Headers[HeaderNames.CacheControl] = $"max-age={time.TotalSeconds.ToString()}";
                    context.Context.Response.Headers[HeaderNames.Expires] = DateTime.UtcNow.Add(time).ToString("R");
                }
            });

            var rewriter = new RewriteOptions();

            if (Configuration.GetValue<bool>("forcessl"))
            {
                rewriter.AddRedirectToHttps();
            }

            if (Configuration.GetValue<bool>("dropwww"))
            {
                rewriter.Add(new StripWwwRule());
            }

            var azWebRedirect = Configuration.GetValue<string>("redirectazweb");

            if (!string.IsNullOrWhiteSpace(azWebRedirect))
            {
                rewriter.Add(new RedirectAzWebRule(azWebRedirect));
            }

            if (rewriter.Rules.Count > 0)
            {
                app.UseRewriter(rewriter);
            }

            app.UseAuthentication();

            app.UseOutputCaching();
            app.UseWebMarkupMin();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Blog}/{action=Index}/{id?}"
                );
            });
        }
    }
}
