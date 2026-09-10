# Windows Server 2019 MinIO Production Go/No-Go PoC

## Status

- Status: PENDING - NOT EXECUTED
- Production decision: NOT APPROVED
- PoC result: NOT YET DETERMINED
- Date executed: __________________
- Executed by: __________________
- Reviewed by: __________________
- Server/environment identifier: __________________

This checklist being present does not constitute production approval. T128 creates the acceptance instrument only; it does not execute the PoC and does not approve MinIO on Windows Server 2019 for production.

## 1. Scope And Non-Goals

This PoC validates only the proposed direct-Windows MinIO deployment candidate for Feature 003 object storage on the actual Windows Server 2019 environment chosen by museum/IT staff.

It does not prove or implement:

- backup;
- restore;
- high availability;
- replication;
- failover;
- disaster recovery;
- PostgreSQL point-in-time recovery;
- automatic migration;
- dual-write migration;
- filesystem fallback;
- automatic provider failover.

A successful future PoC would mean only that the recorded direct-Windows MinIO candidate is operationally acceptable for the tested Feature 003 object-storage role. It would not approve backup, HA, DR, or migration tooling.

## 2. Architecture Boundary To Preserve

Fixed constraints:

- PostgreSQL is authoritative for Photography structured metadata and business state: Photography Sets, Photography Requests, Artifact Images, Primary Image state, upload idempotency state, deletion state, storage recovery state, and audit references.
- MinIO/private object storage owns original image binaries and thumbnail/preview derivative binaries.
- Artifact identity remains the existing `ArtifactId`.
- Photography activity must not change Museum Number, custody, movement history, current location, Documentation ownership, or Laboratory workflow meaning.
- No Windows filesystem path may become a Domain/Application business identifier.
- No bucket name, object key, provider endpoint, AccessKey, SecretKey, raw provider exception, or permanent public object URL may become staff-facing business data.
- Domain and Application behavior must remain provider-neutral through application storage contracts.
- There is no distributed transaction between PostgreSQL and object storage; consistency depends on verification, compensating cleanup, durable `StorageOperationRecovery`, and idempotent reconciliation.

## 3. Environment Record

Record actual approved deployment choices before execution. Do not record AccessKey or SecretKey values.

| Field | Value / Evidence |
| --- | --- |
| Windows edition | __________________ |
| Windows Server version/build | __________________ |
| VM or physical | __________________ |
| CPU allocation | __________________ |
| RAM allocation | __________________ |
| Storage capacity | __________________ |
| Filesystem | __________________ |
| Storage/data path | __________________ |
| Free capacity before test | __________________ |
| MinIO version/build | __________________ |
| Museum-System build/commit | __________________ |
| .NET runtime | __________________ |
| MinIO service identity | __________________ |
| Service hosting mechanism | __________________ |
| Endpoint | __________________ |
| TLS yes/no | __________________ |
| Certificate source/subject if applicable | __________________ |
| Bucket name - operational evidence only; do not expose to staff | __________________ |
| Network segment | __________________ |
| Firewall/network approval reference | __________________ |
| Credential source/mechanism | __________________ |
| Least-privilege review | Pending |
| PoC date | __________________ |
| Operator | __________________ |
| Reviewer | __________________ |

## 4. Entry Criteria

All entry criteria are pending until the actual PoC execution records evidence.

- [ ] Windows Server 2019 target identified.
- [ ] Approved MinIO binary/version recorded.
- [ ] Installation/binary integrity/source recorded.
- [ ] Dedicated storage location identified.
- [ ] Required capacity available.
- [ ] Service identity selected.
- [ ] Credentials stored through an approved protected mechanism.
- [ ] Private bucket created/configured.
- [ ] Network path from Museum-System to MinIO established.
- [ ] TLS approach recorded and approved where required.
- [ ] Production-like Museum-System configuration prepared without secrets in source control.
- [ ] Rollback/removal procedure for the PoC environment understood.
- [ ] Test evidence location prepared.

Docker Desktop, WSL, and a Linux VM are not production entry criteria for this direct-Windows candidate.

## 5. Service Lifecycle Tests

