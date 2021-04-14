using Compressarr.FFmpegFactory;
using Compressarr.FFmpegFactory.Interfaces;
using Compressarr.Filtering;
using Compressarr.JobProcessing;
using Compressarr.Services;
using Compressarr.Services.Interfaces;
using Compressarr.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Compressarr
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddServerSideBlazor();
            services.AddSingleton<IRadarrService, RadarrService>();
            services.AddSingleton<ISonarrService, SonarrService>();
            services.AddSingleton<FilterManager>();
            services.AddSingleton<SettingsManager>();
            services.AddSingleton<IFFmpegManager, FFmpegManager>();
            services.AddSingleton<JobManager>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime appLifetime)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            appLifetime.ApplicationStarted.Register(() =>
            {
                //FFmpegManager = app.ApplicationServices.GetService<FFmpegManager>();
                //FFmpegManager.Start();
            }
            );

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}