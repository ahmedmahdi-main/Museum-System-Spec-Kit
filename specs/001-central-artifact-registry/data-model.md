# Data Model: السجل المركزي للقطع وإدارة المخزن وحركة التسليم والاستلام

## Overview

النموذج يحافظ على هوية القطعة داخلياً عبر `ArtifactId`، وعلى الرقم المتحفي الرسمي عبر `MuseumNumber` المبني من `CategoryCode + ItemNumber` فقط. `CategoryId` يبقى معرفاً داخلياً تقنياً للفئة ومفتاحاً أجنبياً، ولا يدخل في الرقم المتحفي أو عرضه للموظفين. عمليات المخزن تجمع المواقع، الحالة الحالية، العهدة، التسليم والاستلام، سجل الحركة، الجرد والتصحيحات ضمن حدود واحدة دون إضافة تعقيد معماري جديد.

## Entities

### ArtifactCategory

**Purpose**: تعريف الفئات الرسمية التي تبدأ بها أرقام القطع المتحفية.

**Fields**:
- `CategoryId`: معرف داخلي تقني للفئة.
- `CategoryCode`: رقم الفئة الرسمي، إلزامي وفريد، ويستخدم في تكوين `MuseumNumber`.
- `NameArabic`: اسم الفئة بالعربية.
- `Description`: وصف اختياري.
- `IsActive`: يحدد إمكانية استخدام الفئة في قطع جديدة.
- `CreatedAt`, `CreatedBy`, `LastModifiedAt`, `LastModifiedBy`: بيانات تدقيق.

**Rules**:
- `CategoryCode` مطلوب وفريد على مستوى النظام.
- `CategoryCode` هو الجزء الرسمي من الرقم المتحفي؛ `CategoryId` لا يظهر في الرقم المتحفي ولا يستخدم كجزء منه.
- لا يسمح بتغيير `CategoryCode` بعد استخدام الفئة في قطع متحفية إلا عبر تصحيح موثق ومعتمد.
- تعطيل الفئة يمنع استخدامها في قطع جديدة ولا يغير أرقام القطع القائمة.

### Artifact

**Purpose**: سجل القطعة المتحفية وهويتها وحالتها التشغيلية الحالية.

**Fields**:
- `ArtifactId`: معرف داخلي ثابت للقطعة.
- `CategoryId`: معرف داخلي تقني للفئة، يستخدم كمرجع إلى `ArtifactCategory` فقط.
- `ItemNumber`: رقم القطعة داخل الفئة.
- `MuseumNumberDisplay`: الرقم المتحفي المعروض والمكوّن من `ArtifactCategory.CategoryCode + ItemNumber` فقط.
- `BasicDescription`: وصف أساسي مختصر للقطعة.
- `CurrentStatus`: حالة القطعة: `InStorage` أو `OutOfStorage`.
- `CurrentLocationId`: موقع الخزن الحالي داخل المخزن، أو موقع العرض عند وجود القطعة في قاعة عرض؛ ويكون فارغاً عندما تكون القطعة لدى التوثيق/المختبر/المصور.
- `CurrentHolderType`: جهة العهدة الحالية عند خروج القطعة من المخزن: DocumentationDivision, LaboratoryDivision, Photographer, DisplayHall.
- `CurrentHolderName`: اسم الجهة أو قاعة العرض الحائزة للقطعة.
- `LastKnownStorageLocationId`: آخر موقع خزن معروف، ويبقى محفوظاً عند خروج القطعة.
- `CreatedFromImportBatchId`: دفعة الاستيراد التي أنشأت السجل عند الحاجة.
- `ConcurrencyToken`: قيمة تفاؤلية لمنع تعارض التحديثات الحساسة.
- `CreatedAt`, `CreatedBy`, `LastModifiedAt`, `LastModifiedBy`: بيانات تدقيق.

**Relationships**:
- belongs to ArtifactCategory.
- current/last locations reference Location when applicable.
- has many MovementRecords.
- has many ReconciliationResults.
- has many DocumentedCorrections.

