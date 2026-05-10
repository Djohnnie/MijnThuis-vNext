using MijnThuis.Application;
using MijnThuis.Infrastructure.Sql;
using MijnThuis.Worker.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddApplication();
builder.Services.AddInfrastructureSql(builder.Configuration);

builder.Services.AddHostedService<SampleWorker>();

var host = builder.Build();
host.Run();
