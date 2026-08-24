# Storage Abstraction Contract: Feature 003

The names below are conceptual. Final class/interface names may follow repository conventions during implementation.

## Boundary

Domain and Application code depend on storage abstractions and structured result types. Infrastructure contains the concrete MinIO/S3-compatible implementation and SDK-specific error handling.

No Domain/Application/Web business behavior may depend on:

- MinIO SDK types.
- Bucket names.
- Public permanent object URLs.
- Windows filesystem paths.
- Docker, WSL, or Linux-specific deployment assumptions.

## IArtifactImageStorage

### Store Original

**Input**:

- Generated object key
- Image stream
- Content type
- Length
- Optional checksum/idempotency metadata

**Result**:

- Success with object key and storage metadata
- Already exists/conflict
- Retryable failure
- Permanent provider/configuration failure

### Store Derivative

**Input**:

- Generated derivative object key
- Derivative stream
- Content type
- Length
- Derivative kind

**Result**:

- Success with object key and metadata
- Retryable or permanent failure

### Stat/Exists

**Input**:

- Object key

**Result**:

- Exists with metadata
- Not found
- Retryable failure
- Unauthorized/misconfigured provider

### Open Read Stream

**Input**:

- Object key

**Result**:

- Stream and content metadata
- Not found
- Retryable failure
- Unauthorized/misconfigured provider

### Create Short-Lived Read Access

**Input**:

- Object key
- Requested lifetime within application-configured limit

**Result**:

- Short-lived access token/URL
- Not supported, in which case application streaming is used
- Failure result

**Rule**: Public permanent object URLs are never stored or treated as authority. Feature 003 staff UI does not expose raw provider URLs if they reveal bucket names, object keys, provider endpoints, or other storage internals; staff access defaults to an opaque application endpoint/application-mediated streaming.

### Delete Object

**Input**:

- Object key

**Result**:

- Deleted
- Already missing, treated according to caller operation context
- Retryable failure
- Permanent provider/configuration failure

### Delete Image Objects

**Input**:

- Original object key
- Exclusive derivative object keys

**Result**:

- All deleted
- Partial failure with per-object result
- Retryable or permanent provider failure

**Rule**: The caller must record recoverable/auditable state when partial failure prevents complete deletion.

## IArtifactImageProcessor

### Validate Image

**Input**:

- File stream
- Original filename

**Result**:

- Valid JPEG or PNG with detected content type, dimensions, normalized extension, and safe metadata
- Rejected unsupported/invalid file with staff-facing reason

**Rule**: Extension alone is not authoritative. The concrete package is selected only after dependency/license compatibility review. ImageSharp is a candidate, but Domain/Application contracts must not expose ImageSharp-specific types.

### Generate Derivatives

**Input**:

- Validated image stream

**Result**:

- Thumbnail derivative stream and metadata
- Preview derivative stream and metadata
- Recoverable generation failure

**Rule**: Original binary is never overwritten or mutated.

## Idempotency

Upload commands accept application idempotency keys persisted in PostgreSQL.

Storage object keys are generated before upload and recorded with `PhotographyUploadOperation` and per-file outcome state so retry can distinguish:

- already completed same operation;
- partial operation requiring cleanup/retry;
- new operation with different files;
- conflicting duplicate idempotency key usage.

The idempotency scope is actor plus operation kind plus key. A request fingerprint detects same-key/different-input conflicts. Per-file outcomes make partial success repeatable after application restart.

## Private Access

The object bucket/container is private. Authorized staff access images only through:

- opaque application endpoints/application streaming after authorization.

Short-lived provider access may remain an internal capability for future use only when it preserves the approved opaque application security boundary.

Staff-facing responses never expose storage internals as business identifiers.
