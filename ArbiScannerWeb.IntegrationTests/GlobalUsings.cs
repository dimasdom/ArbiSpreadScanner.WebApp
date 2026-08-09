global using Xunit;

// Every fixture (WebApi/RabbitMq/AdminPanel/Keycloak) spins up its own WebApplicationFactory<Program>
// against the same ArbiScannerWeb.API entry point. Program.cs's top-level statements use the
// process-wide static Serilog.Log.Logger for bootstrap/fatal/close-and-flush, so running collections
// in parallel (xUnit's default) lets one fixture's shutdown dispose the logger another fixture's
// concurrent Program.Main invocation is still using mid-startup - the resulting exception is
// swallowed by Program.cs's top-level catch, which WebApplicationFactory then reports as the
// generic "entry point exited without ever building an IHost" (see AdminPanelIntegrationTests
// flakiness). Serializing collections avoids the race without touching production startup code.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
