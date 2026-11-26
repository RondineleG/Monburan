using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Monburan.Core.Enums;
using Monburan.Core.UseCases.Migrations;
using Monburan.RecruivaMigration;
using Monburan.Web.Data;

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
if (args.Length == 0)
    return Usage();

if (args[0] == "apply")
{
    var values = Pairs(args.Skip(1).ToArray());
    if (!Required(values, "--input", out var input) || !Required(values, "--output", out var output) || !Required(values, "--uploads-source", out var uploadsSource) ||
        !Required(values, "--uploads-target", out var uploadsTarget))
        return Usage("apply --input exportacao.json --uploads-source <dir> --uploads-target <dir> --output relatorio.json [--batch guid]; defina MONBURAN_MIGRATION_CONNECTION no ambiente");
    var connection = Environment.GetEnvironmentVariable("MONBURAN_MIGRATION_CONNECTION");
    if (string.IsNullOrWhiteSpace(connection)) return Usage("Defina MONBURAN_MIGRATION_CONNECTION no ambiente.");
    var export = await ReadJson<RecruivaMigrationExport>(input);
    var dryRunResult = await new BuildRecruivaMigrationDryRunUseCase().ExecuteAsync(export);
    if (dryRunResult.Status != EResultStatus.Success || dryRunResult.Data is null)
        return Usage(dryRunResult.Message);
    var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options;
    await using var context = new ApplicationDbContext(options);
    var batch = values.TryGetValue("--batch", out var batchText) && Guid.TryParse(batchText, out var parsed) ? parsed : (Guid?)null;
    var report = await RecruivaMigrationApplier.ApplyAsync(context, export, dryRunResult.Data, uploadsSource, uploadsTarget, batch);
    await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, jsonOptions) + Environment.NewLine);
    Console.WriteLine(report.Applied ? $"Corte aplicado: lote {report.BatchId}." : $"Corte bloqueado: {report.Problems.Count} problema(s); destino não alterado.");
    return report.Applied ? 0 : 2;
}

if (args[0] == "rollback")
{
    var values = Pairs(args.Skip(1).ToArray());
    if (!Required(values, "--report", out var reportPath) || !Required(values, "--output", out var output))
        return Usage("rollback --report aplicacao.json --output rollback.json; defina MONBURAN_MIGRATION_CONNECTION no ambiente");
    var connection = Environment.GetEnvironmentVariable("MONBURAN_MIGRATION_CONNECTION");
    if (string.IsNullOrWhiteSpace(connection)) return Usage("Defina MONBURAN_MIGRATION_CONNECTION no ambiente.");
    var applyReport = await ReadJson<RecruivaMigrationApplyReport>(reportPath);
    var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options;
    await using var context = new ApplicationDbContext(options);
    var rollback = await RecruivaMigrationApplier.RollbackAsync(context, applyReport);
    await File.WriteAllTextAsync(output, JsonSerializer.Serialize(rollback, jsonOptions) + Environment.NewLine);
    Console.WriteLine($"Rollback executado: {rollback.RemovedCounts.Values.Sum()} registros e {rollback.RemovedFiles} arquivo(s).");
    return 0;
}

if (args.Length != 4 || args[0] != "--input" || args[2] != "--output")
    return Usage();

var migrationExport = await ReadJson<RecruivaMigrationExport>(args[1]);
var result = await new BuildRecruivaMigrationDryRunUseCase().ExecuteAsync(migrationExport);
if (result.Status != EResultStatus.Success || result.Data is null)
    return Usage(result.Message);
if (Path.GetFullPath(args[1]) == Path.GetFullPath(args[3]))
    return Usage("A saída precisa ser diferente da exportação de entrada para não sobrescrever a origem.");
await File.WriteAllTextAsync(args[3], JsonSerializer.Serialize(result.Data, jsonOptions) + Environment.NewLine);
Console.WriteLine(result.Data.CanConfirm ? $"Dry-run reconciliado: {result.Data.Counts.Values.Sum()} registros, versão {result.Data.Version}." : $"Dry-run bloqueado: {result.Data.Problems.Count} problema(s); origem e destino não foram alterados.");
return result.Data.CanConfirm ? 0 : 2;

static Dictionary<string, string> Pairs(string[] args)
{
    var values = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var index = 0; index + 1 < args.Length; index += 2) values[args[index]] = args[index + 1];
    return values;
}
static bool Required(Dictionary<string, string> values, string key, out string value) => values.TryGetValue(key, out value!) && !string.IsNullOrWhiteSpace(value);
static async Task<T> ReadJson<T>(string path)
{
    try { return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new InvalidDataException("JSON vazio."); }
    catch (Exception ex) when (ex is JsonException or InvalidDataException) { throw new InvalidOperationException($"JSON inválido em {path}: {ex.Message}"); }
}
static int Usage(string? message = null)
{
    Console.Error.WriteLine(message ?? "Uso: dotnet run --project tools/Monburan.RecruivaMigration -- --input exportacao.json --output relatorio.json");
    return 1;
}
