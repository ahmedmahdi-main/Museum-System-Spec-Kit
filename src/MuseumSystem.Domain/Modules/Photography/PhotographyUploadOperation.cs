namespace MuseumSystem.Domain.Modules.Photography;

public sealed class PhotographyUploadOperation
{
    private readonly List<PhotographyUploadFileOutcome> _fileOutcomes = [];

    private PhotographyUploadOperation()
    {
    }

    private PhotographyUploadOperation(string actorUserId, PhotographyUploadOperationKind operationKind, string idempotencyKey, string requestFingerprint, Guid artifactId, Guid? photographySetId = null)
    {
        var validatedOperationKind = PhotographyEnumValidation.RequireDefined(operationKind, nameof(operationKind));

        PhotographyUploadOperationId = Guid.NewGuid();
        ActorUserId = RequireText(actorUserId, nameof(actorUserId));
        OperationKind = validatedOperationKind;
        IdempotencyKey = RequireText(idempotencyKey, nameof(idempotencyKey));
        RequestFingerprint = RequireText(requestFingerprint, nameof(requestFingerprint));
        ArtifactId = RequireGuid(artifactId, nameof(artifactId));
        PhotographySetId = ResolveInitialPhotographySetId(validatedOperationKind, photographySetId);
        Status = PhotographyUploadOperationStatus.InProgress;
        StartedAt = DateTimeOffset.UtcNow;
        LastSeenAt = StartedAt;
    }

    public Guid PhotographyUploadOperationId { get; private set; }
    public string ActorUserId { get; private set; } = string.Empty;
    public PhotographyUploadOperationKind OperationKind { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestFingerprint { get; private set; } = string.Empty;
    public Guid ArtifactId { get; private set; }
    public Guid? PhotographySetId { get; private set; }
    public PhotographyUploadOperationStatus Status { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }
    public int ConcurrencyToken { get; private set; }
    public IReadOnlyCollection<PhotographyUploadFileOutcome> FileOutcomes => _fileOutcomes.AsReadOnly();

    public static PhotographyUploadOperation Start(string actorUserId, PhotographyUploadOperationKind operationKind, string idempotencyKey, string requestFingerprint, Guid artifactId, Guid? photographySetId = null) =>
        new(actorUserId, operationKind, idempotencyKey, requestFingerprint, artifactId, photographySetId);

    public bool MatchesFingerprint(string requestFingerprint) => RequestFingerprint == RequireText(requestFingerprint, nameof(requestFingerprint));

    public void AttachPhotographySet(Guid photographySetId)
    {
        if (Status is not (PhotographyUploadOperationStatus.InProgress or PhotographyUploadOperationStatus.RecoveryNeeded))
        {
            throw new InvalidOperationException("The photography set can only be attached while the upload operation is in progress or awaiting recovery.");
        }

        if (OperationKind != PhotographyUploadOperationKind.CreateSetUpload)
        {
            throw new InvalidOperationException("Only create-set upload operations can attach a newly established photography set.");
        }

        var requiredSetId = RequireGuid(photographySetId, nameof(photographySetId));
        if (PhotographySetId == requiredSetId)
        {
            return;
        }

        if (PhotographySetId.HasValue)
        {
            throw new InvalidOperationException("The established photography set cannot be replaced.");
        }

        if (!_fileOutcomes.Any(outcome => outcome.Status == PhotographyUploadFileOutcomeStatus.Succeeded))
        {
            throw new InvalidOperationException("A create-set upload cannot establish a photography set until an existing file outcome has succeeded.");
        }

        PhotographySetId = requiredSetId;
        Touch();
    }

    public void AddFileOutcome(PhotographyUploadFileOutcome outcome)
    {
        if (Status != PhotographyUploadOperationStatus.InProgress)
        {
            throw new InvalidOperationException("File outcomes can only be added while the upload operation is in progress.");
        }

        ArgumentNullException.ThrowIfNull(outcome);
        if (outcome.PhotographyUploadOperationId != PhotographyUploadOperationId)
        {
            throw new InvalidOperationException("File outcome must belong to this upload operation.");
        }

        if (_fileOutcomes.Any(existing => existing.ClientFileOrdinal == outcome.ClientFileOrdinal))
        {
            throw new InvalidOperationException("File outcome ordinal must be unique within the upload operation.");
        }

        _fileOutcomes.Add(outcome);
        Touch();
    }

    public void FinalizeBatch(int expectedFileCount)
    {
        if (expectedFileCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedFileCount), "Expected file count must be greater than zero.");
        }

