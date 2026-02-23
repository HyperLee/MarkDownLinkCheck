using Serilog;
using MarkDownLinkCheck.Models;
using MarkDownLinkCheck.Services;
using System.Net;

namespace MarkDownLinkCheck;

public class Program
{
    public static void Main(string[] args)
    {
        try
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure Serilog from appsettings.json
            // Use two-stage initialization that allows WebApplicationFactory to reuse the host
            builder.Host.UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .WriteTo.Console());

            // Bind LinkCheckOptions from configuration
            builder.Services.Configure<LinkCheckOptions>(
                builder.Configuration.GetSection("LinkCheck"));
            
            // Also register LinkCheckOptions directly for services that need it
            builder.Services.AddSingleton(sp =>
            {
                return builder.Configuration.GetSection("LinkCheck").Get<LinkCheckOptions>()
                    ?? new LinkCheckOptions();
            });

            // Configure HttpClientFactory with SSRF protection
            builder.Services.AddHttpClient("LinkChecker", client =>
            {
                var options = builder.Configuration.GetSection("LinkCheck").Get<LinkCheckOptions>() 
                    ?? new LinkCheckOptions();
                client.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds);
                client.DefaultRequestHeaders.Add("User-Agent", options.UserAgent);
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                    AllowAutoRedirect = false, // Don't auto-redirect so we can detect 301/302
                    MaxAutomaticRedirections = 5,
                    ConnectCallback = async (context, cancellationToken) =>
                    {
                        // SSRF Protection: Resolve DNS and block private IPs
                        var entry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, cancellationToken);
                        foreach (var address in entry.AddressList)
                        {
                            if (IsPrivateIp(address))
                            {
                                throw new HttpRequestException($"Connection to private IP address {address} is blocked for security reasons");
                            }
                        }

                        var socket = new System.Net.Sockets.Socket(
                            System.Net.Sockets.SocketType.Stream,
                            System.Net.Sockets.ProtocolType.Tcp);
                        await socket.ConnectAsync(entry.AddressList, context.DnsEndPoint.Port, cancellationToken);
                        return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
                    }
                };
                return handler;
            });

            // Register services
            builder.Services.AddScoped<IMarkdownParserService, MarkdownParserService>();
            builder.Services.AddScoped<ILinkValidatorService, LinkValidatorService>();
            builder.Services.AddScoped<IGitHubRepoService, GitHubRepoService>();
            builder.Services.AddScoped<ILinkCheckOrchestrator, LinkCheckOrchestrator>();
            builder.Services.AddScoped<IAnchorSuggestionService, AnchorSuggestionService>();
            builder.Services.AddScoped<IReportGeneratorService, ReportGeneratorService>();

            // Add Razor Pages
            builder.Services.AddRazorPages();

            // Add Anti-Forgery token support
            builder.Services.AddAntiforgery();

            var app = builder.Build();

            // Configure the HTTP request pipeline
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseSerilogRequestLogging();

            // Add IP-based rate limiting middleware (FR-036: 5 req/min/IP)
            app.UseMiddleware<IpRateLimitMiddleware>();

            app.UseRouting();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapRazorPages().WithStaticAssets();

            // Map SSE endpoint
            app.MapPost("/api/check/sse", async (HttpContext context, ILinkCheckOrchestrator orchestrator) =>
            {
                await LinkCheckSseEndpoint.HandleAsync(context, orchestrator);
            });

            app.Run();
        }
        catch (Exception ex)
        {
            // Use builder's logger if available; otherwise use a simple console logger
            if (ex.GetType().Name != "HostAbortedException")
            {
                Console.Error.WriteLine($"Application terminated unexpectedly: {ex}");
            }
            throw;
        }
    }

    /// <summary>
    /// Checks if an IP address is private (for SSRF protection)
    /// </summary>
    private static bool IsPrivateIp(IPAddress address)
    {
        var bytes = address.GetAddressBytes();

        // IPv4 private ranges
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            // 127.0.0.0/8 (loopback)
            if (bytes[0] == 127) return true;

            // 10.0.0.0/8
            if (bytes[0] == 10) return true;

            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;

            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168) return true;
        }

        // IPv6 loopback (::1)
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            if (IPAddress.IsLoopback(address)) return true;
        }

        return false;
    }
}