| Test ID | Criticality | Procedure | Expected result | Evidence | Actual result | Status |
| --- | --- | --- | --- | --- | --- | --- |
| WS-P01 | Critical | Start MinIO using the approved service hosting mechanism. | Service starts successfully; data path is accessible; no immediate fatal errors. | Startup log/reference: __________________ | __________________ | Pending |
| WS-P02 | Critical | Stop and start MinIO through the approved operational mechanism after storing a representative object. | Service stops cleanly, starts again, and existing objects remain available. | Stop/start log and object verification: __________________ | __________________ | Pending |
| WS-P03 | Critical | Restart Windows Server 2019 and observe the approved MinIO hosting process. | MinIO becomes available through the expected operational process and existing objects remain available. | Reboot record and object verification: __________________ | __________________ | Pending |
| WS-P04 | Major | Perform multiple controlled MinIO restarts around representative stored objects. | Repeated restarts do not corrupt, unlink, or make stored objects unavailable. | Restart sequence record: __________________ | __________________ | Pending |

The PoC validates the chosen service hosting mechanism. This checklist does not prescribe a Windows service wrapper, scheduled task, or installer.

## 6. Security And Private Access Tests

| Test ID | Criticality | Procedure | Expected result | Evidence | Actual result | Status |
| --- | --- | --- | --- | --- | --- | --- |
| WS-P05 | Critical | Verify bucket/container access policy. | Bucket remains private. | Policy evidence: __________________ | __________________ | Pending |
| WS-P06 | Critical | Attempt anonymous/public read. | Anonymous/public read is denied. | Denial evidence: __________________ | __________________ | Pending |
| WS-P07 | Critical | Attempt anonymous/public write. | Anonymous/public write is denied. | Denial evidence: __________________ | __________________ | Pending |
| WS-P08 | Critical | Review repository and deployed configuration evidence. | Credentials are not stored in source control. | Configuration review reference: __________________ | __________________ | Pending |
| WS-P09 | Critical | Review application and storage logs after representative operations. | Credentials are not emitted in application or storage logs. | Log review reference: __________________ | __________________ | Pending |
| WS-P10 | Critical | Review configured service account permissions. | Least-privilege access is accepted by museum/IT staff. | Permission review reference: __________________ | __________________ | Pending |
| WS-P11 | Critical | Exercise Museum-System storage operations with approved credentials. | Required upload, stat, read, and delete operations are authorized. | Operation evidence: __________________ | __________________ | Pending |
| WS-P12 | Critical | Attempt an unauthorized storage operation with non-approved access. | Unauthorized access is denied. | Denial evidence: __________________ | __________________ | Pending |
| WS-P13 | Critical where TLS is required | Connect using the approved TLS approach when enabled/required. | TLS connectivity works and certificate checks satisfy policy. | TLS evidence: __________________ | __________________ | Pending |
| WS-P14 | Critical | Inspect staff-facing UI/API responses during storage workflows. | Staff-facing surfaces expose no endpoint, bucket name, object key, AccessKey, SecretKey, raw provider exception, or permanent public URL. | UI/API evidence: __________________ | __________________ | Pending |

Do not place real secret values in evidence.

## 7. Configuration Contract

Museum-System reads Photography object-storage configuration from `Photography:Storage`.

Current expected keys:

- `Provider`
- `Endpoint`
- `BucketName`
- `Region`
- `UseTls`
- `AccessKey`
- `SecretKey`
- `RequestTimeoutSeconds`

When `Provider` is `Minio`, the current options validator requires `Endpoint`, `BucketName`, `AccessKey`, and `SecretKey`; `RequestTimeoutSeconds` must be greater than zero; `Region` may be optional; `UseTls` defaults to true unless deployment configuration overrides it.

Environment variable names may be used by an approved deployment mechanism. Names only:

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

| Test ID | Criticality | Procedure | Expected result | Evidence | Actual result | Status |
| --- | --- | --- | --- | --- | --- | --- |
| WS-P15 | Critical | Review deployed configuration source. | `Provider` is `Minio`; required values are supplied through approved deployment configuration. | Config evidence without secrets: __________________ | __________________ | Pending |
| WS-P16 | Critical | Review timeout and optional region configuration. | `RequestTimeoutSeconds` is greater than zero; `Region` is recorded if used. | Config evidence: __________________ | __________________ | Pending |
| WS-P17 | Critical | Review source control and deployment records. | Real AccessKey/SecretKey values are not source-controlled and are supplied only through protected configuration. | Secret-handling review: __________________ | __________________ | Pending |

## 8. Core Storage Contract Tests

Validate through Museum-System where possible. Manual MinIO console checks may supplement evidence but must not replace application-level contract proof.

