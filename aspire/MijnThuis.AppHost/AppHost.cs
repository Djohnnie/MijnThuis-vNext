var builder = DistributedApplication.CreateBuilder(args);

var sqlDatabase = builder.AddConnectionString("mijnthuisdb");

var api = builder.AddProject<Projects.MijnThuis_Api>("api")
    .WithReference(sqlDatabase);

var worker = builder.AddProject<Projects.MijnThuis_Worker>("worker")
    .WithReference(sqlDatabase);

var web = builder.AddProject<Projects.MijnThuis_Web>("web")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