**Rules**:
- الرقم المتحفي الرسمي فريد عبر `CategoryCode + ItemNumber`.
- `CategoryId` معرف داخلي ومفتاح أجنبي فقط، ولا يدخل في `MuseumNumberDisplay`.
- `ArtifactId` لا يتغير بعد إنشاء القطعة.
- داخل المخزن: `CurrentLocationId` يشير إلى موقع الخزن الحالي، و`CurrentHolderType/Name` فارغة.
- لدى التوثيق/المختبر/المصور: `CurrentHolderType/Name` يحددان الجهة، ولا يوجد `CurrentLocationId` حالي.
- في قاعة العرض: `CurrentLocationId` يشير إلى موقع العرض، و`CurrentHolderType/Name` يحددان قاعة العرض.
- عند خروج القطعة من المخزن، تبقى `LastKnownStorageLocationId` محفوظة حتى بعد تحديث الحالة الحالية.

### Location

**Purpose**: مرجع موحد لمواقع الخزن وقاعات العرض.

**Fields**:
- `LocationId`: معرف داخلي ثابت.
- `NameArabic`: اسم الموقع.
- `LocationType`: Storage أو DisplayHall.
- `ParentLocationId`: اختياري للرفوف أو التقسيمات الداخلية إذا احتاج الموظفون ذلك دون تعقيد المرحلة الأولى.
- `IsActive`: صالح للاختيار في العمليات الجديدة.
- `CreatedAt`, `CreatedBy`, `LastModifiedAt`, `LastModifiedBy`: بيانات تدقيق.

**Rules**:
- الموقع المستخدم تاريخياً لا يحذف تشغيلياً؛ يعطل فقط.
- التسليم إلى قاعة العرض يعد خروجاً من المخزن مع بقاء الموقع/الجهة الحالية واضحة.

### MovementRecord

**Purpose**: سجل تاريخي للتسليم والاستلام.

**Fields**:
- `MovementId`: معرف الحركة.
- `MovementType`: Delivery أو Return.
- `MovementGroupId`: يربط القطع في العملية الجماعية الواحدة.
- `ArtifactId`: القطعة المرتبطة.
- `RecipientType`: DocumentationDivision, LaboratoryDivision, Photographer, DisplayHall عند التسليم.
- `RecipientName`: اسم المستلم عند التسليم.
- `Purpose`: الغرض من التسليم.
- `ReturnLocationId`: موقع الخزن عند الاستلام.
- `Note`: ملاحظة اختيارية.
- `OccurredAt`: التاريخ والوقت المسجلان تلقائياً.
- `RecordedBy`: المستخدم الذي حفظ الحركة.

**Rules**:
- لا يحذف تشغيلياً.
- Delivery مسموح فقط للقطع داخل المخزن.
- Return مسموح فقط للقطع خارج المخزن.
- العملية الجماعية تفشل كاملة إذا فشلت أهلية أي قطعة.

### ImportBatch

**Purpose**: تمثيل دورة استيراد Excel من الرفع حتى الاعتماد.

**Fields**:
- `ImportBatchId`: معرف الدفعة.
- `FileName`: اسم الملف الأصلي.
- `Status`: Previewed, ValidatedWithErrors, ReadyToCommit, Committed, Cancelled.
- `UploadedAt`, `UploadedBy`.
- `ValidatedAt`, `ValidatedBy`.
- `CommittedAt`, `CommittedBy`.
- `TotalRows`, `AcceptedRows`, `RejectedRows`.
- `ConcurrencyToken`: يمنع اعتماد الدفعة مرتين.

**Rules**:
- الرفع والمعاينة لا يغيران Artifact أو Location.
- لا يمكن commit قبل validation ناجح للسجلات المقبولة.
- commit ينشئ السجلات المقبولة فقط ويحتفظ بالأخطاء للمراجعة.

### ImportRow

**Purpose**: صف واحد مقروء من Excel وحالة تحققه.

**Fields**:
- `ImportRowId`.
- `ImportBatchId`.
- `RowNumber`.
- `CategoryValue`, `ItemNumberValue`, `LocationValue`, `DescriptionValue`.
- `ProposedCategoryId`, `ProposedLocationId`, `ProposedArtifactId` عند المطابقة.
- `Status`: Accepted, Rejected, NeedsReview.
- `Issues`: قائمة أسباب مفهومة للموظف.

**Rules**:
- الصف المكرر داخل الفئة لا يعتمد.
- الموقع غير المعروف يرفض أو يعلّم للمراجعة حسب قاعدة الاستيراد المعتمدة في tasks.

### ReconciliationSession

**Purpose**: عملية جرد ومطابقة لموقع محدد.

**Fields**:
- `ReconciliationSessionId`.
- `LocationId`.
- `StartedAt`, `StartedBy`.
- `CompletedAt`, `CompletedBy`.
- `Status`: Draft, Completed, Reviewed.
- `Note`.

