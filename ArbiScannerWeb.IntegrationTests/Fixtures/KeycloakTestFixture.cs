using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArbiScannerWeb.IntegrationTests.Support;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.Keycloak;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace ArbiScannerWeb.IntegrationTests.Fixtures;

// Spins up a real Keycloak container and imports the actual production realm
// export (keycloak/realm-export/arbiscanner-web-realm.json, with sslRequired
// patched to "none" in a temp copy — see BuildImportableRealmFile) — this is
// the one fixture in the suite that proves the API's Authority-based JWKS
// discovery really works against a live IdP, and that the realm export itself
// imports cleanly. Everything else in the suite uses the fast forged-JWT path
// instead (see JwtTestSettings/JwtTestTokenFactory).
public sealed class KeycloakTestFixture : IAsyncLifetime
{
    private const string RealmName = "arbiscanner-web";
    private const string AdminUsername = "admin";
    private const string AdminPassword = "admin";

    // Throwaway confidential client, created via kcadm.sh after the container
    // starts (not part of the shipped realm export) — used only to mint a
    // genuinely Keycloak-signed token for this test, via the client_credentials
    // grant. The production arbiscanner-web-spa client is a public, PKCE-only
    // client with no direct/service-account grant, exactly as it should be, so
    // it can't be used to mint a token from a plain test.
    private const string TestClientId = "integration-test-harness";
    private const string TestClientSecret = "integration-test-harness-secret";

    // arbiscanner-mcp ships in the real realm export (confidential,
    // standard.token.exchange.enabled=true, audience mappers for both itself
    // and arbiscanner-web-spa — see keycloak/realm-export/arbiscanner-web-realm.json)
    // but without a secret, on purpose ("no secrets in git" — Keycloak generates
    // one on import). Overwritten to a known value here so McpTokenService's
    // Standard Token Exchange call can actually authenticate as this client.
    private const string McpClientId = "arbiscanner-mcp";
    private const string McpClientSecret = "integration-test-mcp-secret";

    private readonly string _importedRealmFile = BuildImportableRealmFile();

    private KeycloakContainer? _keycloak;

    // The API needs its usual dependencies to start up at all (Postgres for
    // AddDbContext/SeedExchangesAsync, Redis for the connection multiplexer, Mongo
    // for the spreads store) — this fixture only swaps out auth for a real IdP, it
    // doesn't test business logic, so RabbitMq is left pointed at a dummy address
    // like the other fixtures do.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(Images.Postgres)
        .WithDatabase("ArbiScannerBot")
        .WithUsername("postgres")
        .WithPassword("REDACTED")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder(Images.Redis)
        .Build();

    private readonly MongoDbContainer _mongo = new MongoDbBuilder(Images.Mongo)
        .Build();

