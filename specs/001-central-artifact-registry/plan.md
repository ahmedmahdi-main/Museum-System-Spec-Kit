# Implementation Plan: السجل المركزي للقطع وإدارة المخزن وحركة التسليم والاستلام

**Branch**: `001-central-artifact-registry` | **Date**: 2026-08-11 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-central-artifact-registry/spec.md`

## Summary

تنفيذ المرحلة الأولى كتطبيق Modular Monolith واحد وقاعدة بيانات واحدة يخدم موظفي المخزن والسجل والجرد. الخطة تنشئ أساس السجل المركزي للقطع، فئات القطع، مواقع الخزن وقاعات العرض، حركة التسليم والاستلام، استيراد Excel بدورة Preview -> Validation -> Explicit Commit، والجرد مع تصحيحات موثقة. التصميم يبقي الحدود المنطقية بين الوحدات واضحة دون Microservices أو Event Bus أو CQRS أو تعقيدات غير مثبتة.

## Technical Context

**Language/Version**: C# على .NET 10

**Primary Dependencies**: ASP.NET Core, Blazor Web App, ASP.NET Core Identity, Entity Framework Core, Npgsql EF Core Provider, ClosedXML لاستيراد Excel `.xlsx`

**Storage**: PostgreSQL كقاعدة بيانات واحدة للنظام في المرحلة الحالية

**Testing**: اختبارات وحدة لقواعد الدومين، اختبارات تكامل للتخزين وعمليات الخدمات، اختبارات واجهة/قبول لمسارات الموظف الأساسية

**Target Platform**: Windows Server أولاً، مع دعم تشغيل اختياري عبر Docker Compose دون أن يكون Docker شرطاً

**Project Type**: تطبيق ويب مؤسسي داخلي بواجهة عربية RTL

**Performance Goals**: البحث المعتاد يعرض النتائج خلال ثانيتين في 95% من الحالات؛ تسليم قطعة واحدة بعد العثور عليها خلال أقل من دقيقة؛ عملية جماعية من 20 قطعة دون إدخال متكرر للبيانات

**Constraints**: Modular Monolith فقط؛ لا Microservices؛ لا RabbitMQ؛ لا gRPC؛ لا Event Bus؛ لا CQRS/MediatR/Event Sourcing ما لم تظهر حاجة موثقة لاحقاً؛ الصور والتوثيق العلمي والمختبر والصيانة خارج النطاق

**Scale/Scope**: مرحلة أولى لمتحف واحد، قاعدة بيانات واحدة، أدوار موظفين داخلية، وملفات Excel تمثل الجرد الحالي في المخازن وقاعات العرض

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Plan Response |
|-----------|--------|---------------|
| Artifact-Centered Digital Identity | PASS | ArtifactId داخلي ثابت لكل قطعة، والرقم المتحفي منفصل للعرض والبحث. |
| Single Source of Truth | PASS | السجل المركزي هو المصدر الوحيد للقطعة؛ الوحدات الأخرى تشير إليه ولا تنشئ نسخة. |
| Modular Monolith First | PASS | تطبيق واحد وقاعدة واحدة مع حدود modules داخلية واضحة. |
| Staff-Centered Operational Experience | PASS | مسارات التسليم والاستلام والاستيراد مصممة كخطوات قليلة وواضحة. |
| Integrity Before Convenience | PASS | القيود الحرجة تطبق في الدومين والتخزين، والعملية الجماعية ترفض كاملة عند وجود قطعة غير مؤهلة. |
| Traceable Custody, Movement, and Location | PASS | Movement history وDocumented Correction غير قابلين للحذف تشغيلياً. |
| Clear Domain Ownership | PASS | كل وحدة تملك مفاهيمها وقواعدها وحدود التحقق الخاصة بها. |
| Security, Permissions, and Audit by Design | PASS | Identity + roles/permissions، وتدقيق عمليات الإنشاء والحركة والاستيراد والتصحيح. |
| Verifiable Legacy Data Migration | PASS | Excel import لا يعتمد مباشرة؛ Preview/Validation/Commit مع تقارير أخطاء. |
| Backup and Recovery Readiness | PASS | الخطة تتضمن نهج migration/deployment ونسخ احتياطي قبل الاعتماد. |
| Infrastructure Independence | PASS | التشغيل المباشر على Windows Server شرط أساسي؛ Docker اختياري. |
| Critical Business Rule Testing | PASS | الاختبارات تغطي الهوية، التفرد، الحركة، العمليات الجماعية، الاستيراد، الجرد. |
| No Premature Over-Engineering | PASS | استبعاد CQRS/MediatR/Event Sourcing/Event Bus وMicroservices. |
| User-Validated Incremental Phases | PASS | الخطة مقسمة إلى مراحل صغيرة قابلة للتحقق. |

No constitution violations. Complexity Tracking is not required.

## Project Structure

### Documentation (this feature)

```text
specs/001-central-artifact-registry/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── application-use-cases.md
│   └── ui-workflows.md
├── checklists/
│   └── requirements.md
└── spec.md
```

### Source Code (repository root)

```text
Museum-System.sln
src/
├── MuseumSystem.Web/                  # Blazor Web App; Arabic RTL staff UI; composition root
│   ├── Components/
│   │   ├── Layout/
│   │   └── Pages/
│   │       ├── Artifacts/
│   │       ├── Storehouse/
│   │       └── Imports/
│   └── wwwroot/
├── MuseumSystem.Domain/               # Domain entities, value objects, rules by module
│   └── Modules/
│       ├── ArtifactRegistry/
│       ├── StorehouseOperations/
│       ├── Import/
│       └── IdentityAccess/
├── MuseumSystem.Application/          # Use cases/application services and DTO contracts
│   └── Modules/
│       ├── ArtifactRegistry/
│       ├── StorehouseOperations/
│       ├── Import/
│       └── IdentityAccess/
└── MuseumSystem.Infrastructure/       # EF Core persistence, Identity storage, Excel adapter, audit
    ├── Persistence/
    ├── Identity/
    ├── Excel/
    └── Audit/

