# Feature 003 Implementation Decisions

## T001 - JPEG/PNG Processing Package

**Decision**: Use `SkiaSharp` for Feature 003 image validation and derivative generation in Infrastructure only.

**Package/version considered**:

- `SkiaSharp` `4.151.1`, the current stable NuGet release reviewed on 2026-08-24.
- `SkiaSharp.NativeAssets.Linux` `4.151.1` for Linux development/test runtime support.
- `SixLabors.ImageSharp` `4.0.0` as the originally identified candidate.
- `System.Drawing.Common`, rejected by planning research because modern .NET treats it as Windows-specific.

**Compatibility**:

- `SkiaSharp` `4.151.1` lists `net10.0` compatibility on NuGet.
- The project targets `net10.0`.
- Windows native assets are transitive from `SkiaSharp`; Linux native assets are added explicitly so WSL/Linux validation can run without making Linux a production requirement.

**JPEG support**:

- SkiaSharp decodes compressed bitmap streams and supports JPEG encoding through `SKEncodedImageFormat.Jpeg`.

**PNG support**:

- SkiaSharp decodes PNG images and supports PNG encoding through `SKEncodedImageFormat.Png`.

**Resizing/thumbnail capability**:

- SkiaSharp provides bitmap/image decode, draw, scale, and encode APIs suitable for bounded thumbnail and preview derivative generation.

**License**:

- `SkiaSharp` and `SkiaSharp.NativeAssets.Linux` are MIT licensed.
- MIT licensing is compatible with the Museum-System's intended institutional deployment because it permits use, copying, modification, distribution, sublicensing, and sale when copyright and permission notices are retained.

**Reason selected over alternatives**:

- Selected over `SixLabors.ImageSharp` because current ImageSharp releases use the Six Labors Split License, where direct closed-source enterprise use can require a commercial license unless the consuming organization qualifies under the license criteria. The museum deployment status cannot be assumed to satisfy those criteria.
- Selected over `System.Drawing.Common` because Feature 003 requires portable behavior and the planning research already rejected Windows-specific imaging.
- Selected over heavier ImageMagick/Magick.NET-style options because Feature 003 only needs JPEG/PNG validation plus thumbnail/preview derivatives, and SkiaSharp provides those capabilities under a simple MIT license.

**Boundary decision**:

- Package-specific types must remain in Infrastructure. Domain and Application models remain package-neutral and do not expose SkiaSharp types.

**Sources reviewed**:

- NuGet `SkiaSharp` 4.151.1 package page: https://www.nuget.org/packages/SkiaSharp/4.151.1
- NuGet `SkiaSharp.NativeAssets.Linux` 4.151.1 package page: https://www.nuget.org/packages/SkiaSharp.NativeAssets.Linux/4.151.1
- SkiaSharp MIT license: https://github.com/mono/SkiaSharp/blob/main/LICENSE.md
- SkiaSharp README/platform support: https://github.com/mono/SkiaSharp/blob/main/README.md
- SkiaSharp bitmap decode/encode documentation: https://mono.github.io/SkiaSharp/docs/guides/bitmaps/saving.html
- Six Labors ImageSharp license: https://github.com/SixLabors/ImageSharp/blob/main/LICENSE
## Checkpoint U0 - Durable Deletion Attribution

**Decision**: Store deletion-intent attribution separately as `DeletionRequestedByUserId` and `DeletionRequestedAt` on `ArtifactImage` when a permanent deletion is accepted.

**Reason**: `DeletedByUserId` and `DeletedAt` keep their finalization semantics and remain null while an image is `DeletePending`. Automatic recovery after restart can then finalize metadata using the original accepted deletion actor/time without substituting a recovery worker identity or fabricating historical attribution. Legacy `DeletePending` rows without these intent fields are treated as incomplete/manual-attention states.

## Checkpoint V0 - Durable Upload-Recovery Correlation and Idempotency Retention

**Decision**: `StorageOperationRecovery` carries two nullable historical correlation identifiers for `UploadCleanup` rows - `PhotographyUploadOperationId` and `PhotographyUploadFileOutcomeId` - populated at creation time by `PhotographyUploadPersistenceService.PersistRecoveryNeededOutcomeAsync`. They are plain nullable UUID columns with supporting indexes, intentionally **not** relational foreign keys.

**Reason for no FK**: `StorageOperationRecovery` rows are retained as operational/audit history even after their correlated `PhotographyUploadOperation` idempotency record is purged by retention. A real FK would force either deleting recovery history alongside the purged operation, or nulling out historical correlation on purge - neither is acceptable. Correlation IDs on retained recovery rows remain unchanged after the operation/outcome they pointed to is deleted; legacy rows created before this correlation existed keep null values, and those values are never inferred from `ArtifactId`, object keys, or timestamps.

**Reconciliation**: When `StorageOperationRecoveryUseCase` finishes a correlated `UploadCleanup` retry (all orphan objects cleaned), it loads the linked operation/outcome by the stored IDs. A linked outcome still in `RecoveryNeeded` or `CleanupPending` moves to final `Failed` with the fixed staff-safe cleanup-completed message; a linked outcome already `Failed` is accepted as a restart-idempotent, already-reconciled state. A linked outcome already `Succeeded` or `Rejected` is not mutated and is treated as `FailedNeedsAttention`/`InvalidState`. A linked operation still `RecoveryNeeded` is re-finalized using its complete persisted file outcomes; a linked operation already `Completed`, `CompletedWithFailures`, or `Failed` is treated as already reconciled and is not finalized again. A linked operation still `InProgress`, one-sided/mismatched correlation, or missing linked row is not guessed - the recovery is marked `FailedNeedsAttention`/`InvalidState`. Upload outcome/operation reconciliation persists in its own local PostgreSQL transaction after object-storage cleanup succeeds, and the recovery is marked `Resolved` only afterward; the retry path is idempotent across a crash between those two transactions. Uncorrelated (legacy) `UploadCleanup` rows still resolve the storage inconsistency itself, exactly as before; no upload operation/outcome is guessed for them.

**Retention**: `PhotographyUploadIdempotencyRetentionService` (internal application service; no permission, no endpoint, no hosted service/scheduler) performs one cleanup pass via `CleanupExpiredAsync`. An operation is eligible for deletion only when: its `LastSeenAt` (the idempotency-replay activity clock, not `StartedAt`) is at or before `TimeProvider.GetUtcNow() - RetentionDays`; its status is exactly `Completed`, `CompletedWithFailures`, or `Failed`; every file outcome is final; and no `StorageOperationRecovery` correlated by `PhotographyUploadOperationId` is unresolved (`Pending`/`Retrying`/`FailedNeedsAttention`). A `Resolved` linked recovery does not block. An unresolved recovery that merely shares the same `ArtifactId` (not correlated to the operation) never blocks - correlation is always operation-specific. `RetentionDays` binds the existing `Photography:Idempotency:RetentionDays` configuration key (previously unused) through `PhotographyIdempotencyOptions`. Because `PhotographyUploadFileOutcome -> PhotographyUploadOperation` uses `DeleteBehavior.Restrict`, cleanup explicitly deletes an operation's file outcomes before the operation itself, and never touches `PhotographySet`, `ArtifactImage`, `ArtifactImageDerivative`, `Artifact`, `StorageOperationRecovery`, or `AuditEntry` rows. Deletion uses the operation's existing `ConcurrencyToken` so a candidate changed after selection (e.g. a concurrent idempotency replay bumping `LastSeenAt`) is safely skipped rather than overwritten. No background scheduler is implemented; `CleanupExpiredAsync` is invoked manually/on demand only.