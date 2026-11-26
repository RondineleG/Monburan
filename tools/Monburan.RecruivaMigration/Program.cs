using System.Text.Json;

using Monburan.Core.Enums;
using Monburan.Core.UseCases.Migrations;

if (args.Length != 4 || args[0] != "--input" || args[2] != "--output")
{
    Console.Error.WriteLine("Uso: dotnet run --project tools/Monburan.RecruivaMigration -- --input exportacao.json --output relatorio.json");
    return 1;
}

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
RecruivaMigrationExport? export;
try
{
    export = JsonSerializer.Deserialize<RecruivaMigrationExport>(await File.ReadAllTextAsync(args[1]), jsonOptions);
}
catch (JsonException ex)
{
    Console.Error.WriteLine($"Exportação inválida: {ex.Message}");
    return 1;
}

if (export is null)
{
    Console.Error.WriteLine("Exportação vazia.");
    return 1;
}

var result = await new BuildRecruivaMigrationDryRunUseCase().ExecuteAsync(export);
if (result.Status != EResultStatus.Success || result.Data is null)
{
    Console.Error.WriteLine(result.Message);
    return 1;
}

if (Path.GetFullPath(args[1]) == Path.GetFullPath(args[3]))
{
    Console.Error.WriteLine("A saída precisa ser diferente da exportação de entrada para não sobrescrever a origem.");
    return 1;
}

var reportJson = JsonSerializer.Serialize(result.Data, jsonOptions);
await File.WriteAllTextAsync(args[3], reportJson + Environment.NewLine);
Console.WriteLine(result.Data.CanConfirm
    ? $"Dry-run reconciliado: {result.Data.Counts.Values.Sum()} registros, versão {result.Data.Version}."
    : $"Dry-run bloqueado: {result.Data.Problems.Count} problema(s); origem e destino não foram alterados.");
return result.Data.CanConfirm ? 0 : 2;