    internal CustomWebApplicationFactory Factory { get; private set; } = default!;
    internal string Authority { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _keycloak = new KeycloakBuilder(Images.Keycloak)
            .WithUsername(AdminUsername)
            .WithPassword(AdminPassword)
            .WithRealm(_importedRealmFile)
            .Build();

        await Task.WhenAll(_keycloak.StartAsync(), _postgres.StartAsync(), _redis.StartAsync(), _mongo.StartAsync());

        Authority = $"{_keycloak.GetBaseAddress().TrimEnd('/')}/realms/{RealmName}";

        await RegisterServiceAccountClientAsync();
        await SetMcpClientSecretAsync();
        await AddMcpAudienceMapperToServiceAccountClientAsync();

        Factory = new CustomWebApplicationFactory(
            new Dictionary<string, string?>
            {
                ["Jwt:Authority"] = Authority,
                // The client_credentials token minted below isn't audience-mapped to
                // arbiscanner-web-spa (that mapper only applies to the SPA's own
                // Authorization Code tokens) — this fixture exists to prove
                // Authority/JWKS/issuer/signature validation against a live IdP, not
                // audience enforcement (which forged-JWT tests already cover cheaply).
                ["Jwt:Audience"] = string.Empty,
                // McpTokenService's Standard Token Exchange grant — the same
                // arbiscanner-mcp client already imported from the real realm export,
                // secret overwritten above so this fixture can authenticate as it.
                ["Keycloak:McpExchange:Authority"] = Authority,
                ["Keycloak:McpExchange:ClientId"] = McpClientId,
                ["Keycloak:McpExchange:ClientSecret"] = McpClientSecret,
                ["ConnectionStrings:SqlServer"] = _postgres.GetConnectionString(),
                ["Redis:Endpoint"] = _redis.GetConnectionString(),
                ["MongoDb:ConnectionString"] = _mongo.GetConnectionString(),
                ["MongoDb:DatabaseName"] = "ArbiScannerWebIntegrationTests",
                ["MongoDb:CurrentSpreadsCollection"] = "CurrentSpreads",
                ["MongoDb:SpreadsTickerCollection"] = "SpreadsTicker",
                ["RabbitMq:Host"] = "127.0.0.1",
                ["RabbitMq:Port"] = "1",
                ["RabbitMq:Queue"] = "spread_api_unused",
                ["RabbitMq:Exchange"] = "spread_fanout_exchange_unused",
                ["RabbitMq:RoutingKey"] = "",
                ["RabbitMq:Username"] = "guest",
                ["RabbitMq:Password"] = "guest",
            },
            // Jwt:Authority (set above) already points AddJwtBearer at this real
            // container, so the framework's own JwtBearerPostConfigureOptions wires
            // up real JWKS discovery/signature validation unmodified. Two things are
            // relaxed relative to production: RequireHttpsMetadata (the raw
            // testcontainer serves plain HTTP — production reaches Keycloak through
            // nginx's TLS termination, per keycloak/README.md) and audience
            // validation (the service-account token minted below isn't mapped to
            // the SPA client's audience). This must run as a Configure (not
            // PostConfigure) — the framework's own JwtBearerPostConfigureOptions
            // throws on a plain-HTTP Authority when RequireHttpsMetadata is still
            // true, and all Configure delegates run before any PostConfigure ones.
            configureTestServices: services =>
                services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.RequireHttpsMetadata = false;
                    options.TokenValidationParameters.ValidateAudience = false;
                }));

        _ = Factory.Services;
    }

    internal async Task<string> GetServiceAccountTokenAsync()
    {
        using var http = new HttpClient();
        using var response = await http.PostAsync(
            $"{Authority}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = TestClientId,
                ["client_secret"] = TestClientSecret,
            }));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("access_token").GetString()!;
    }


    // Runs kcadm.sh *inside* the container rather than hitting the host-mapped
    // port from here: Keycloak's default sslRequired=external policy on the
    // built-in master realm (which the realm-export patch below can't reach,
    // since it only imports arbiscanner-web) rejects plain-HTTP admin logins
    // that arrive from anything it doesn't consider an internal address — which
    // Docker Desktop's host-side port mapping doesn't reliably satisfy. A
    // loopback connection from inside the same container always does.
    private async Task RegisterServiceAccountClientAsync()
    {
        await ExecOrThrow(
            "kcadm.sh", "config", "credentials",
            "--server", "http://localhost:8080",
            "--realm", "master",
            "--user", AdminUsername,
            "--password", AdminPassword);

        await ExecOrThrow(
            "kcadm.sh", "create", "clients",
            "-r", RealmName,
            "-s", $"clientId={TestClientId}",
            "-s", $"secret={TestClientSecret}",
            "-s", "publicClient=false",
            "-s", "serviceAccountsEnabled=true",
            "-s", "standardFlowEnabled=false",
            "-s", "directAccessGrantsEnabled=false",
            "-s", "protocol=openid-connect");
    }

    // arbiscanner-mcp ships without a secret (Keycloak generates a random one on
    // import) — overwritten to a known value so this fixture's HttpClient calls
    // can authenticate as it. Reuses the admin session RegisterServiceAccountClientAsync
    // already logged in above (kcadm persists credentials in the container's
    // filesystem across separate `docker exec` calls).
    private async Task SetMcpClientSecretAsync()
    {
        var internalId = await GetClientInternalIdAsync(McpClientId);
        await ExecOrThrow("kcadm.sh", "update", $"clients/{internalId}", "-r", RealmName, "-s", $"secret={McpClientSecret}");
    }

    // Standard Token Exchange requires the requesting client (arbiscanner-mcp,
    // which McpTokenService authenticates as) to already appear in the subject
    // token's own audience — see keycloak/README.md step 9. The throwaway
    // integration-test-harness client's tokens don't carry that audience by
    // default, so this adds the same oidc-audience-mapper the production
    // arbiscanner-web-spa client ships with.
    private async Task AddMcpAudienceMapperToServiceAccountClientAsync()
    {
        var internalId = await GetClientInternalIdAsync(TestClientId);
        await ExecOrThrow(
            "kcadm.sh", "create", $"clients/{internalId}/protocol-mappers/models",
            "-r", RealmName,
            "-s", "name=audience-arbiscanner-mcp",
            "-s", "protocol=openid-connect",
            "-s", "protocolMapper=oidc-audience-mapper",
            "-s", "config.\"included.client.audience\"=arbiscanner-mcp",
            "-s", "config.\"id.token.claim\"=false",
            "-s", "config.\"access.token.claim\"=true");
    }

    private async Task<string> GetClientInternalIdAsync(string clientId)
    {
        var (_, stdout, _) = await ExecCapture("kcadm.sh", "get", "clients", "-r", RealmName, "-q", $"clientId={clientId}", "--fields", "id");
        using var doc = JsonDocument.Parse(stdout);
        return doc.RootElement[0].GetProperty("id").GetString()!;
    }

    private async Task ExecOrThrow(params string[] command) => await ExecCapture(command);

    private async Task<(long? ExitCode, string Stdout, string Stderr)> ExecCapture(params string[] command)
    {
        var fullCommand = new List<string> { "/opt/keycloak/bin/" + command[0] };
        fullCommand.AddRange(command.Skip(1));

        var result = await _keycloak!.ExecAsync(fullCommand);
        return result.ExitCode != 0
            ? throw new InvalidOperationException(
                $"'{string.Join(' ', fullCommand)}' failed (exit {result.ExitCode}):\n{result.Stdout}\n{result.Stderr}")
            : (result.ExitCode, result.Stdout, result.Stderr);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        var disposeTasks = new List<Task> { _postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask(), _mongo.DisposeAsync().AsTask() };
        if (_keycloak is not null)
        {
            disposeTasks.Add(_keycloak.DisposeAsync().AsTask());
        }
        await Task.WhenAll(disposeTasks);
        File.Delete(_importedRealmFile);
    }

    // Copies the actual production realm export to a temp file with sslRequired
    // patched to "none" — this fixture proves resource-server JWKS validation
    // against a live IdP, not Keycloak's own login-transport security policy,
    // and that policy is what makes host-to-container admin/token calls over
    // plain HTTP unreliable under Docker Desktop's networking. Production's
    // shipped file (keycloak/realm-export/arbiscanner-web-realm.json) is
    // untouched — only this temp copy differs.
    private static string BuildImportableRealmFile()
    {
        var sourcePath = FindRealmExportFile();
        var json = JsonNode.Parse(File.ReadAllText(sourcePath))!.AsObject();
        json["sslRequired"] = "none";

        var tempPath = Path.Combine(Path.GetTempPath(), $"arbiscanner-web-realm-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(tempPath, json.ToJsonString());
        return tempPath;
    }

    private static string FindRealmExportFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "keycloak", "realm-export", "arbiscanner-web-realm.json")))
        {
            dir = dir.Parent;
        }

        return dir is null
            ? throw new InvalidOperationException("Could not locate keycloak/realm-export/arbiscanner-web-realm.json by walking up from the test assembly's output directory.")
            : Path.Combine(dir.FullName, "keycloak", "realm-export", "arbiscanner-web-realm.json");
    }
}
