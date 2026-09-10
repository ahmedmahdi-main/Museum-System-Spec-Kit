# Photography Storage Provider Operations

This note is for museum system administrators, deployment operators, and future maintainers. It describes the Feature 003 Photography storage boundary as implemented today and the operational constraints for future storage migration and recovery planning.

It is not a MinIO tutorial, a backup design, or a high-availability design.

## Authority And Identity

PostgreSQL owns the structured Photography business metadata: Photography Sets, Photography Requests, Artifact Images, Primary Image state, upload idempotency state, deletion state, storage recovery state, and audit references.

Private object storage owns the binary content:

- original image binaries;
- thumbnail derivatives;
- preview derivatives.

The system does not assume a distributed transaction between PostgreSQL and object storage. Consistency is handled by storage verification, compensating cleanup, durable `StorageOperationRecovery`, and idempotent reconciliation.

Artifact identity remains centered on the existing `ArtifactId`. Photography migration must not create a second Artifact identity and must not change Museum Number, custody, movement history, current location, Documentation ownership, or Laboratory workflow meaning.

## Provider-Neutral Boundary

Domain and Application code depend on `IArtifactImageStorage` and provider-neutral result contracts. Concrete MinIO SDK usage and provider error translation live in Infrastructure.

The current implemented provider is MinIO. Provider-neutral does not mean that any arbitrary provider works without code. A future provider requires a new Infrastructure implementation that honors the same application storage contract.

Changing providers must not require changes to Domain business rules, Artifact identity, Photography Set meaning, Artifact Image business meaning, permissions, or staff workflow semantics. S3, Azure, GCP, and other providers are not currently implemented by Feature 003.

## Current Configuration Contract

Photography storage configuration is read from `Photography:Storage`.

Current keys:

- `Provider`
- `Endpoint`
- `BucketName`
- `Region`
- `UseTls`
- `AccessKey`
- `SecretKey`
- `RequestTimeoutSeconds`

When `Provider` is `Minio`, validation requires `Endpoint`, `BucketName`, `AccessKey`, and `SecretKey`, and `RequestTimeoutSeconds` must be greater than zero. `Region` is optional. `UseTls` defaults to true in the options class unless configuration overrides it.

Production secrets must be supplied through environment configuration, a deployment secret mechanism, or another approved protected configuration source. Do not commit `AccessKey` or `SecretKey` into source-controlled production configuration.

`src/MuseumSystem.Web/appsettings.json` contains non-secret structural defaults. The production `Endpoint` is intentionally empty and credentials are not committed. `src/MuseumSystem.Web/appsettings.Development.json` contains a localhost development endpoint and development bucket only; it is not a production deployment prescription.

Standard .NET hierarchical environment variable names may be used where approved:

```text
Photography__Storage__Provider
Photography__Storage__Endpoint
Photography__Storage__BucketName
Photography__Storage__Region
Photography__Storage__UseTls
Photography__Storage__AccessKey
Photography__Storage__SecretKey
Photography__Storage__RequestTimeoutSeconds
```

These are configuration examples only. Environment variables are not the only allowed secret mechanism.

## Private Access Rule

The bucket or container must remain private. Staff access goes through opaque application-authorized image endpoints and application-mediated streaming.

Do not expose these values as business identifiers:

- bucket name;
- object key;
- provider endpoint;
- credentials;
- permanent public URL.

Feature 003 prohibits public permanent object URLs. If short-lived provider access is supported internally in the future, it must preserve the opaque application security boundary.

## Object Identity

`ImageStorageObjectKey` is a logical object-storage identity. It is not a Windows path, Linux path, Museum Number, or Artifact name.

Object keys are generated independently of mutable Artifact descriptions. Migration should preserve the authoritative object-key identity whenever possible.

If a future provider requires key transformation, PostgreSQL references and object binaries must be transformed in one explicitly planned migration. Do not copy objects under new keys while leaving old PostgreSQL references behind. Feature 003 does not provide automatic key-rewrite support.

