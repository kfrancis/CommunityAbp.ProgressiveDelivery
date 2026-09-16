var builder = DistributedApplication.CreateBuilder(args);

// The sample keeps its state in a local SQLite file, so the only orchestrated resource is the ABP web host.
// OpenTelemetry (progressive_delivery.* traces and metrics) flows to the Aspire dashboard through ServiceDefaults.
builder.AddProject<Projects.CommunityAbp_ProgressiveDelivery_Sample_Web>("web")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
