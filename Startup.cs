﻿using System;
using blog.Rewrite;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Web;
using Microsoft.Net.Http.Headers;

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

            services.AddSingleton(Configuration.GetSection("blog").Get<SiteSettings>());
            services.AddSingleton<IBlogService, AzureStorageBlogService>();

            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.AddOutputCache(options =>
            {
                options.AddPolicy("AuthenticatedOutputCachePolicy", OutputCacheAuthenticatedPolicy.Instance);
            });

            services.AddMicrosoftIdentityWebAppAuthentication(Configuration);

            services.AddControllersWithViews().AddRazorRuntimeCompilation();

            services.AddWebOptimizer(pipeline =>
            {
                //HeaderNames.CacheControl] = $"max-age={time.TotalSeconds.ToString()}"
                pipeline.MinifyJsFiles().AddResponseHeader(HeaderNames.CacheControl, $"max-age={TimeSpan.FromDays(365).TotalSeconds}");
                pipeline.CompileScssFiles()
                        .InlineImages(1).AddResponseHeader(HeaderNames.CacheControl, $"max-age={TimeSpan.FromDays(365).TotalSeconds}");
            });

            services.AddSingleton<ITelemetryInitializer, ArinTelemetry>();

            services.AddResponseCompression();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseStatusCodePages("text/plain", "Status code page, status code: {0}");
            }

            app.UseStatusCodePagesWithReExecute("/error/{0}");

            app.UseResponseCompression();

            app.UseWebOptimizer();

            app.UseMiddleware<ArinMiddleware>();

            app.UseStaticFiles(new StaticFileOptions()
            {
                OnPrepareResponse = (context) =>
                {
                    var time = TimeSpan.FromDays(365);
                    context.Context.Response.Headers[HeaderNames.CacheControl] = $"max-age={time.TotalSeconds}";
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

            app.UseOutputCache();

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
