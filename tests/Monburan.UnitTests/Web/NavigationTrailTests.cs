using FluentAssertions;

using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class NavigationTrailTests
{
    [Theory]
    [InlineData("/", NavRole.Candidate, "Início")]
    [InlineData("/painel", NavRole.Advertiser, "Visão geral")]
    [InlineData("/", NavRole.Admin, "Visão geral")]
    [InlineData("/jobs", NavRole.Candidate, "Vagas")]
    [InlineData("/jobs?q=dev&modo=remoto", NavRole.Visitor, "Vagas")]
    [InlineData("/minhas-candidaturas", NavRole.Candidate, "Minhas candidaturas")]
    [InlineData("/minhas-vagas", NavRole.Advertiser, "Minhas vagas")]
    [InlineData("/candidaturas-recebidas", NavRole.Advertiser, "Candidaturas recebidas")]
    [InlineData("/conta", NavRole.Candidate, "Conta e segurança")]
    [InlineData("/rota-que-nao-existe", NavRole.Candidate, "Monburan")]
    public void SectionPages_HaveASingleItem_TheCurrentPage(string path, NavRole role, string label)
    {
        var trail = NavigationTrail.For(path, role);

        trail.Should().ContainSingle().Which.Should().Be(new TrailItem(label));
    }

    [Theory]
    [InlineData("/vagas/nova", "Minhas vagas", "/minhas-vagas", "Publicar vaga")]
    [InlineData("/vagas/2b1c6e0e-6a3a-4f2b-9a51-0b8f2d0b4c11/editar", "Minhas vagas", "/minhas-vagas", "Editar vaga")]
    [InlineData("/vagas/2b1c6e0e-6a3a-4f2b-9a51-0b8f2d0b4c11/candidaturas", "Candidaturas recebidas", "/candidaturas-recebidas", "Por vaga")]
    [InlineData("/jobs/2b1c6e0e-6a3a-4f2b-9a51-0b8f2d0b4c11", "Vagas", "/jobs", "Vaga")]
    [InlineData("/candidatar/2b1c6e0e-6a3a-4f2b-9a51-0b8f2d0b4c11", "Vagas", "/jobs", "Candidatura")]
    [InlineData("/curriculos/2b1c6e0e-6a3a-4f2b-9a51-0b8f2d0b4c11/editar", "Meus currículos", "/curriculos", "Editar currículo")]
    [InlineData("/curriculos/2b1c6e0e-6a3a-4f2b-9a51-0b8f2d0b4c11", "Meus currículos", "/curriculos", "Currículo")]
    [InlineData("/enderecos/2b1c6e0e-6a3a-4f2b-9a51-0b8f2d0b4c11/editar", "Endereços", "/enderecos", "Editar endereço")]
    [InlineData("/conta/alterar-senha", "Conta e segurança", "/conta", "Alterar senha")]
    [InlineData("/admin/auditoria", "Administração", "/", "Auditoria")]
    [InlineData("/empresa/logo", "Perfil da empresa", "/perfil/empresa", "Logo")]
    public void Subpages_LinkOneLevelUp_AndEndOnTheCurrentPage(string path, string parent, string parentHref, string current)
    {
        var trail = NavigationTrail.For(path, NavRole.Candidate);

        trail.Should().HaveCount(2);
        trail[0].Should().Be(new TrailItem(parent, parentHref));
        trail[1].Should().Be(new TrailItem(current));
    }

    [Fact]
    public void LastItem_IsNeverALink()
    {
        foreach (var path in new[] { "/", "/vagas/nova", "/conta/email", "/admin/vagas", "/enderecos" })
            NavigationTrail.For(path, NavRole.Advertiser)[^1].Href.Should().BeNull(path);
    }
}
