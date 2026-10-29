using FluentAssertions;

using Monburan.Core.Services;

namespace Monburan.UnitTests.Services;

public class AdminAuditActionsTests
{
    [Fact]
    public void EveryKnownAction_HasADistinctReadableLabel()
    {
        var labels = AdminAuditActions.All.Select(AdminAuditActions.Label).ToList();

        labels.Should().OnlyHaveUniqueItems();
        labels.Should().OnlyContain(label => !string.IsNullOrWhiteSpace(label) && !label.Contains('.') && !label.Contains('_'));
    }

    [Fact]
    public void TheKeysAreStable_BecauseTheyAreWhatTheDatabaseStores()
    {
        AdminAuditActions.All.Should().BeEquivalentTo(
            "account.deactivated", "account.activated", "account.unlocked", "account.email_confirmed", "account.sessions_revoked",
            "account.two_factor_reset", "account.admin_granted", "account.admin_revoked", "account.plan_changed", "job.status_changed", "job.deleted",
            "ai_provider.saved", "ai_provider.default_changed", "ai_provider.deleted", "submission.decided");
    }

    [Fact]
    public void AnUnknownAction_IsShownAsItsKey_SoOldRecordsNeverDisappear()
    {
        AdminAuditActions.Label("coisa.antiga").Should().Be("coisa.antiga");
        AdminAuditActions.TargetLabel("outro").Should().Be("outro");
    }

    [Fact]
    public void TheTargetsHaveLabelsToo()
    {
        AdminAuditActions.Targets.Select(AdminAuditActions.TargetLabel).Should().BeEquivalentTo("Conta", "Vaga", "Provedor de IA", "Sugestão de vaga");
    }
}
