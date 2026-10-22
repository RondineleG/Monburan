using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Monburan.Web.Endpoints;

/// <summary>
/// Escrita da resposta dos endpoints de health check.
/// O <c>/health</c> (e o <c>/health/live</c>) mantêm o contrato antigo: texto simples com o
/// status. O <c>/health/ready</c> devolve JSON com o status de cada check.
/// </summary>
internal static class HealthCheckResponseWriter
{
    /// <summary>
    /// Escreve o status em texto simples ("Healthy"), igual ao que o <c>/health</c> entregava.
    /// </summary>
    /// <param name="context">Contexto da requisição.</param>
    /// <param name="report">Resultado da verificação de saúde.</param>
    public static Task WritePlainText(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "text/plain";
        return context.Response.WriteAsync(report.Status.ToString());
    }

    /// <summary>
    /// Escreve o resultado em JSON, com o status de cada check. Não leva a mensagem da
    /// exceção: o endpoint é público e o erro do banco carrega endereço e credencial.
    /// </summary>
    /// <param name="context">Contexto da requisição.</param>
    /// <param name="report">Resultado da verificação de saúde.</param>
    public static Task WriteJson(HttpContext context, HealthReport report)
    {
        var checks = new List<object>(report.Entries.Count);
        foreach (var entry in report.Entries)
        {
            checks.Add(new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1)
            });
        }

        return context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks
        });
    }
}
