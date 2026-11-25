using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FechaTurma.Desktop.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

internal static class Program
{
    private const string Id = "a43545ae-a369-4ac1-8b3c-a8509409b4ab";
    private const string Collection = "/api/v1/closures";
    private const string Summary = "\"id\":\"" + Id + "\",\"organization\":2,\"term\":\"2026.1\",\"classCode\":\"C01\",\"title\":\"Álgebra\",\"studentCount\":1,\"approvedCount\":1,\"failedCount\":0,\"importedAt\":\"2026-01-01T12:00:00Z\"";
    private const string Pending = "{ " + Summary + ",\"status\":\"pending\",\"version\":7,\"closedAt\":null";
    private const string Closed = "{ " + Summary + ",\"status\":\"closed\",\"version\":8,\"closedAt\":\"2026-01-01T13:00:00Z\"";
    private const string Rows = ",\"rows\":[{\"registration\":\"001\",\"name\":\"João\",\"grade\":8.5,\"attendance\":91.25,\"outcome\":\"approved\"}]";

    private static async Task Main()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        var closeAttempts = 0;
        app.Run(async context =>
        {
            var path = context.Request.Path.Value;
            if (path == Collection)
            {
                if (context.Request.Headers.Authorization != "Bearer local-test-key")
                {
                    context.Response.StatusCode = 401;
                    return;
                }
                await context.Response.WriteAsync("[" + Pending + "}]");
            }
            else if (path == Collection + "/" + Id)
                await context.Response.WriteAsync(Pending + Rows + ",\"audit\":[{\"action\":\"imported\",\"actor\":\"Importador\",\"at\":\"2026-01-01T12:00:00Z\"}]}");
            else if (path == Collection + "/" + Id + "/close" && context.Request.Method == "POST")
            {
                if (context.Request.Headers.Authorization != "Bearer local-test-key")
                    context.Response.StatusCode = 403;
                else if (context.Request.ContentType != "application/json; charset=utf-8")
                    context.Response.StatusCode = 415;
                else
                {
                    using (var payload = await JsonDocument.ParseAsync(context.Request.Body))
                    {
                        var fields = payload.RootElement.EnumerateObject().ToArray();
                        if (fields.Length != 1 || fields[0].Name != "version" || fields[0].Value.GetInt32() != 7 || ++closeAttempts != 1)
                        {
                            context.Response.StatusCode = 409;
                            await context.Response.WriteAsync("{\"code\":\"conflict\",\"message\":\"Unsafe server text\"}");
                        }
                        else
                            await context.Response.WriteAsync(Closed + Rows + ",\"audit\":[{\"action\":\"closed\",\"actor\":\"Coordenação\",\"at\":\"2026-01-01T13:00:00Z\"}]}");
                    }
                }
            }
            else if (path == Collection + "/" + Guid.Empty)
                context.Response.Redirect(Collection);
            else
                context.Response.StatusCode = 404;
        });
        await app.StartAsync();
        try
        {
            var origin = app.Urls.Single();
            using (var client = new ClosureClient(origin))
            {
                var list = await client.ListAsync("local-test-key");
                Check(list.Length == 1 && list[0].Id == Id && list[0].Title == "Álgebra" && list[0].Organization == 2, "typed array");
                var detail = await client.DetailAsync(Guid.Parse(Id), "local-test-key");
                Check(detail.Version == 7 && detail.Rows.Single().Registration == "001" && detail.Rows[0].Grade == 8.5m && detail.Rows[0].Attendance == 91.25m && detail.Audit.Single().Action == "imported", "typed detail");
                var closed = await client.CloseAsync(Guid.Parse(Id), detail.Version, "local-test-key");
                Check(closed.Status == "closed" && closed.Version == 8 && closed.Audit.Single().Actor == "Coordenação", "authoritative close result");
                await ExpectStatus(HttpStatusCode.Conflict, () => client.CloseAsync(Guid.Parse(Id), detail.Version, "local-test-key"));
                await ExpectStatus(HttpStatusCode.Unauthorized, () => client.ListAsync(""));
                await ExpectStatus(HttpStatusCode.Forbidden, () => client.CloseAsync(Guid.Parse(Id), detail.Version, "invalid-role"));
                await ExpectStatus(HttpStatusCode.NotFound, () => client.DetailAsync(Guid.NewGuid(), "local-test-key"));
                await ExpectStatus(HttpStatusCode.Found, () => client.DetailAsync(Guid.Empty, "local-test-key"));
            }
            foreach (var invalid in new[] { "https://example.invalid", "http://localhost.example.invalid", "http://user@localhost:5184", "http://localhost/path", "http://localhost/?query=1", "http://localhost/#fragment", "ftp://127.0.0.1" })
            {
                try { using (new ClosureClient(invalid)) { } }
                catch (ArgumentException) { continue; }
                throw new Exception("Expected rejection of non-loopback origin or non-origin URI.");
            }
            Console.WriteLine("PASS: list, detail, versioned close, HTTP 401/403/404/409, redirect and origin isolation");
        }
        finally { await app.DisposeAsync(); }
    }

    private static async Task ExpectStatus(HttpStatusCode status, Func<Task> action)
    {
        try { await action(); }
        catch (ClosureHttpException error)
        {
            Check(error.Status == status, "preserved HTTP status " + status);
            Check(!error.Message.Contains("Unsafe server text"), "does not echo server responses");
            return;
        }
        throw new Exception("Expected HTTP failure: " + status);
    }

    private static void Check(bool result, string behavior)
    {
        if (!result) throw new Exception("Failed: " + behavior);
    }
}