tests/
├── MuseumSystem.Domain.Tests/
├── MuseumSystem.Application.Tests/
├── MuseumSystem.Integration.Tests/
└── MuseumSystem.Web.AcceptanceTests/
```

**Structure Decision**: حل واحد، تطبيق Web واحد قابل للنشر، وقاعدة PostgreSQL واحدة. تقسيم المشاريع يحمي الدومين وقواعد العمل من تفاصيل الواجهة والتخزين دون تحويل النظام إلى خدمات منفصلة.

## Module Boundaries

### Artifact Registry

**Owns**: Artifact, ArtifactCategory, CategoryCode, MuseumNumber, identity rules, category lifecycle.

**Responsibilities**:
- إدارة هوية القطعة الأساسية.
- إدارة الفئات ورقم الفئة الرسمي CategoryCode.
- إنشاء ArtifactId الداخلي الثابت.
- ضمان تفرد الرقم المتحفي المبني من CategoryCode + ItemNumber.
- حفظ بيانات القطعة الأساسية اللازمة للبحث والتوثيق.

**Does Not Own**: عمليات المخزن، العهدة، الحركة، الجرد، أو استيراد Excel.

### Storehouse Operations

**Owns**: StorageLocation, DisplayHall/DisplayLocation classification, current location/holder state, custody, delivery/return, movement history, inventory/reconciliation sessions, and documented corrections.

**Responsibilities**:
- المواقع: إنشاء وتحديث مواقع الخزن ومواقع العرض القابلة للاختيار.
- الحالة الحالية: إدارة CurrentLocation وCurrentHolder وLastKnownStorageLocation.
- العهدة: تسجيل الجهة الحائزة للقطعة عند خروجها من المخزن.
- التسليم والاستلام: تنفيذ التسليم والإرجاع كعمليات موثقة.
- سجل الحركة: حفظ MovementRecord كسجل تاريخي غير قابل للاستبدال بالحالة الحالية.
- الجرد والتصحيحات: إدارة جلسات الجرد، نتائج المطابقة، والتصحيحات الموثقة.

**Does Not Own**: قواعد هوية القطعة أو تحليل/اعتماد ملفات Excel.

### Import

**Owns**: ImportBatch, ImportRowIssue, Excel preview, validation, and explicit commit.

**Responsibilities**:
- Preview لملفات Excel قبل أي تغيير دائم.
- Validation للصفوف والقيم والروابط المطلوبة.
- Explicit Commit للبيانات المقبولة فقط.
- حفظ مشاكل الصفوف وملخصات الاستيراد.

**Does Not Own**: العهدة، سجل الحركة، الجرد والتصحيحات، أو الحالة الحالية إلا من خلال commit موثق ومتحقق.

### Identity & Access

**Owns**: users, roles, permissions, authorization policies, audit actor context.

**Responsibilities**:
- إدارة المستخدمين والأدوار والصلاحيات.
- توفير سياق الفاعل لكل عمليات التدقيق.
- تعريف permissions قابلة للتوسع دون hard-coding داخل الشاشات.

## Domain Model

### Core Identity

- **ArtifactId**: معرف داخلي ثابت لا يتغير ويستخدم للربط التقني بين الجداول والسجلات.
- **CategoryCode**: رقم الفئة الرسمي، إلزامي وفريد، وهو الجزء الأول من الرقم المتحفي.
- **MuseumNumber**: الرقم المتحفي يتكون من `CategoryCode + ItemNumber` فقط. `CategoryId` معرف داخلي تقني ولا يدخل في الرقم المتحفي.
- **ArtifactCategory**: الفئة التي تحمل CategoryCode الرسمي واسمها ووصفها وحالة استخدامها.

### Artifact State

- **CurrentStatus**: `InStorage` أو `OutOfStorage`.
- **CurrentLocationId**: داخل المخزن يمثل موقع الخزن الحالي؛ وفي قاعة العرض يمثل موقع العرض. لا يعبأ عندما تكون القطعة لدى التوثيق/المختبر/المصور ولا يوجد موقع خزن حالي.
- **CurrentHolderType**: جهة العهدة الحالية عند خروج القطعة: DocumentationDivision, LaboratoryDivision, Photographer, DisplayHall.
- **CurrentHolderName**: اسم الجهة أو قاعة العرض.
- **LastKnownStorageLocationId**: آخر موقع خزن معروف، ويبقى محفوظاً عند خروج القطعة للتوثيق/المختبر/المصور أو العرض.
- **ConcurrencyToken**: قيمة تفاؤلية لمنع تعارض تحديثات التسليم/الاستلام/التصحيح.

Current state rules:
- داخل المخزن: `CurrentLocationId` = موقع الخزن، و`CurrentHolderType/Name` فارغة.
- لدى التوثيق/المختبر/المصور: `CurrentHolderType/Name` = الجهة، ولا يوجد `CurrentLocationId` حالي.
- في قاعة العرض: `CurrentLocationId` = موقع العرض، و`CurrentHolderType/Name` = قاعة العرض.
- عند خروج القطعة من المخزن، تبقى قيمة `LastKnownStorageLocationId` محفوظة.

### Movement State Transitions

```text
InStorage --Deliver--> OutOfStorage
OutOfStorage --Return--> InStorage
InStorage --DocumentedCorrection--> InStorage with corrected location
OutOfStorage --DocumentedCorrection--> allowed only for documented current-location/holder correction, not as substitute for Return
```

### Bulk Atomicity

- كل bulk delivery أو bulk return عملية ذرية على مستوى use case.
- إذا كانت أي قطعة غير مؤهلة، تفشل العملية كاملة ولا تتغير حالة أي قطعة.
- يعرض النظام قائمة القطع غير المؤهلة وأسباب الرفض حتى يعدل الموظف الاختيار بسرعة.

## Data Model

تفاصيل الجداول في [data-model.md](./data-model.md). القرارات الأساسية:
- Unique constraint على `ArtifactCategory.CategoryCode` لأنه رقم الفئة الرسمي الإلزامي.
- Unique museum number على `CategoryCode + ItemNumber`; `CategoryId` يستخدم كمعرف داخلي/مفتاح أجنبي فقط ولا يظهر في الرقم المتحفي.
- Foreign keys من سجلات الحركة والجرد والتصحيحات والاستيراد إلى `ArtifactId` الداخلي.
- MovementRecord وDocumentedCorrection وImportBatch تبقى append-only حسب قواعد التدقيق.
- Concurrency token على Artifact وعلى ImportBatch للحالات الحساسة.
- Audit metadata على العمليات المهمة: CreatedBy, CreatedAt, LastModifiedBy, LastModifiedAt حسب الحاجة.

## Main Application Services / Use Cases

### Artifact Registry

- `CreateCategory`
- `UpdateCategory`
- `DisableCategoryForNewUse`
- `CreateArtifact`
- `UpdateArtifactBasicInfo`
- `SearchArtifacts`
- `GetArtifactDetails`

### Storehouse Operations

- `CreateLocation`
- `UpdateLocation`
- `DisableLocationForNewUse`
- `ListSelectableLocations`
- `PreviewDeliveryEligibility`
- `DeliverArtifacts`
- `PreviewReturnEligibility`
- `ReturnArtifacts`
- `GetMovementHistory`
- `StartReconciliationSession`
- `RecordReconciliationItems`
- `ReviewReconciliationResults`
- `CreateDocumentedCorrection`

### Import

- `UploadImportFileForPreview`
- `ValidateImportBatch`
- `CommitImportBatch`
- `CancelImportBatch`

### Identity & Access

- `ManageUsers`
- `AssignRoles`
- `AssignPermissions`
- `ViewAuditTrail`

## Validation Boundaries

- **UI validation**: الحقول المطلوبة، صيغ الأرقام، اختيار الجهة/الموقع، رسائل قصيرة قابلة للتصرف.
- **Application validation**: أهلية القطع للحركة، رفض العملية الجماعية كاملة، دورة Excel، صلاحيات المستخدم، عدم تخطي خطوات الاعتماد.
- **Domain validation**: هوية القطعة، حالات الحركة المسموحة، منع حذف التاريخ، التفرد المنطقي للرقم المتحفي.
- **Persistence validation**: unique constraints، foreign keys، optimistic concurrency، عدم فقدان العلاقات التاريخية.

## Authorization Boundaries

| Permission | Typical Roles | Scope |
|------------|---------------|-------|
| `Artifacts.View` | Viewer, Storekeeper, RegistryManager, InventoryOfficer, Admin | البحث وعرض التفاصيل |
| `Artifacts.Manage` | RegistryManager, Admin | الفئات والسجل الأساسي |
| `Storehouse.Locations.Manage` | Storekeeper, Admin | مواقع الخزن وقاعات العرض |
| `Storehouse.Deliver` | Storekeeper, Admin | التسليم |
| `Storehouse.Return` | Storekeeper, Admin | الاستلام |
| `Imports.Preview` | RegistryManager, InventoryOfficer, Admin | رفع ومعاينة Excel |
| `Imports.Commit` | RegistryManager, Admin | اعتماد الاستيراد |
| `Storehouse.Reconciliation.Manage` | InventoryOfficer, Admin | الجرد والمطابقة |
| `Storehouse.Corrections.Create` | RegistryManager, InventoryOfficer, Admin | التصحيح الموثق |
| `Audit.View` | Admin, RegistryManager | سجل التدقيق |
| `Identity.Manage` | Admin | المستخدمون والأدوار |

## Staff UX Plan

- واجهة عربية RTL من البداية، مع مصطلحات موحدة: الرقم المتحفي، الحالة الحالية، الجهة الحالية، آخر موقع خزن.
- شاشة بحث واحدة تعرض نتائج مختصرة وتفتح تفاصيل القطعة.
- التسليم: اختيار قطع -> جهة/مستلم/غرض/ملاحظة -> مراجعة وحفظ.
- الاستلام: اختيار قطع خارج المخزن -> موقع خزن/ملاحظة -> مراجعة وحفظ.
- الاستيراد: رفع ملف -> معاينة -> أخطاء قابلة للتصفية -> اعتماد المقبول.
- الجرد: اختيار موقع -> إدخال/تحميل قائمة القطع -> تقرير تعارضات -> تصحيح موثق عند التأكيد.
- كل رفض يعرض سبباً قابلاً للتصرف دون stack traces أو مصطلحات تقنية.

## Testing Strategy

### Unit Tests

- MuseumNumber uniqueness rules.
- CategoryCode required/unique rules and exclusion of CategoryId from MuseumNumber.
- ArtifactId immutability.
- Movement state transitions.
- CurrentLocation/CurrentHolder/LastKnownStorageLocation state rules.
- Bulk atomicity failure when one artifact is ineligible.
- Import validation rules before commit.
- Reconciliation classification rules.
- Documented correction rules.

### Application Tests

- `DeliverArtifacts` commits all selected artifacts or none.
- `ReturnArtifacts` updates current location and last known storage location.
- `CommitImportBatch` refuses unvalidated or conflicted batches.
- `CreateDocumentedCorrection` records audit/correction history and updates current state.
- Authorization policies reject users without permission.

### Integration Tests

- PostgreSQL constraints for unique `ArtifactCategory.CategoryCode` and unique museum number derived from `CategoryCode + ItemNumber`.
- EF Core optimistic concurrency on sensitive updates.
- Identity role/permission persistence.
- Import preview does not mutate artifact tables.
- Movement history remains after state updates.

### Web/Acceptance Tests

- Arabic RTL layout sanity for search, delivery, return, import preview, and reconciliation screens.
- Staff can complete delivery/return in expected step count.
- Bulk rejection shows ineligible artifacts and reasons.
- Excel preview/validation/commit flow is visible and irreversible only after explicit commit.

## Migration / Deployment Approach

1. Create solution/projects and baseline configuration for direct Windows Server execution.
2. Add PostgreSQL connection configuration through environment/app settings without Docker dependency.
3. Create initial EF Core migration for Identity, audit, registry, storehouse operations, import.
4. Seed initial roles and permissions only; do not seed museum artifact data except controlled test fixtures.
5. Deploy to Windows Server using published app artifacts and configured PostgreSQL instance.
6. Before first real Excel commit, take database backup and run import preview/validation with staff review.
7. Docker Compose is supported as an optional packaging/runtime path with only `MuseumSystem.Web` and PostgreSQL, a persistent PostgreSQL volume, environment-based configuration, and database health checks.
8. Direct Windows Server deployment remains supported and does not depend on Docker.

## Technical Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Legacy Excel files vary in columns and naming | Import delays and staff confusion | Preview mapping, clear row-level errors, sample template, no mutation before commit |
| Duplicate museum numbers in real inventory | Blocks clean migration | Validation report and correction workflow before commit |
| Concurrent delivery/return by two users | Incorrect current state | Optimistic concurrency on Artifact and transaction boundary around bulk operations |
| Overly broad permissions | Audit and custody risk | Permission-based policies with role presets and audit trail |
| UI becomes too form-heavy | Staff adoption risk | Keep primary flows to few screens, reuse search and bulk selection, short actionable errors |
| Treating correction as a hidden movement | History integrity risk | Separate DocumentedCorrection entity and audit reason |
| Premature architecture expansion | Slower phase one delivery | No Microservices/Event Bus/CQRS/MediatR/Event Sourcing unless future evidence demands it |
| Excel library licensing mismatch | Legal/operational risk | Prefer MIT-licensed ClosedXML; review license before implementation |

## Phase Plan

### Phase A - Foundation

- Solution structure, Identity baseline, PostgreSQL/EF Core setup, audit foundation.
- Empty RTL Blazor shell with authenticated staff layout.

### Phase B - Registry & Storehouse Foundation

- Categories, artifact creation, uniqueness enforcement, locations, search, detail view.
- Tests for ArtifactId, MuseumNumber, and location lifecycle.

### Phase C - Storehouse Operations

- Delivery/return use cases, bulk atomicity, movement history, current state updates.
- Tests for transitions, concurrency, and staff flow.

### Phase D - Excel Import

- Upload preview, validation report, explicit commit, import audit.
- Tests proving preview does not mutate and commit enforces rules.

### Phase E - Reconciliation & Corrections

- Reconciliation sessions, result classification, documented correction, audit visibility.
- Tests for conflict handling and correction history.

### Phase F - Hardening & UAT

- Acceptance scenarios, RTL usability review, backup/restore drill, performance checks against success criteria.

### Phase G - Optional Docker Compose Packaging

- Optional multi-stage Docker image for `MuseumSystem.Web`.
- Optional Docker Compose file with only the web application and PostgreSQL.
- Environment-based PostgreSQL connection settings, no committed secrets, and no additional infrastructure services.
- Docker remains optional and is not a prerequisite for direct Windows Server deployment.

## Post-Design Constitution Check

PASS. The design keeps one deployable Modular Monolith application and one PostgreSQL database, preserves artifact identity and custody history, provides audit and permission boundaries, verifies Excel migration before commit, avoids excluded features and architectural overreach, and keeps Docker Compose as optional packaging rather than a required runtime dependency.

## Complexity Tracking

No constitution violations or intentional complexity exceptions.
