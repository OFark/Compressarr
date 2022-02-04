using Compressarr.Application;
using Compressarr.FFmpeg;
using Compressarr.Filtering;
using Compressarr.Filtering.Models;
using Compressarr.History;
using Compressarr.Initialisation;
using Compressarr.JobProcessing;
using Compressarr.JobProcessing.Models;
using Compressarr.Pages.Services;
using Compressarr.Presets;
using Compressarr.Services;
using Compressarr.Settings;
using Compressarr.Settings.FFmpegFactory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MudBlazor;
using MudBlazor.Services;
using System.Collections.Generic;
using System.Text.Json.Serialization;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMemoryCache();

//Add 3rd Party Extensions
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomCenter;
});


builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    //options.SerializerOptions.Converters.Add(new JsonStringEnumConverter().CreateConverter(typeof(CodecType), new JsonSerializerOptions(JsonSerializerDefaults.Web)));

});

// Configure settings
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("Settings"));
builder.Services.Configure<APIServiceSettings>(builder.Configuration.GetSection("Services"));
builder.Services.Configure<HashSet<Filter>>(builder.Configuration.GetSection("Filters"));
builder.Services.Configure<HashSet<FFmpegPresetBase>>(builder.Configuration.GetSection("Presets"));
builder.Services.Configure<HashSet<Job>>(builder.Configuration.GetSection("Jobs"));


// Add DI Services


builder.Services.AddSingleton<IApplicationInitialiser, ApplicationInitialiser>();
builder.Services.AddSingleton<IApplicationService, ApplicationService>();
//services.AddSingleton<IProcessManager, ProcessManager>();

builder.Services.AddScoped<ILayoutService, LayoutService>();

builder.Services.AddTransient<IArgumentService, ArgumentService>();
builder.Services.AddTransient<IFFmpegProcessor, FFmpegProcessor>();
builder.Services.AddTransient<IFilterManager, FilterManager>();
builder.Services.AddTransient<IFolderService, FolderService>();
builder.Services.AddTransient<IHistoryService, HistoryService>();
builder.Services.AddTransient<IJobManager, JobManager>();
builder.Services.AddTransient<IPresetManager, PresetManager>();
builder.Services.AddTransient<IRadarrService, RadarrService>();
builder.Services.AddTransient<ISonarrService, SonarrService>();

builder.Services.AddTransient<IFileService, FileService>();

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomCenter;
});


// Add Startup/Background Services
builder.Services.AddHostedService<StartupBackgroundService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();