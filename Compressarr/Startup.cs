using Compressarr.Application;
using Compressarr.Presets;
using Compressarr.Presets.Models;
using Compressarr.Filtering;
using Compressarr.Filtering.Models;
using Compressarr.JobProcessing;
using Compressarr.JobProcessing.Models;
using Compressarr.Pages.Services;
using Compressarr.Services;
using Compressarr.Settings;
using Compressarr.Settings.FFmpegFactory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MudBlazor;
using MudBlazor.Services;
using System.Collections.Generic;
using Compressarr.FFmpeg;
using Compressarr.History;
using Compressarr.Initialisation;

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
            services.Configure<HashSet<FFmpegPresetBase>>(options => Configuration.GetSection("Presets").Bind(options));
            services.Configure<HashSet<Job>>(options => Configuration.GetSection("Jobs").Bind(options));

            services.AddHostedService<StartupBackgroundService>();

            services.AddSingleton<IApplicationInitialiser, ApplicationInitialiser>();
            services.AddSingleton<IApplicationService, ApplicationService>();
            //services.AddSingleton<IProcessManager, ProcessManager>();

            services.AddScoped<ILayoutService, LayoutService>();

            services.AddTransient<IArgumentService, ArgumentService>();
            services.AddTransient<IFFmpegProcessor, FFmpegProcessor>();
            services.AddTransient<IFilterManager, FilterManager>();
            services.AddTransient<IFolderService, FolderService>();
            services.AddTransient<IHistoryService, HistoryService>();
            services.AddTransient<IJobManager, JobManager>();
            services.AddTransient<IPresetManager, PresetManager>();
            services.AddTransient<IRadarrService, RadarrService>();
            services.AddTransient<ISonarrService, SonarrService>();

            services.AddTransient<IFileService, FileService>();

            services.AddMudServices(config =>
            {
                config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomCenter;
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
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