## Windows Server Status

MinIO on Windows Server 2019 is a provisional deployment candidate. Feature 003 does not declare the current direct-Windows MinIO topology production approved.

The [Windows Server 2019 MinIO Production Go/No-Go PoC](photography-minio-windows-2019-poc.md) is the T128 acceptance instrument only. Its existence does not constitute production approval, does not mean the PoC passed, and does not change the PoC status from Pending / Not Executed. Until the actual PoC is executed, evidence is recorded, and an explicit future Go decision is made, Windows Server 2019 MinIO is not production-approved by Feature 003.

Production reliance requires the documented go/no-go proof of concept, including checks such as:

- service start and restart;
- reboot behavior;
- private access;
- protected credentials;
- TLS and network connectivity;
- upload, read, and delete;
- application restart;
- provider restart;
- `StorageOperationRecovery` behavior;
- representative image workload;
- migration and export feasibility.

Feature 003 does not require Docker, Docker Desktop, WSL, Testcontainers, or a Linux VM for production operation. Automated tests may use containers; test infrastructure is not a production deployment requirement. Docker is not forbidden, but T129 does not prescribe a new production topology.

## Operational Acceptance Boundaries

Windows filesystem paths used to host MinIO object data are deployment configuration only. They must not become Domain data, Application business data, Artifact identity, Museum Number, image business identity, `ImageStorageObjectKey`, or staff-facing identifiers. `ImageStorageObjectKey` remains a logical object-storage key, not a Windows filesystem path, and the storage contract remains provider-neutral.

