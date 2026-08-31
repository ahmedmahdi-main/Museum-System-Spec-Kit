using System.Reflection;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Domain.Tests.Photography;

public sealed class PhotographyRequestTests
{
    private static readonly DateTimeOffset RequestedAt = new(2026, 8, 24, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CompletedAt = new(2026, 8, 24, 11, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CancelledAt = new(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Valid_request_starts_pending_with_no_terminal_metadata()
    {
        var artifactId = Guid.NewGuid();
        var request = PhotographyRequest.Create(
            artifactId,
            PhotographyPurpose.PreMaintenance,
            " requester-1 ",
            RequestedAt);

        Assert.NotEqual(Guid.Empty, request.PhotographyRequestId);
        Assert.Equal(artifactId, request.ArtifactId);
        Assert.Equal(PhotographyPurpose.PreMaintenance, request.Purpose);
        Assert.Equal("requester-1", request.RequestedByUserId);
        Assert.Equal(RequestedAt, request.RequestedAt);
        Assert.Equal(PhotographyRequestStatus.Pending, request.Status);
        Assert.Null(request.FulfillingPhotographySetId);
        Assert.Null(request.CompletedByUserId);
        Assert.Null(request.CompletedAt);
        Assert.Null(request.CancelledByUserId);
        Assert.Null(request.CancelledAt);
        Assert.Equal(0, request.ConcurrencyToken);
    }

    [Fact]
    public void Creation_requires_artifact_purpose_requester_and_requested_timestamp()
    {
        Assert.Throws<ArgumentException>(() => CreateRequest(artifactId: Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRequest(purpose: (PhotographyPurpose)999));
        Assert.Throws<ArgumentException>(() => CreateRequest(requestedByUserId: " "));
        Assert.Throws<ArgumentException>(() => CreateRequest(requestedAt: new DateTimeOffset()));

        Assert.Equal([
            nameof(PhotographyRequestStatus.Pending),
            nameof(PhotographyRequestStatus.Completed),
            nameof(PhotographyRequestStatus.Cancelled)
        ], Enum.GetNames<PhotographyRequestStatus>());
    }

    [Fact]
    public void Pending_request_completes_with_valid_fulfillment()
    {
        var artifactId = Guid.NewGuid();
        var setId = Guid.NewGuid();
        var request = CreateRequest(artifactId);

        request.Complete(
            setId,
            artifactId,
            PhotographyPurpose.GeneralDocumentation,
            fulfillingSetHasAvailableImage: true,
            " photographer-1 ",
            CompletedAt);

        Assert.Equal(PhotographyRequestStatus.Completed, request.Status);
        Assert.Equal(setId, request.FulfillingPhotographySetId);
        Assert.Equal("photographer-1", request.CompletedByUserId);
        Assert.Equal(CompletedAt, request.CompletedAt);
        Assert.Null(request.CancelledByUserId);
        Assert.Null(request.CancelledAt);
        Assert.Equal(1, request.ConcurrencyToken);
    }

    [Fact]
    public void Completion_requires_valid_matching_fulfillment_facts_and_actor_time()
    {
        var artifactId = Guid.NewGuid();
        var request = CreateRequest(artifactId);

        Assert.Throws<ArgumentException>(() => request.Complete(
            Guid.Empty,
            artifactId,
            PhotographyPurpose.GeneralDocumentation,
            true,
            "photographer-1",
            CompletedAt));

        Assert.Throws<ArgumentException>(() => request.Complete(
            Guid.NewGuid(),
            Guid.Empty,
            PhotographyPurpose.GeneralDocumentation,
            true,
            "photographer-1",
            CompletedAt));

        Assert.Throws<InvalidOperationException>(() => request.Complete(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PhotographyPurpose.GeneralDocumentation,
            true,
            "photographer-1",
            CompletedAt));

        Assert.Throws<InvalidOperationException>(() => request.Complete(
            Guid.NewGuid(),
            artifactId,
            PhotographyPurpose.PostMaintenance,
            true,
            "photographer-1",
            CompletedAt));

        Assert.Throws<InvalidOperationException>(() => request.Complete(
            Guid.NewGuid(),
            artifactId,
            PhotographyPurpose.GeneralDocumentation,
            false,
            "photographer-1",
            CompletedAt));

        Assert.Throws<ArgumentException>(() => request.Complete(
            Guid.NewGuid(),
            artifactId,
            PhotographyPurpose.GeneralDocumentation,
            true,
            " ",
            CompletedAt));

        Assert.Throws<ArgumentException>(() => request.Complete(
            Guid.NewGuid(),
            artifactId,
            PhotographyPurpose.GeneralDocumentation,
            true,
            "photographer-1",
            default));

        Assert.Equal(PhotographyRequestStatus.Pending, request.Status);
        Assert.Null(request.FulfillingPhotographySetId);
        Assert.Null(request.CompletedByUserId);
        Assert.Null(request.CompletedAt);
        Assert.Equal(0, request.ConcurrencyToken);
    }

    [Fact]
    public void Completed_request_rejects_repeated_completion_and_cancellation()
    {
        var artifactId = Guid.NewGuid();
        var request = CreateCompletedRequest(artifactId);

        Assert.Throws<InvalidOperationException>(() => request.Complete(
            Guid.NewGuid(),
            artifactId,
            PhotographyPurpose.GeneralDocumentation,
            true,
            "photographer-2",
            CompletedAt.AddMinutes(5)));
        Assert.Throws<InvalidOperationException>(() => request.Cancel("requester-1", CancelledAt, actorHasManageAuthority: false));

        Assert.Equal(PhotographyRequestStatus.Completed, request.Status);
        Assert.NotNull(request.FulfillingPhotographySetId);
        Assert.Equal("photographer-1", request.CompletedByUserId);
        Assert.Null(request.CancelledByUserId);
        Assert.Null(request.CancelledAt);
        Assert.Equal(1, request.ConcurrencyToken);
    }

    [Fact]
    public void Original_requester_may_cancel_own_pending_request()
    {
        var request = CreateRequest();

        request.Cancel(" requester-1 ", CancelledAt, actorHasManageAuthority: false);

        Assert.Equal(PhotographyRequestStatus.Cancelled, request.Status);
        Assert.Equal("requester-1", request.CancelledByUserId);
        Assert.Equal(CancelledAt, request.CancelledAt);
        Assert.Null(request.FulfillingPhotographySetId);
        Assert.Null(request.CompletedByUserId);
        Assert.Null(request.CompletedAt);
        Assert.Equal(1, request.ConcurrencyToken);
    }

    [Fact]
    public void Manager_may_cancel_another_users_pending_request()
    {
        var request = CreateRequest();

        request.Cancel("manager-1", CancelledAt, actorHasManageAuthority: true);

        Assert.Equal(PhotographyRequestStatus.Cancelled, request.Status);
        Assert.Equal("manager-1", request.CancelledByUserId);
        Assert.Equal(CancelledAt, request.CancelledAt);
    }

    [Fact]
    public void Another_user_without_manage_authority_cannot_cancel()
    {
        var request = CreateRequest();

        Assert.Throws<UnauthorizedAccessException>(() => request.Cancel("other-user", CancelledAt, actorHasManageAuthority: false));

        Assert.Equal(PhotographyRequestStatus.Pending, request.Status);
        Assert.Null(request.CancelledByUserId);
        Assert.Null(request.CancelledAt);
        Assert.Equal(0, request.ConcurrencyToken);
    }

    [Fact]
    public void Cancellation_requires_actor_and_timestamp()
    {
        var request = CreateRequest();

        Assert.Throws<ArgumentException>(() => request.Cancel(" ", CancelledAt, actorHasManageAuthority: true));
        Assert.Throws<ArgumentException>(() => request.Cancel("requester-1", default, actorHasManageAuthority: false));

        Assert.Equal(PhotographyRequestStatus.Pending, request.Status);
    }

    [Fact]
    public void Cancelled_request_rejects_completion_and_repeated_cancellation()
    {
        var artifactId = Guid.NewGuid();
        var request = CreateRequest(artifactId);
        request.Cancel("requester-1", CancelledAt, actorHasManageAuthority: false);

        Assert.Throws<InvalidOperationException>(() => request.Complete(
            Guid.NewGuid(),
            artifactId,
            PhotographyPurpose.GeneralDocumentation,
            true,
            "photographer-1",
            CompletedAt));
        Assert.Throws<InvalidOperationException>(() => request.Cancel("manager-1", CancelledAt.AddMinutes(5), actorHasManageAuthority: true));

        Assert.Equal(PhotographyRequestStatus.Cancelled, request.Status);
        Assert.Equal("requester-1", request.CancelledByUserId);
        Assert.Equal(CancelledAt, request.CancelledAt);
        Assert.Null(request.FulfillingPhotographySetId);
        Assert.Null(request.CompletedByUserId);
        Assert.Null(request.CompletedAt);
        Assert.Equal(1, request.ConcurrencyToken);
    }

    [Fact]
    public void Request_does_not_expose_reopen_reset_or_cross_terminal_mutators()
    {
        var publicMethodNames = typeof(PhotographyRequest)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .ToArray();
        var memberNames = typeof(PhotographyRequest)
            .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(member => member.Name)
            .ToArray();

        Assert.Contains(nameof(PhotographyRequest.Complete), publicMethodNames);
        Assert.Contains(nameof(PhotographyRequest.Cancel), publicMethodNames);
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Reopen", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Reset", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(memberNames, name => name.Contains("MuseumNumber", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(memberNames, name => name.Contains("Category", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(memberNames, name => name.Contains("Custody", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(memberNames, name => name.Contains("Movement", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(memberNames, name => name.Contains("Location", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(memberNames, name => name.Contains("Documentation", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(memberNames, name => name.Contains("Laboratory", StringComparison.OrdinalIgnoreCase));
    }

    private static PhotographyRequest CreateCompletedRequest(Guid artifactId)
    {
        var request = CreateRequest(artifactId);
        request.Complete(
            Guid.NewGuid(),
            artifactId,
            PhotographyPurpose.GeneralDocumentation,
            true,
            "photographer-1",
            CompletedAt);
        return request;
    }

    private static PhotographyRequest CreateRequest(
        Guid? artifactId = null,
        PhotographyPurpose purpose = PhotographyPurpose.GeneralDocumentation,
        string requestedByUserId = "requester-1",
        DateTimeOffset? requestedAt = null) =>
        PhotographyRequest.Create(
            artifactId ?? Guid.NewGuid(),
            purpose,
            requestedByUserId,
            requestedAt ?? RequestedAt);
}
