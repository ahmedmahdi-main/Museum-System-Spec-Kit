# Application Use Case Contracts

هذه العقود تصف واجهات التطبيق المنطقية بين Blazor UI وطبقة Application. لا تحدد endpoints أو controllers أو بروتوكولاً خارجياً، لأن المرحلة الحالية تطبيق واحد داخل Modular Monolith.

## Shared Result Shape

كل use case يعيد نتيجة مفهومة للواجهة:

- `Succeeded`: نعم/لا.
- `Messages`: رسائل قصيرة قابلة للتصرف بالعربية.
- `ValidationIssues`: قائمة أخطاء مرتبطة بحقل أو قطعة أو صف Excel.
- `ConcurrencyConflict`: نعم/لا عند تغيّر الحالة أثناء العملية.
- `AuditReference`: مرجع اختياري لعملية محفوظة.

## Artifact Registry

### CreateCategory

**Input**:
- اسم الفئة بالعربية.
- رمز/معرف عرض اختياري.
- وصف اختياري.

**Success**:
- إنشاء فئة نشطة قابلة للاستخدام.

**Validation**:
- الاسم مطلوب.
- لا يسمح بتكرار رمز الفئة إن استخدم.

**Authorization**: `Artifacts.Manage`

### CreateArtifact

**Input**:
- CategoryId.
- ItemNumber.
- BasicDescription.
- InitialLocationId.

**Success**:
- إنشاء ArtifactId داخلي ثابت.
- عرض MuseumNumber للموظف.
- الحالة الأولية: داخل المخزن.
- LastKnownStorageLocationId = InitialLocationId.

**Validation**:
- الفئة نشطة.
- الموقع صالح ونشط.
- `(CategoryId, ItemNumber)` غير مستخدم.

**Authorization**: `Artifacts.Manage`

### SearchArtifacts

**Input**:
- نص بحث: رقم، رقم جزئي، فئة، أو كلمات وصفية.
- فلاتر اختيارية: الحالة، الموقع، الجهة الحالية.

**Success**:
- قائمة مختصرة تعرض الرقم المتحفي، الوصف، الحالة، الموقع/الجهة، وآخر موقع خزن.

**Authorization**: `Artifacts.View`

## Inventory Locations

### CreateLocation

**Input**:
- الاسم العربي.
- النوع: مخزن أو قاعة عرض.
- الموقع الأب اختياري.

**Success**:
- إنشاء موقع نشط قابل للاختيار.

**Validation**:
- الاسم مطلوب.
- النوع مطلوب.

**Authorization**: `Locations.Manage`

### DisableLocationForNewUse

**Input**:
- LocationId.
- سبب التعطيل.

**Success**:
- الموقع لا يظهر كاختيار جديد، لكنه يبقى في التاريخ.

**Authorization**: `Locations.Manage`

## Movements

### PreviewDeliveryEligibility

**Input**:
- ArtifactIds.
- RecipientType.

**Success**:
- لكل قطعة: مؤهلة/غير مؤهلة وسبب مختصر.

**Validation**:
- كل القطع يجب أن تكون داخل المخزن.
- الجهة المستلمة ضمن القائمة المدعومة.

**Authorization**: `Movements.Deliver`

### DeliverArtifacts

**Input**:
- ArtifactIds.
- RecipientType.
- RecipientName.
- Purpose.
- Note اختياري.

**Success**:
- إنشاء MovementGroup واحد.
- إنشاء MovementRecord لكل قطعة.
- تحديث حالة كل قطعة إلى خارج المخزن.
- حفظ الجهة الحالية واسم المستلم والغرض.

**Failure**:
- إذا كانت أي قطعة غير مؤهلة، تفشل العملية كاملة ولا تتغير أي قطعة.
- إذا حدث concurrency conflict، تعرض الواجهة أن حالة قطعة تغيرت ويجب إعادة المراجعة.

**Authorization**: `Movements.Deliver`

### PreviewReturnEligibility

**Input**:
- ArtifactIds.
- ReturnLocationId.

**Success**:
- لكل قطعة: مؤهلة/غير مؤهلة وسبب مختصر.

**Validation**:
- كل القطع يجب أن تكون خارج المخزن.
- موقع العودة موقع خزن صالح ونشط.

**Authorization**: `Movements.Return`

### ReturnArtifacts

**Input**:
- ArtifactIds.
- ReturnLocationId.
- Note اختياري.

**Success**:
- إنشاء MovementGroup واحد.
- إنشاء MovementRecord لكل قطعة.
- تحديث الحالة إلى داخل المخزن.
- تحديث CurrentLocationId وLastKnownStorageLocationId.

**Failure**:
- تفشل العملية كاملة إذا وجدت قطعة غير مؤهلة.

**Authorization**: `Movements.Return`

## Excel Import

### UploadImportFileForPreview

**Input**:
- ملف Excel.
- نوع مصدر الجرد: مخزن أو قاعة عرض.
- موقع المصدر عند الحاجة.

**Success**:
- إنشاء ImportBatch بحالة Previewed.
- قراءة الصفوف وعرضها دون إنشاء قطع.

**Validation**:
- صيغة الملف مدعومة.
- وجود الأعمدة المطلوبة أو إمكانية ربطها قبل التحقق.

**Authorization**: `Imports.Preview`

### ValidateImportBatch

**Input**:
- ImportBatchId.
- اختيار/تأكيد mapping للأعمدة إن احتاج الملف.

**Success**:
- تحديث حالة كل صف إلى Accepted أو Rejected أو NeedsReview.
- عرض ملخص الأخطاء.

**Validation**:
- الفئة قابلة للمطابقة أو الإنشاء حسب سياسة tasks.
- رقم القطعة موجود وغير مكرر داخل الفئة.
- الموقع معروف وصالح.

**Authorization**: `Imports.Preview`

### CommitImportBatch

**Input**:
- ImportBatchId.
- تأكيد صريح من الموظف.

**Success**:
- اعتماد الصفوف المقبولة فقط.
- إنشاء/ربط السجلات وفق نتائج validation.
- وضع الدفعة في حالة Committed.

**Failure**:
- لا يقبل commit لدفعة غير جاهزة.
- لا يقبل commit مرتين.
- concurrency conflict يعيد المستخدم للمراجعة.

**Authorization**: `Imports.Commit`

## Reconciliation

### StartReconciliationSession

**Input**:
- LocationId.
- Note اختياري.

**Success**:
- إنشاء جلسة جرد Draft للموقع.

**Authorization**: `Reconciliation.Manage`

### RecordReconciliationItems

**Input**:
- ReconciliationSessionId.
- قائمة أرقام متحفية مرصودة أو ArtifactIds.

**Success**:
- تصنيف النتائج إلى Matched, Missing, Extra, Conflict, NeedsReview.

**Authorization**: `Reconciliation.Manage`

### CreateDocumentedCorrection

**Input**:
- ArtifactId.
- Source reconciliation result إن وجد.
- CorrectionType.
- New value.
- Reason.

**Success**:
- إنشاء DocumentedCorrection.
- تحديث الحالة/الموقع الحالي عند انطباقه.
- حفظ audit entry.

**Validation**:
- السبب مطلوب.
- التصحيح لا يحذف ولا يعدل MovementRecord سابق.

**Authorization**: `Corrections.Create`