| Test ID | Criticality | Procedure | Expected result | Evidence | Actual result | Status |
| --- | --- | --- | --- | --- | --- | --- |
| WS-P18 | Critical | Upload a valid JPEG through Museum-System. | Original object is stored; metadata finalizes only after storage verification. | Upload evidence: __________________ | __________________ | Pending |
| WS-P19 | Critical | Upload a valid PNG through Museum-System. | Original object is stored; metadata finalizes only after storage verification. | Upload evidence: __________________ | __________________ | Pending |
| WS-P20 | Critical | Inspect generated derivatives for accepted uploads. | Thumbnail and preview are generated and stored. | Derivative evidence: __________________ | __________________ | Pending |
| WS-P21 | Critical | Verify stat/existence behavior for originals and derivatives. | Existing objects return expected metadata through the application storage contract. | Stat evidence: __________________ | __________________ | Pending |
| WS-P22 | Critical | Read an authorized image through the application boundary. | Authorized read succeeds through an opaque application image endpoint/application-mediated stream. | Read evidence: __________________ | __________________ | Pending |
| WS-P23 | Critical | Inspect staff-facing image access. | Staff UI does not receive raw storage URLs or storage internals. | UI/network evidence: __________________ | __________________ | Pending |
| WS-P24 | Critical | Delete an image through authorized Museum-System behavior. | Original, thumbnail, and preview deletion are handled according to the application deletion contract. | Deletion evidence: __________________ | __________________ | Pending |
| WS-P25 | Major | Exercise already-missing deletion behavior in the approved test scenario. | Missing object deletion remains idempotent or recoverable according to application behavior. | Missing-object evidence: __________________ | __________________ | Pending |

## 9. Application Restart Tests

| Test ID | Criticality | Procedure | Expected result | Evidence | Actual result | Status |
| --- | --- | --- | --- | --- | --- | --- |
| WS-P26 | Critical | Upload an image/set, restart Museum-System, then read the existing image. | Existing image remains readable and metadata still points to the correct image. | Restart/read evidence: __________________ | __________________ | Pending |
| WS-P27 | Critical | Verify Primary Image behavior after application restart. | Primary Image state remains valid and uses existing PostgreSQL metadata plus object storage. | Primary evidence: __________________ | __________________ | Pending |
| WS-P28 | Critical | Perform a new upload after application restart. | New upload succeeds through the same storage contract. | Post-restart upload evidence: __________________ | __________________ | Pending |
| WS-P29 | Major | Replay an upload/idempotency scenario after application restart where practical. | Existing per-file outcome is returned or conflicting reuse is rejected according to idempotency rules. | Replay evidence: __________________ | __________________ | Pending |

## 10. Provider Interruption Tests

| Test ID | Criticality | Procedure | Expected result | Evidence | Actual result | Status |
| --- | --- | --- | --- | --- | --- | --- |
| WS-P30 | Critical | Attempt an authorized read while MinIO is unavailable. | Staff receives controlled unavailable/retry behavior with no raw provider details. | Unavailable-read evidence: __________________ | __________________ | Pending |
| WS-P31 | Critical | Attempt upload during temporary provider outage. | Operation fails safely or returns retryable outcome; no false successful image state is created. | Upload-outage evidence: __________________ | __________________ | Pending |
| WS-P32 | Critical | Interrupt deletion or simulate partial storage deletion failure. | Recoverable/auditable state is retained; system does not falsely claim complete permanent deletion. | Deletion-interruption evidence: __________________ | __________________ | Pending |
| WS-P33 | Critical | Restore MinIO availability after the interruption. | Provider becomes usable again for the implemented contract. | Recovery evidence: __________________ | __________________ | Pending |
| WS-P34 | Critical | Execute the approved internal recovery/retry path. | `StorageOperationRecovery` semantics reconcile correctly and audit remains intact. | Recovery-path evidence: __________________ | __________________ | Pending |
| WS-P35 | Critical | Restart the application while recovery remains unresolved. | Durable recovery state survives; no guessing or fabricated correlation is introduced. | Restart-with-recovery evidence: __________________ | __________________ | Pending |

Do not create an operator Web UI, do not create a sixth Photography permission, and do not manually mark recovery rows resolved merely to pass the PoC.

## 11. StorageOperationRecovery Evidence

`StorageOperationRecovery` is not backup, replication, high availability, or disaster recovery. It is an internal consistency and reconciliation mechanism for storage operations.

