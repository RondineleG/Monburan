using FluentAssertions;

using Monburan.Core.Catalogs;

namespace Monburan.UnitTests.Services;

public class JobCategoriesTests
{
    [Fact]
    public void All_FollowsTheAgreedOrder_WithOtherAreasLast()
    {
        JobCategories.All.Should().Equal(
            "Administrativo",
            "Atendimento ao cliente",
            "Comercial e vendas",
            "Contabilidade e finanças",
            "Design e criação",
            "Educação",
            "Engenharia e manufatura",
            "Indústria e produção",
            "Jurídico",
            "Logística e transporte",
            "Marketing e comunicação",
            "Recursos humanos",
            "Saúde e bem-estar",
            "Serviços gerais",
            "Tecnologia da informação",
            "Varejo e lojas",
            "Outras áreas");
    }

    [Fact]
    public void All_HasNoDuplicates()
    {
        JobCategories.All.Should().OnlyHaveUniqueItems();
    }
}
