using Olbrasoft.SpeechToText.Core.Configuration;
using Olbrasoft.SpeechToText.Core.Interfaces;
using Olbrasoft.SpeechToText.Providers;
using Olbrasoft.SpeechToText.Service.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure options
builder.Services.Configure<SpeechToTextOptions>(
    builder.Configuration.GetSection("SpeechToText"));

// Register services
builder.Services.AddSingleton<ITranscriptionProvider, WhisperNetProvider>();

// Add gRPC
builder.Services.AddGrpc();

// Add controllers for REST API fallback
builder.Services.AddControllers();

// Add health checks
builder.Services.AddHealthChecks();

// Configure Kestrel to listen on port 5052
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5052, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
});

var app = builder.Build();

// Map gRPC services
app.MapGrpcService<SttGrpcService>();

// Map REST controllers
app.MapControllers();

// Map health check
app.MapHealthChecks("/health");

// gRPC info endpoint
if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => "SpeechToText gRPC Service (use gRPC client or POST /api/stt/transcribe for REST)");
}

app.Run();
