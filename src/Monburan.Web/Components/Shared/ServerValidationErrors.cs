using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Monburan.Web.Components.Shared;

/// <summary>
/// Leva erros de validação do backend (UseCases) para os campos do <see cref="EditForm"/>:
/// cada mensagem aparece no <c>ValidationMessage</c> do campo correspondente, como as
/// validações do próprio formulário. Colocar dentro do EditForm.
/// </summary>
public sealed class ServerValidationErrors : ComponentBase, IDisposable
{
    private ValidationMessageStore? store;
    private EditContext? subscribedContext;

    [CascadingParameter]
    private EditContext? CurrentEditContext { get; set; }

    /// <summary>Campo do domínio → propriedade do modelo, quando os nomes diferem (ex.: Salary → SalaryMax).</summary>
    [Parameter]
    public IReadOnlyDictionary<string, string>? FieldMap { get; set; }

    protected override void OnParametersSet()
    {
        if (CurrentEditContext is null)
            throw new InvalidOperationException($"{nameof(ServerValidationErrors)} precisa estar dentro de um EditForm.");

        if (ReferenceEquals(subscribedContext, CurrentEditContext))
            return;

        Unsubscribe();
        subscribedContext = CurrentEditContext;
        store = new ValidationMessageStore(CurrentEditContext);
        CurrentEditContext.OnValidationRequested += OnValidationRequested;
        CurrentEditContext.OnFieldChanged += OnFieldChanged;
    }

    /// <summary>
    /// Exibe os erros nos campos. Devolve as mensagens que não correspondem a nenhum
    /// campo do formulário — a página deve mostrá-las de outra forma (ex.: toast).
    /// </summary>
    public IReadOnlyList<string> Display(IReadOnlyDictionary<string, string[]> errors)
    {
        if (store is null || CurrentEditContext is null)
            return errors.SelectMany(e => e.Value).ToList();

        store.Clear();
        var unmatched = new List<string>();
        var model = CurrentEditContext.Model;

        foreach (var (field, messages) in errors)
        {
            var property = ResolveProperty(field);
            if (property is not null && model.GetType().GetProperty(property) is not null)
                store.Add(new FieldIdentifier(model, property), messages);
            else
                unmatched.AddRange(messages);
        }

        CurrentEditContext.NotifyValidationStateChanged();
        return unmatched;
    }

    public void Clear()
    {
        store?.Clear();
        CurrentEditContext?.NotifyValidationStateChanged();
    }

    private string? ResolveProperty(string field)
    {
        if (string.IsNullOrWhiteSpace(field))
            return null;

        if (FieldMap is not null && FieldMap.TryGetValue(field, out var mapped))
            return mapped;

        // "Location.City" → "City"
        var lastDot = field.LastIndexOf('.');
        return lastDot >= 0 ? field[(lastDot + 1)..] : field;
    }

    private void OnValidationRequested(object? sender, ValidationRequestedEventArgs e) => store?.Clear();

    // Ao editar o campo, o erro do servidor daquele campo sai — senão ficaria "preso"
    private void OnFieldChanged(object? sender, FieldChangedEventArgs e) => store?.Clear(e.FieldIdentifier);

    private void Unsubscribe()
    {
        if (subscribedContext is null)
            return;

        subscribedContext.OnValidationRequested -= OnValidationRequested;
        subscribedContext.OnFieldChanged -= OnFieldChanged;
        subscribedContext = null;
    }

    public void Dispose() => Unsubscribe();
}