        if (IsTerminal(Status))
        {
            throw new InvalidOperationException("A finalized upload operation cannot be finalized again.");
        }

        if (_fileOutcomes.Count != expectedFileCount)
        {
            throw new InvalidOperationException("The upload operation cannot be finalized until every intended file has an outcome.");
        }

        EnsureCreateSetHasEstablishedSetForSuccessfulOutcomes();

        if (_fileOutcomes.Any(outcome => outcome.IsUnresolved))
        {
            Status = PhotographyUploadOperationStatus.RecoveryNeeded;
            CompletedAt = null;
            Touch();
            return;
        }

        Status = _fileOutcomes.All(outcome => outcome.Status == PhotographyUploadFileOutcomeStatus.Succeeded)
            ? PhotographyUploadOperationStatus.Completed
            : _fileOutcomes.Any(outcome => outcome.Status == PhotographyUploadFileOutcomeStatus.Succeeded)
                ? PhotographyUploadOperationStatus.CompletedWithFailures
                : PhotographyUploadOperationStatus.Failed;
        CompletedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void MarkSeen()
    {
        LastSeenAt = DateTimeOffset.UtcNow;
        Touch();
    }

    private void Touch()
    {
        ConcurrencyToken++;
        LastSeenAt = DateTimeOffset.UtcNow;
    }

    private static Guid? ResolveInitialPhotographySetId(PhotographyUploadOperationKind operationKind, Guid? photographySetId)
    {
        return operationKind switch
        {
            PhotographyUploadOperationKind.CreateSetUpload when photographySetId.HasValue && photographySetId.Value != Guid.Empty =>
                throw new ArgumentException("Create-set upload operations must start without an established photography set.", nameof(photographySetId)),
            PhotographyUploadOperationKind.CreateSetUpload => null,
            PhotographyUploadOperationKind.AppendToSetUpload when !photographySetId.HasValue || photographySetId.Value == Guid.Empty =>
                throw new ArgumentException("Append upload operations require an existing photography set.", nameof(photographySetId)),
            PhotographyUploadOperationKind.AppendToSetUpload => photographySetId.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(operationKind), "Unsupported photography upload operation kind.")
        };
    }

    private static bool IsTerminal(PhotographyUploadOperationStatus status) =>
        status is PhotographyUploadOperationStatus.Completed
            or PhotographyUploadOperationStatus.CompletedWithFailures
            or PhotographyUploadOperationStatus.Failed;

    private void EnsureCreateSetHasEstablishedSetForSuccessfulOutcomes()
    {
        if (OperationKind == PhotographyUploadOperationKind.CreateSetUpload
            && _fileOutcomes.Any(outcome => outcome.Status == PhotographyUploadFileOutcomeStatus.Succeeded)
            && !PhotographySetId.HasValue)
        {
            throw new InvalidOperationException("A create-set upload with successful file outcomes must reference the established photography set before finalization.");
        }
    }

    private static Guid RequireGuid(Guid value, string paramName) =>
        value == Guid.Empty ? throw new ArgumentException("A value is required.", paramName) : value;

    private static string RequireText(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", paramName);
        }

        return value.Trim();
    }
}

public sealed class PhotographyUploadFileOutcome
{
    private PhotographyUploadFileOutcome()
    {
    }