| Test ID | Criticality | Procedure | Expected result | Evidence | Actual result | Status |
| --- | --- | --- | --- | --- | --- | --- |
| WS-P36 | Critical | Observe recovery state transitions during approved failure/retry scenarios. | `Pending`, `Retrying`, `Resolved`, and `FailedNeedsAttention` are used only according to actual reconciliation outcomes. | Recovery-state evidence: __________________ | __________________ | Pending |
| WS-P37 | Critical | Review unresolved recovery retention. | Unresolved recovery records are retained and not deleted or guessed. | Retention evidence: __________________ | __________________ | Pending |
| WS-P38 | Critical | Inspect staff-facing failure surfaces during recovery-related workflows. | Staff UI remains free of storage-internal details. | Staff-boundary evidence: __________________ | __________________ | Pending |

## 12. Representative Image Workload

Define the representative workload before execution. Do not invent a throughput SLO.

| Workload field | Planned value |
| --- | --- |
| Number of artifacts | ______ |
| Number of images | ______ |
| Typical image size | ______ |
| Largest representative image size | ______ |
| Concurrent/repeated operator pattern | ______ |
| Test duration | ______ |
| Museum/IT operational acceptance notes | ______ |

| Test ID | Criticality | Procedure | Expected result | Evidence | Actual result | Status |
| --- | --- | --- | --- | --- | --- | --- |
| WS-P39 | Critical | Run the agreed representative workload. | Uploads complete without corruption; previews/thumbnails generate correctly; gallery reads remain operationally acceptable; deletion remains correct. | Workload evidence: __________________ | __________________ | Pending |
| WS-P40 | Major | Record server CPU/RAM/storage observations, capacity growth, errors, and retries during workload. | Observations are sufficient for museum/IT operational acceptance. | Observation evidence: __________________ | __________________ | Pending |

## 13. Disk / Storage Behavior

| Test ID | Criticality | Procedure | Expected result | Evidence | Actual result | Status |
| --- | --- | --- | --- | --- | --- | --- |
| WS-P41 | Critical | Verify configured object data path across service restart and Windows reboot. | Data path survives restart/reboot; filesystem permissions remain correct for the service identity. | Disk/reboot evidence: __________________ | __________________ | Pending |
| WS-P42 | Major | Review low-space/insufficient-space behavior using an approved safe method. | Behavior is understood and recorded; disk usage after representative workload is recorded. | Capacity evidence: __________________ | __________________ | Pending |

The actual configured path is deployment configuration only. Do not encode a Windows path in application business logic and do not treat a single disk as HA.

## 14. Network And TLS

| Test ID | Criticality | Procedure | Expected result | Evidence | Actual result | Status |
| --- | --- | --- | --- | --- | --- | --- |
| WS-P43 | Critical | Verify DNS/hostname or approved endpoint from the Museum-System host. | Endpoint resolves and routes according to the approved topology. | Network evidence: __________________ | __________________ | Pending |
| WS-P44 | Critical | Verify required port connectivity and firewall path. | Museum-System can reach MinIO only through approved network paths. | Firewall evidence: __________________ | __________________ | Pending |
| WS-P45 | Critical where TLS is enabled/required | Verify TLS handshake, certificate trust, expiry, and hostname checks. | TLS satisfies approved deployment and network policy. | TLS evidence: __________________ | __________________ | Pending |
| WS-P46 | Critical | Restart Museum-System after network/TLS validation. | Connection remains valid after restart; provider errors are mapped to staff-safe behavior. | Restart/network evidence: __________________ | __________________ | Pending |

## 15. Export / Migration Feasibility

This section validates feasibility only. It does not implement migration tooling and does not mandate a specific external transfer tool.

| Test ID | Criticality | Procedure | Expected result | Evidence | Actual result | Status |
| --- | --- | --- | --- | --- | --- | --- |
| WS-P47 | Critical | Inventory representative original and derivative objects using an approved method. | Object inventory can be produced without rewriting PostgreSQL keys. | Inventory evidence: __________________ | __________________ | Pending |
| WS-P48 | Critical | Export/copy representative originals and derivatives while preserving object keys. | Objects can be copied/exported with keys and content preserved. | Export evidence: __________________ | __________________ | Pending |
| WS-P49 | Critical | Verify representative transferred objects. | Approved count, checksum, or inventory comparison confirms expected content. | Verification evidence: __________________ | __________________ | Pending |
| WS-P50 | Critical | Exercise controlled write pause or equivalent migration discipline if used. | Writes are prevented during controlled migration exercise when required by the approved plan. | Write-control evidence: __________________ | __________________ | Pending |
| WS-P51 | Critical | Restore source configuration if target validation fails in the exercise. | Operators can return to the explicitly preserved source configuration according to the approved plan. | Rollback evidence: __________________ | __________________ | Pending |

