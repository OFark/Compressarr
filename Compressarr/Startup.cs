using Compressarr.FFmpegFactory;
using Compressarr.FFmpegFactory.Models;
using Compressarr.Filtering;
using Compressarr.Filtering.Models;
using Compressarr.JobProcessing;
using Compressarr.JobProcessing.Models;
using Compressarr.Pages.Services;
using Compressarr.Services;
using Compressarr.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MudBlazor.Services;
using System.Collections.Generic;
using Compressarr.Settings;
using Compressarr.Application.Interfaces;

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

            services.Configure<AppSettings>(options => Configuration.GetSection("Settings").Bind(options));
            services.Configure<APIServiceSettings>(options => Configuration.GetSection("Services").Bind(options));
            services.Configure<HashSet<Filter>>(options => Configuration.GetSection("Filters").Bind(options));
            services.Configure<HashSet<FFmpegPreset>>(options =>
            {
                foreach(var op in options)
                {
                    op.AudioStreamPresets.Clear();
                }
                Configuration.GetSection("Presets").Bind(options);
            });
            services.Configure<HashSet<Job>>(options => Configuration.GetSection("Jobs").Bind(options));


            services.AddSingleton<IApplicationService, ApplicationService>();
            services.AddSingleton<IStartupTask, ApplicationInitialiser>();
            services.AddSingleton<IProcessManager, ProcessManager>();

            services.AddScoped<ILayoutService, LayoutService>();

            services.AddTransient<IFFmpegManager, FFmpegManager>();
            services.AddTransient<IFilterManager, FilterManager>();
            services.AddTransient<IJobManager, JobManager>();
            services.AddTransient<IRadarrService, RadarrService>();
            services.AddTransient<ISonarrService, SonarrService>();

            services.AddTransient<IFileService, FileService>();

            services.AddMudServices();
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


            //app.UseMiddleware<AsyncInitializationMiddleware>();

            //app.UseHttpsRedirection();
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