If a deployment uses one local `D:\` storage location or another single local volume as the MinIO data location, that topology provides only local persistent storage. It is not high availability, replication, failover, or disaster recovery. Increasing disk capacity does not turn a single storage node, single local volume, or single server into HA.

MinIO/private object storage is the authoritative binary store for Photography originals, thumbnails, and previews, but the existence of those objects is not itself a backup. PostgreSQL is not a backup of MinIO, MinIO is not a backup of PostgreSQL, `StorageOperationRecovery` is not backup, extra free disk capacity is not backup, and a copied object directory without coordinated metadata state is not a complete Photography recovery point.

T128 defines the direct-Windows MinIO go/no-go PoC instrument. T129 documents deployment and operational acceptance boundaries. Neither task executes the PoC, approves production, implements backup, or implements HA. A future successful PoC still would not make a single storage volume highly available and would not constitute a backup strategy.

Operational storage topology must not alter the existing `ArtifactId`, Museum Number, custody, movement history, current physical location, Documentation ownership, or Laboratory workflow meaning.

## Future Linux Or Provider Migration

Use this as a runbook checklist concept, not as executable migration tooling.

1. Preflight
   - Confirm the target object provider implementation is supported by Infrastructure.
   - Verify capacity, network, TLS, private access, credentials, and permissions.
   - Verify the target supports required store, stat, read, and delete semantics.
   - Review unresolved recoveries before migration.

2. Control writes
   - Establish an approved maintenance window or write quiescence for Photography.
   - Do not migrate while staff continue creating or deleting image objects unless a separately designed online migration protocol exists.
   - Feature 003 does not implement online dual-write migration.

3. Establish a coordinated source point
   - Identify the PostgreSQL state and corresponding object-store state that belong together.
   - Preserve both as a coordinated operational recovery point.
   - Do not assume a specific backup product or atomic snapshot capability from Feature 003.

4. Transfer object data
   - Copy originals and derivatives.
   - Preserve object keys and object content.
   - Do not manually reinterpret object keys as filesystem paths.

5. Verification
   - Verify expected objects can be stat/read on the target.
   - Verify representative original, thumbnail, and preview objects.
   - Compare an approved inventory, checksum, or count strategy.
   - Verify no metadata points to an unconfirmed target object.
   - Feature 003 does not include a repository-wide checksum migration tool.

6. Switch configuration
   - Update Infrastructure/deployment configuration only.
   - Keep Domain, Application, and business identity unchanged.
   - Restart or redeploy the application using the approved operational process.

7. Acceptance
   - Verify upload.
   - Verify gallery/read.
   - Verify primary-image viewing.
   - Verify delete.
   - Verify recovery behavior.
   - Verify staff-safe unavailable behavior.
   - Verify representative pre-migration images.

8. Resume writes
   - Resume Photography writes only after application and object-store verification succeeds.

9. Rollback decision
   - If target validation fails, stop and return to the explicitly preserved source configuration and state according to the approved migration plan.
   - Feature 003 does not implement automatic rollback.

## Coordinated Recovery Requirement

PostgreSQL backup alone is not a complete Photography recovery.

Object-storage backup alone is not a complete Photography recovery.

A future backup and restore design must coordinate both parts of the Photography logical state:

1. PostgreSQL metadata/state: Photography Sets, Photography Requests where relevant, Artifact Images, derivatives metadata, Primary Image state, deletion state, idempotency state, `StorageOperationRecovery` state, and audit references.
2. Object binaries: originals, thumbnails, and previews.

A future restore process must establish a coordinated point or otherwise prove that restored metadata and binaries are mutually consistent. Do not claim that Feature 003 provides atomic snapshots or prescribes a backup vendor, product, or restore engine.

Restoring independent PostgreSQL and object-storage points in time can produce:

- PostgreSQL metadata pointing to an object that does not exist;
- an object with no corresponding committed metadata;
- deleted binaries reappearing while PostgreSQL still says `Deleted`;
- PostgreSQL restored to a pre-delete state while the binary remains deleted;
- Primary Image metadata referencing unavailable content;
- stale `DeletePending` or recovery state disagreeing with actual storage;
- unresolved `StorageOperationRecovery` state no longer matching real object state.

A future restore or migration procedure must control Photography writes sufficiently to avoid creating a new inconsistent point during restore. It may use an approved maintenance window, write quiescence, or another future designed mechanism. Feature 003 does not implement online dual-write restore or migration.

Feature 003 deliberately does not implement the backup or restore mechanism. `StorageOperationRecovery` does not replace backup.

## Storage Operation Recovery

`StorageOperationRecovery` is an internal consistency mechanism. It is not a backup system, high availability, replication, disaster recovery, or a staff-visible recovery workflow.

Current recovery states include:

- `Pending`
- `Retrying`
- `Resolved`
- `FailedNeedsAttention`

Unresolved recovery must be reviewed before and during migration. Do not delete unresolved recovery rows, guess missing correlation, change object keys behind them, or mark them resolved merely because migration occurred.

For upload cleanup, `PhotographyUploadOperationId` and `PhotographyUploadFileOutcomeId` are historical correlation IDs. They are intentionally not foreign keys. Retained recovery history may outlive idempotency operation rows. Legacy null correlation must never be inferred from `ArtifactId`, object key, or time.

## Idempotency Retention

Photography upload idempotency retention uses `LastSeenAt` and a configured retention cutoff. It deletes only terminal upload operations and outcomes.

Unresolved correlated `StorageOperationRecovery` blocks purge. Resolved recovery does not block. Recovery history itself is retained.

There is currently no background scheduler, hosted service, CLI, or endpoint for this cleanup. The implemented service performs one cleanup pass when an approved caller invokes it.

## Deletion Consistency

Deletion intent becomes durable before destructive object deletion. An image can be `DeletePending` while cleanup or finalization is incomplete.

Successful permanent deletion removes the original and exclusive derivatives. If storage deletion fails, the system retains recoverable and auditable state and must not claim deletion fully succeeded.

If storage deletion succeeds but database/audit finalization fails, the system does not recreate the deleted binary. It finalizes metadata and audit idempotently through recovery semantics. Migration must not convert `DeletePending` into `Available`.

## Upload Consistency

Objects are stored and verified before successful available metadata finalization.

If metadata persistence fails after object write, compensating cleanup is attempted. If cleanup fails, durable recovery is created.

A migration operator must not treat every object lacking `Available` metadata as safe to delete without checking recovery and idempotency state. Do not prescribe manual deletion based only on bucket inventory.

## Staff-Facing Failure Boundary

Staff UI intentionally receives controlled unavailable or retry messages. Staff must not receive object keys, bucket names, provider endpoints, credentials, raw provider exceptions, `FailureSummary`, or `OperationalSummary`.

Detailed recovery state remains internal, auditable, and operational. No sixth Photography recovery permission exists. Any future manual administrator recovery UI requires a separate authorization decision.

## Monitoring And Operator Checklist

Feature 003 does not implement monitoring dashboards, alert rules, SQL reports, endpoints, scheduled jobs, or operational automation. Operators should plan to observe these through approved operational or administration mechanisms when they exist:

- application and storage availability;
- storage capacity and low-space conditions;
- provider service availability;
- count or list of unresolved `StorageOperationRecovery` rows;
- `FailedNeedsAttention` recovery state;
- repeated provider configuration failures;
- audit trail for recovery operations.

These are operational requirements, not implemented monitoring features.

## Out Of Scope

T121 and Feature 003 do not implement:

- backup engine;
- backup scheduler;
- restore engine;
- coordinated backup orchestration;
- coordinated restore orchestration;
- PostgreSQL PITR;
- object-storage backup scheduler;
- object-storage replication;
- MinIO replication;
- HA cluster;
- multi-node MinIO HA;
- failover;
- automatic failover;
- DR orchestration;
- disaster-recovery orchestration;
- automatic migration;
- dual-write migration;
- online dual-write restore or migration;
- scheduler for recovery;
- scheduler for idempotency retention;
- operator recovery Web UI;
- sixth Photography permission;
- filesystem fallback;
- automatic provider failover.

Do not promise these capabilities as part of Feature 003.

## Security

`AccessKey` and `SecretKey` are secrets. Never include them in logs, staff messages, audit summaries, documentation examples with real values, or source control.

Use least-privilege object-storage credentials. Keep the bucket private. TLS should follow the approved deployment and network policy; Feature 003 does not prescribe a TLS termination topology.

## Validation After Configuration Or Migration

After provider configuration or migration, verify:

- the application starts with valid provider configuration;
- a valid JPEG or PNG can be uploaded;
- original and derivative objects verify;
- authorized gallery/read succeeds;
- missing or unavailable object storage produces controlled staff behavior;
- deletion removes original and exclusive derivatives;
- recovery retry works after temporary provider interruption;
- the same storage instance works after provider restart;
- no raw storage internals leak to staff;
- Artifact identity, custody, movement, and location remain unchanged.

Do not require production operators to run test fixtures or Testcontainers.

## Source Of Truth References

- [Feature 003 specification](../../specs/003-artifact-photography-image-stewardship/spec.md)
- [Feature 003 plan](../../specs/003-artifact-photography-image-stewardship/plan.md)
- [Feature 003 research](../../specs/003-artifact-photography-image-stewardship/research.md)
- [Storage abstraction contract](../../specs/003-artifact-photography-image-stewardship/contracts/storage-abstraction.md)
- [Implementation decisions](../../specs/003-artifact-photography-image-stewardship/implementation-decisions.md)
- [Windows Server 2019 MinIO PoC checklist](photography-minio-windows-2019-poc.md)
- [MinIO storage options](../../src/MuseumSystem.Infrastructure/Photography/Storage/MinioArtifactImageStorageOptions.cs)
- [Storage recovery use case](../../src/MuseumSystem.Application/Modules/Photography/StorageOperationRecoveryUseCase.cs)