    private PhotographyUploadFileOutcome(
        Guid photographyUploadOperationId,
        int clientFileOrdinal,
        string originalFilename,
        string inputFingerprint,
        PhotographyUploadFileOutcomeStatus status,
        string staffFacingMessage,
        Guid? artifactImageId = null,
        ImageStorageObjectKey? originalObjectKey = null,
        IReadOnlyCollection<ImageStorageObjectKey>? derivativeObjectKeys = null)
    {
        PhotographyUploadFileOutcomeId = Guid.NewGuid();
        PhotographyUploadOperationId = RequireGuid(photographyUploadOperationId, nameof(photographyUploadOperationId));
        ClientFileOrdinal = clientFileOrdinal < 0 ? throw new ArgumentOutOfRangeException(nameof(clientFileOrdinal), "File ordinal cannot be negative.") : clientFileOrdinal;
        OriginalFilename = RequireText(originalFilename, nameof(originalFilename));
        InputFingerprint = RequireText(inputFingerprint, nameof(inputFingerprint));
        Status = PhotographyEnumValidation.RequireDefined(status, nameof(status));
        StaffFacingMessage = RequireText(staffFacingMessage, nameof(staffFacingMessage));
        ArtifactImageId = artifactImageId == Guid.Empty ? null : artifactImageId;
        OriginalObjectKey = originalObjectKey;
        DerivativeObjectKeys = [.. derivativeObjectKeys ?? []];
        CreatedAt = DateTimeOffset.UtcNow;
        FinalizedAt = IsFinal(status) ? CreatedAt : null;
    }

