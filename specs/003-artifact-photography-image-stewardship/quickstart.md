# Quickstart: Feature 003 Planning Notes

This quickstart is for future implementation and verification. It does not create production code or tasks.

## Local Development Expectations

1. Run the existing application and PostgreSQL development setup according to repository conventions.
2. Configure an object-storage provider through application settings:
   - endpoint;
   - bucket;
   - access key;
   - secret key;
   - TLS setting;
   - maximum upload size;
   - thumbnail/preview sizes.
3. Use MinIO for development/integration tests where available.
4. Do not require Docker for production deployment assumptions.

## Windows Server 2019 Provisional Production Candidate

MinIO on Windows Server 2019 is not yet an approved final production topology. It is a provisional candidate that requires a production go/no-go PoC on the actual museum server before production reliance.

Candidate shape:

1. Install MinIO executable directly on Windows Server 2019 without Docker, WSL, or Linux VM.
2. Use a dedicated private data directory on `D:\`, separate from application files.
3. Run MinIO as a service through WinSW or an equivalent approved Windows service mechanism.
4. Configure restart-on-failure and logs for operational review.
5. Store credentials outside source control and inject them through protected configuration.
6. Keep the bucket private; users access images only through the application.

Go/no-go PoC must verify:

- installation without Docker/WSL/Linux VM;
- Windows service startup and reboot auto-start;
- restart-on-failure;
- persistence on `D:\`;
- dedicated storage directory ownership/access;
- TLS/network access;
- protected credentials;
- private bucket;
- upload/read/delete;
- multiple-image workload;
- representative large JPEG/PNG files;
- application restart;
- MinIO restart during/around operations;
- cleanup and `StorageOperationRecovery` behavior;
- disk-full/low-space handling where safely testable;
- migration/export feasibility to future Linux.

If this PoC fails, do not silently fall back to filesystem storage. Production deployment requires a separately approved object-storage provider/deployment decision or the future Linux environment.

Operational acceptance must acknowledge that single `D:\` storage is a single point of failure and MinIO is not backup. Backup implementation remains out of scope for Feature 003, but PostgreSQL metadata and object binaries require coordinated backup/recovery.

## Future Linux Migration Check

The implementation should pass these portability checks:

- No Domain/Application behavior depends on Windows paths.
- Object keys are stable and provider-neutral.
- MinIO endpoint and credentials are configuration only.
- Image processing uses a cross-platform library.
- PostgreSQL metadata remains the source of image relationships and audit identity.
- Provider abstraction remains intact if moving away from the provisional Windows candidate.

## Verification Checklist for Implementation Phase

- Existing Artifact identity is referenced, not duplicated.
- Photography workflows do not create custody or movement.
- Multi-image upload returns per-file results.
- Persistent idempotency returns the same per-file results after application restart.
- Same idempotency key with different input is rejected.
- Only JPEG/JPG and PNG content is accepted.
- At most one Primary Image exists per Artifact.
- SetPrimary and DeletePrimary races resolve through the Artifact-level Photography state.
- Request completion requires `Photography.Upload` and valid matching fulfillment.
- Multiple matching requests may reference the same fulfilling set only through explicit independent completion.
- Appending to an existing set rejects wrong Artifact/Purpose input.
- Grace deletion requires current `Photography.Upload`, same uploader, and no more than 60 minutes since server-generated UploadedAt.
- Privileged deletion requires `Photography.Delete` and a non-empty reason.
- Deleted originals and exclusive derivatives are removed from storage.
- Storage inconsistencies produce recoverable/auditable records.
- Object deletion followed by metadata/audit finalization failure retries finalization and does not restore deleted binaries.
- Staff image viewing uses opaque application endpoints/application streaming and never exposes raw MinIO internals.
- Concrete image-processing package selection passes dependency/license compatibility review.
- No `tasks.md` should exist until the tasks stage.
