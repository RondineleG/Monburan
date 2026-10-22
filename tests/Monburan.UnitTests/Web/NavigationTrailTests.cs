using FluentAssertions;

using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class NavigationTrailTests
{
    [Theory]
    [InlineData("/", NavRole.Candidate, "Início")]
    [InlineData("/dashboard", NavRole.Advertiser, "Visão geral")]
    [InlineData("/", NavRole.Admin, "Visão geral")]
    [InlineData("/jobs", NavRole.Candidate, "Vagas")]
    [InlineData("/jobs?q=dev&modo=remoto", NavRole.Visitor, "Vagas")]
    [InlineData("/my-applications", NavRole.Candidate, "Minhas candidaturas")]
    [InlineData("/my-jobs", NavRole.Advertiser, "Minhas vagas")]
    [InlineData("/received-applications", NavRole.Advertiser, "Candidaturas recebidas")]
    [InlineData("/Account/Manage", NavRole.Candidate, "Conta e segurança")]
    [InlineData("/rota-que-nao-existe", NavRole.Candidate, "Monburan")]
    public void SectionPages_HaveASingleItem_TheCurrentPage(string path, NavRole role, string label)
    {
        var trail = NavigationTrail.For(path, role);

        trail.Should().ContainSingle().Which.Should().Be(new TrailItem(label));
    }

    [Theory]
    [InlineData("/jobs/create", "Minhas vagas", "/my-jobs", "Publicar vaga")]
    [InlineData("/jobs/edit/2b1c6e0e-6a3a-4f2b-9a51-0b8f2d0b4c11", "Minhas vagas", "/my-jobs", "Editar vaga")]
    [InlineData("/jobs/2b1c6e0e-6a3a-4f2b-9a51-0b8f2d0b4c11/applications", "Candidaturas recebidas", "/received-applications", "Por vaga")]
    [InlineData("/jobs/2b1c6e0e-6a3a-4f2b-9a51-0b8f2d0b4c11", "Vagas", "/jobs", "Vaga")]
    [InlineData("/apply/2b1c6e0e-6a3a-4f2b-9a51-0b8f2d0b4c11", "Vagas", "/jobs", "Candidatura")]
    [InlineData("/resumes/2b1c6e0e-6a3a-4f2b-9a51-0b8f2d0b4c11/edit", "Meus currículos", "/resumes", "Editar currículo")]
    [InlineData("/resumes/2b1c6e0e-6a3a-4f2b-9a51-0b8f2d0b4c11", "Meus currículos", "/resumes", "Currículo")]
    [InlineData("/address/edit/2b1c6e0e-6a3a-4f2b-9a51-0b8f2d0b4c11", "Endereços", "/address/list", "Editar endereço")]
    [InlineData("/Account/Manage/ChangePassword", "Conta e segurança", "/Account/Manage", "Alterar senha")]
    [InlineData("/admin/audit", "Administração", "/", "Auditoria")]
    [InlineData("/advertiser/upload-logo", "Perfil da empresa", "/profile/advertiser", "Logo")]
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
        foreach (var path in new[] { "/", "/jobs/create", "/Account/Manage/Email", "/admin/jobs", "/address/list" })
            NavigationTrail.For(path, NavRole.Advertiser)[^1].Href.Should().BeNull(path);
    }
}
