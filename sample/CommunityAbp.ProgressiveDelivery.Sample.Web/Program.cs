using CommunityAbp.ProgressiveDelivery.Sample.Web;
using Volo.Abp;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Host.UseAutofac();
await builder.AddApplicationAsync<SampleWebModule>();

var app = builder.Build();

app.MapDefaultEndpoints();
await app.InitializeApplicationAsync();
await app.RunAsync();