    public Guid PhotographyUploadFileOutcomeId { get; private set; }
    public Guid PhotographyUploadOperationId { get; private set; }
    public int ClientFileOrdinal { get; private set; }
    public string OriginalFilename { get; private set; } = string.Empty;
    public string InputFingerprint { get; private set; } = string.Empty;
    public PhotographyUploadFileOutcomeStatus Status { get; private set; }
    public Guid? ArtifactImageId { get; private set; }
    public ImageStorageObjectKey? OriginalObjectKey { get; private set; }
    public IReadOnlyCollection<ImageStorageObjectKey> DerivativeObjectKeys { get; private set; } = [];
    public string StaffFacingMessage { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? FinalizedAt { get; private set; }
    public bool IsUnresolved => !IsFinal(Status);

    public static PhotographyUploadFileOutcome Succeeded(
        Guid photographyUploadOperationId,
        int clientFileOrdinal,
        string originalFilename,
        string inputFingerprint,
        Guid artifactImageId,
        ImageStorageObjectKey originalObjectKey,
        IReadOnlyCollection<ImageStorageObjectKey> derivativeObjectKeys,
        string staffFacingMessage = "File uploaded.") =>
        new(photographyUploadOperationId, clientFileOrdinal, originalFilename, inputFingerprint, PhotographyUploadFileOutcomeStatus.Succeeded, staffFacingMessage, RequireGuid(artifactImageId, nameof(artifactImageId)), originalObjectKey ?? throw new ArgumentNullException(nameof(originalObjectKey)), derivativeObjectKeys ?? throw new ArgumentNullException(nameof(derivativeObjectKeys)));

    public static PhotographyUploadFileOutcome Rejected(Guid photographyUploadOperationId, int clientFileOrdinal, string originalFilename, string inputFingerprint, string staffFacingMessage) =>
        new(photographyUploadOperationId, clientFileOrdinal, originalFilename, inputFingerprint, PhotographyUploadFileOutcomeStatus.Rejected, staffFacingMessage);

    public static PhotographyUploadFileOutcome Failed(Guid photographyUploadOperationId, int clientFileOrdinal, string originalFilename, string inputFingerprint, string staffFacingMessage) =>
        new(photographyUploadOperationId, clientFileOrdinal, originalFilename, inputFingerprint, PhotographyUploadFileOutcomeStatus.Failed, staffFacingMessage);

    public static PhotographyUploadFileOutcome CleanupPending(
        Guid photographyUploadOperationId,
        int clientFileOrdinal,
        string originalFilename,
        string inputFingerprint,
        string staffFacingMessage,
        Guid? artifactImageId = null,
        ImageStorageObjectKey? originalObjectKey = null,
        IReadOnlyCollection<ImageStorageObjectKey>? derivativeObjectKeys = null) =>
        new(photographyUploadOperationId, clientFileOrdinal, originalFilename, inputFingerprint, PhotographyUploadFileOutcomeStatus.CleanupPending, staffFacingMessage, artifactImageId, originalObjectKey, derivativeObjectKeys);

    public static PhotographyUploadFileOutcome RecoveryNeeded(
        Guid photographyUploadOperationId,
        int clientFileOrdinal,
        string originalFilename,
        string inputFingerprint,
        string staffFacingMessage,
        ImageStorageObjectKey? originalObjectKey = null,
        IReadOnlyCollection<ImageStorageObjectKey>? derivativeObjectKeys = null) =>
        new(photographyUploadOperationId, clientFileOrdinal, originalFilename, inputFingerprint, PhotographyUploadFileOutcomeStatus.RecoveryNeeded, staffFacingMessage, null, originalObjectKey, derivativeObjectKeys);

    public void ResolveToSucceeded(Guid artifactImageId, ImageStorageObjectKey originalObjectKey, IReadOnlyCollection<ImageStorageObjectKey> derivativeObjectKeys, string staffFacingMessage = "File uploaded.")
    {
        EnsureUnresolved();
        ArgumentNullException.ThrowIfNull(derivativeObjectKeys);
        RequireStableIdentity(RequireGuid(artifactImageId, nameof(artifactImageId)), originalObjectKey, derivativeObjectKeys);

        ArtifactImageId = artifactImageId;
        OriginalObjectKey = originalObjectKey ?? throw new ArgumentNullException(nameof(originalObjectKey));
        DerivativeObjectKeys = [.. derivativeObjectKeys ?? []];
        FinalizeAs(PhotographyUploadFileOutcomeStatus.Succeeded, staffFacingMessage);
    }

    public void ResolveToRejected(string staffFacingMessage)
    {
        EnsureUnresolved();
        if (ArtifactImageId.HasValue || OriginalObjectKey is not null || DerivativeObjectKeys.Count > 0)
        {
            throw new InvalidOperationException("An outcome with stable image or object identity cannot be converted to a rejected file.");
        }

        FinalizeAs(PhotographyUploadFileOutcomeStatus.Rejected, staffFacingMessage);
    }

    public void ResolveToFailed(string staffFacingMessage)
    {
        EnsureUnresolved();
        FinalizeAs(PhotographyUploadFileOutcomeStatus.Failed, staffFacingMessage);
    }

    public void MarkRecoveryNeeded(string staffFacingMessage)
    {
        EnsureUnresolved();
        Status = PhotographyUploadFileOutcomeStatus.RecoveryNeeded;
        StaffFacingMessage = RequireText(staffFacingMessage, nameof(staffFacingMessage));
        FinalizedAt = null;
    }

    private static bool IsFinal(PhotographyUploadFileOutcomeStatus status) =>
        status is PhotographyUploadFileOutcomeStatus.Succeeded or PhotographyUploadFileOutcomeStatus.Rejected or PhotographyUploadFileOutcomeStatus.Failed;

    private void EnsureUnresolved()
    {
        if (!IsUnresolved)
        {
            throw new InvalidOperationException("Only unresolved upload file outcomes can transition.");
        }
    }

    private void FinalizeAs(PhotographyUploadFileOutcomeStatus status, string staffFacingMessage)
    {
        Status = status;
        StaffFacingMessage = RequireText(staffFacingMessage, nameof(staffFacingMessage));
        FinalizedAt = DateTimeOffset.UtcNow;
    }

    private void RequireStableIdentity(Guid artifactImageId, ImageStorageObjectKey originalObjectKey, IReadOnlyCollection<ImageStorageObjectKey>? derivativeObjectKeys)
    {
        ArgumentNullException.ThrowIfNull(originalObjectKey);

        if (ArtifactImageId.HasValue && ArtifactImageId.Value != artifactImageId)
        {
            throw new InvalidOperationException("The file outcome artifact image identity cannot be replaced.");
        }

        if (OriginalObjectKey is not null && OriginalObjectKey != originalObjectKey)
        {
            throw new InvalidOperationException("The file outcome original object identity cannot be replaced.");
        }

        var resolvedDerivativeKeys = derivativeObjectKeys?.ToArray() ?? [];
        if (DerivativeObjectKeys.Count > 0 && !DerivativeObjectKeys.SequenceEqual(resolvedDerivativeKeys))
        {
            throw new InvalidOperationException("The file outcome derivative object identities cannot be replaced.");
        }
    }

    private static Guid RequireGuid(Guid value, string paramName) =>
        value == Guid.Empty ? throw new ArgumentException("A value is required.", paramName) : value;

    private static string RequireText(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", paramName);
        }

        return value.Trim();
    }
}
