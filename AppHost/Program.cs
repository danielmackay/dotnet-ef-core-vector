var builder = DistributedApplication.CreateBuilder(args);

var db = builder
    .AddSqlServer("sql-server")
    .WithLifetime(ContainerLifetime.Persistent)
    .AddDatabase("sql-db");

builder.AddProject<Projects.ConsoleApp>("console")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();
