using System.ComponentModel.DataAnnotations;

using AddressEntity = Monburan.Core.Entities.Address;

namespace Monburan.Web.Components.Pages;

/// <summary>
/// Modelo do formulário de endereço. As regras espelham os limites das colunas
/// (AddressConfiguration): sem elas o formulário aceitava "Brasil" no país (coluna char(5))
/// e o save derrubava a tela. Fica fora da entidade para não alterar o modelo do EF.
/// </summary>
public sealed class AddressForm
{
    [Required(ErrorMessage = "Informe o CEP.")]
    [RegularExpression(@"^\d{5}-?\d{3}$", ErrorMessage = "Informe o CEP no formato 00000-000.")]
    public string? ZipCode { get; set; }

    [Required(ErrorMessage = "Informe a rua ou logradouro.")]
    [StringLength(200, ErrorMessage = "A rua deve ter no máximo {1} caracteres.")]
    public string? Street { get; set; }

    [StringLength(20, ErrorMessage = "O número deve ter no máximo {1} caracteres.")]
    public string? Number { get; set; }

    [StringLength(100, ErrorMessage = "O complemento deve ter no máximo {1} caracteres.")]
    public string? Complement { get; set; }

    [StringLength(100, ErrorMessage = "O bairro deve ter no máximo {1} caracteres.")]
    public string? District { get; set; }

    [Required(ErrorMessage = "Informe a cidade.")]
    [StringLength(100, ErrorMessage = "A cidade deve ter no máximo {1} caracteres.")]
    public string? City { get; set; }

    [Required(ErrorMessage = "Informe a UF.")]
    [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "Informe a UF com 2 letras (ex.: SP).")]
    public string? State { get; set; }

    [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "Use a sigla do país com 2 letras (ex.: BR).")]
    public string? Country { get; set; } = "BR";

    public static AddressForm From(AddressEntity address) => new()
    {
        ZipCode = address.ZipCode,
        Street = address.Street,
        Number = address.Number,
        Complement = address.Complement,
        District = address.District,
        City = address.City,
        State = address.State,
        Country = string.IsNullOrWhiteSpace(address.Country) ? "BR" : address.Country.Trim()
    };

    public void ApplyTo(AddressEntity address)
    {
        address.ZipCode = ZipCode?.Trim();
        address.Street = Street?.Trim();
        address.Number = Number?.Trim();
        address.Complement = Complement?.Trim();
        address.District = District?.Trim();
        address.City = City?.Trim();
        address.State = State?.Trim().ToUpperInvariant();
        address.Country = string.IsNullOrWhiteSpace(Country) ? "BR" : Country.Trim().ToUpperInvariant();
    }
}