Do not rewrite PostgreSQL object keys during this PoC.

## 16. Coordinated State Awareness

Operator acknowledgments:

- [ ] PostgreSQL metadata and object binaries form one logical Photography recovery state.
- [ ] PostgreSQL-only recovery is incomplete.
- [ ] Object-only recovery is incomplete.
- [ ] Mismatched restore points can create inconsistent state.
- [ ] This PoC does not implement backup or restore.
- [ ] Production approval of MinIO does not approve a backup design.

This section records constraints only and does not perform T129.

## 17. Failure / No-Go Conditions

A No-Go decision is required if any critical item proves that:

- MinIO cannot reliably start/restart on the target environment.
- Windows reboot leaves the approved service topology unusable.
- Existing stored objects become unavailable or corrupt after normal restart.
- Private access cannot be enforced.
- Protected credential handling cannot be achieved.
- Required TLS/network connectivity cannot be achieved under approved policy.
- Museum-System cannot upload, read, and delete through the implemented contract.
- Application or staff UI exposes raw storage internals to staff.
- Provider interruption can cause false successful state without recoverable evidence.
- `StorageOperationRecovery` cannot safely reconcile required tested scenarios.
- Representative workload is operationally unacceptable to museum/IT staff.
- Export/migration feasibility cannot be demonstrated sufficiently for the deployment decision.

No No-Go condition is marked triggered by creating this document.

## 18. Go Criteria

A future Go decision may be recorded only when:

- every critical PoC test is executed and passed;
- no unresolved critical defect remains;
- all required evidence is attached or referenced;
- museum IT/operator review is completed;
- security, network, and storage requirements are accepted;
- representative workload is accepted;
- migration/export feasibility is accepted.

Current decision remains Pending / Not executed.

## 19. Decision Record

Decision:

- [ ] GO
- [ ] NO-GO
- [ ] HOLD / RETEST

Decision date: __________________

Decision makers: __________________

Evidence package: __________________

Known limitations: __________________

Required remediation: __________________

Retest date: __________________

Current state: PENDING / NOT EXECUTED.

## 20. Evidence Table

| Evidence area | Criticality | Status | Executor | Date | Evidence reference | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Initial start | Critical | Pending | __________________ | __________________ | __________________ | Service lifecycle evidence. |
| Controlled service stop/start | Critical | Pending | __________________ | __________________ | __________________ | Service lifecycle evidence. |
| Windows reboot | Critical | Pending | __________________ | __________________ | __________________ | Service lifecycle evidence. |
| Repeated restart | Major | Pending | __________________ | __________________ | __________________ | Service lifecycle evidence. |
| Security and private access | Critical/Major | Pending | __________________ | __________________ | __________________ | Private bucket, anonymous denial, credentials, and least privilege. |
| Configuration contract | Critical | Pending | __________________ | __________________ | __________________ | Provider, timeout, region, and protected secret supply. |
| Core storage contract | Critical/Major | Pending | __________________ | __________________ | __________________ | Upload, derivative, stat, read, and delete behavior. |
| Application restart and idempotency | Critical/Major | Pending | __________________ | __________________ | __________________ | Restart readability, primary image state, upload, and replay behavior. |
| Provider interruption and recovery | Critical | Pending | __________________ | __________________ | __________________ | Outage, deletion interruption, restoration, retry, and durable unresolved recovery. |
| StorageOperationRecovery | Critical | Pending | __________________ | __________________ | __________________ | State transitions, retention, and staff-safe failure surfaces. |
| Representative workload | Critical/Major | Pending | __________________ | __________________ | __________________ | Workload execution and operational observations. |
| Disk/storage behavior | Critical/Major | Pending | __________________ | __________________ | __________________ | Restart/reboot persistence, permissions, and capacity observation. |
| Network and TLS | Critical | Pending | __________________ | __________________ | __________________ | Endpoint, firewall path, certificate trust, and restart behavior. |
| Export/migration feasibility | Critical | Pending | __________________ | __________________ | __________________ | Inventory, copy/export, verification, write control, and rollback. |

No screenshots, logs, dates, operators, or results are fabricated by this checklist.

## 21. Production-Approval Warning

Creating or completing this checklist document does not itself approve MinIO on Windows Server 2019 for production.

T128 only prepares the PoC acceptance instrument. Production approval requires actual execution and recorded evidence.

Feature 003 does not implement backup, high availability, replication, failover, disaster recovery, or automatic migration.
