var builder = DistributedApplication.CreateBuilder(args);

var sqlDatabase = builder.AddSqlServer("sqlserver")
    .AddDatabase("mijnthuisdb");

var api = builder.AddProject<Projects.MijnThuis_Api>("api")
    .WithReference(sqlDatabase)
    .WaitFor(sqlDatabase);

var worker = builder.AddProject<Projects.MijnThuis_Worker>("worker")
    .WithReference(sqlDatabase)
    .WaitFor(sqlDatabase);

var web = builder.AddProject<Projects.MijnThuis_Web>("web")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