**Rules**:
- الجرد لا يغير الحالة الحالية تلقائياً.
- نتائج الجرد تبقى قابلة للمراجعة بعد التصحيح.

### ReconciliationResult

**Purpose**: نتيجة مطابقة قطعة أو رقم ضمن جلسة جرد.

**Fields**:
- `ReconciliationResultId`.
- `ReconciliationSessionId`.
- `ArtifactId`: اختياري إذا لم يطابق الرقم قطعة معروفة.
- `ObservedMuseumNumber`.
- `ExpectedLocationId`.
- `ObservedLocationId`.
- `ResultType`: Matched, Missing, Extra, Conflict, NeedsReview.
- `IssueDescription`.

**Rules**:
- conflict لا يغير Artifact مباشرة.
- التصحيح ينتج DocumentedCorrection منفصل.

### DocumentedCorrection

**Purpose**: تصحيح موثق لموقع أو حالة بعد تحقق إداري أو جرد.

**Fields**:
- `CorrectionId`.
- `ArtifactId`.
- `SourceType`: Reconciliation, AdministrativeCorrection.
- `SourceId`: مرجع اختياري للجرد أو سبب إداري.
- `CorrectionType`: LocationCorrection, MuseumNumberCorrection, StatusCorrection.
- `PreviousValueSummary`.
- `NewValueSummary`.
- `Reason`.
- `CorrectedAt`, `CorrectedBy`.

**Rules**:
- يجب إدخال سبب التصحيح.
- لا يحذف الحركة السابقة ولا يعيد كتابتها.
- لا يستخدم كبديل عن Return إذا كانت القطعة خارج المخزن ومعروف أنها عادت فعلياً.

### AuditEntry

**Purpose**: سجل تدقيق للعمليات الحساسة.

**Fields**:
- `AuditEntryId`.
- `ActorUserId`.
- `ActionName`.
- `ModuleName`.
- `EntityName`.
- `EntityId`.
- `OccurredAt`.
- `Summary`.
- `ChangeSummary`.

**Rules**:
- يسجل إنشاء/تعديل القطع والفئات والمواقع، الحركات، اعتماد الاستيراد، التصحيح، وتغييرات الصلاحيات.
- لا يحتوي بيانات غير لازمة للمرحلة الأولى.

## Current Location And Holder Rules

- داخل المخزن: `CurrentStatus = InStorage`، و`CurrentLocationId` = موقع الخزن، و`CurrentHolderType/Name` فارغة.
- لدى التوثيق/المختبر/المصور: `CurrentStatus = OutOfStorage`، و`CurrentHolderType/Name` = الجهة، ولا يوجد موقع خزن حالي في `CurrentLocationId`.
- في قاعة العرض: `CurrentStatus = OutOfStorage`، و`CurrentLocationId` = موقع العرض، و`CurrentHolderType/Name` = قاعة العرض.
- `LastKnownStorageLocationId` يحفظ آخر موقع خزن معروف عند خروج القطعة، ولا يمسح بسبب التسليم أو العرض.
- سجل الحركة هو المرجع التاريخي للتسليم والاستلام؛ الحالة الحالية تختصر آخر وضع تشغيلي فقط.

## State Diagrams

### Artifact Movement

```text
InStorage
  -> DeliverArtifacts
  -> OutOfStorage
  -> ReturnArtifacts
  -> InStorage
```

### Import Batch

```text
Uploaded
  -> Previewed
  -> ValidatedWithErrors
  -> ReadyToCommit
  -> Committed

Previewed or ValidatedWithErrors or ReadyToCommit
  -> Cancelled
```

### Reconciliation

```text
Draft
  -> Completed
  -> Reviewed
  -> optional DocumentedCorrection per confirmed conflict
```

## Integrity Constraints

- Unique `ArtifactCategory.CategoryCode` for the official required category code.
- Unique museum number derived from `ArtifactCategory.CategoryCode + Artifact.ItemNumber`.
- `Artifact.CategoryId` is an internal foreign key only and must not be used as part of the museum number.
- Foreign keys from all history records to `ArtifactId`.
- MovementRecord, ImportBatch after commit, ReconciliationResult, and DocumentedCorrection are append-only in normal staff flows.
- ConcurrencyToken on Artifact for delivery/return/correction.
- ConcurrencyToken on ImportBatch for explicit commit.
- Required audit actor for all write use cases.
- Current state must follow the documented `CurrentLocationId`, `CurrentHolderType/Name`, and `LastKnownStorageLocationId` rules.
