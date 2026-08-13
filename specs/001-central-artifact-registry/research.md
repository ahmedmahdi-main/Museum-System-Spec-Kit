# Phase 0 Research: السجل المركزي للقطع وإدارة المخزن وحركة التسليم والاستلام

## Decision: Modular Monolith with Clear Internal Modules

**Rationale**: المرحلة الأولى تحتاج سرعة تشغيل، مصدر حقيقة واحد، وقواعد حركة دقيقة. Modular Monolith يحقق حدوداً منطقية واضحة داخل تطبيق واحد وقاعدة واحدة، ويقلل مخاطر التشغيل والتوزيع مقارنة بالخدمات المتعددة.

**Alternatives considered**:
- Microservices: مرفوضة لأنها تزيد النشر والتشغيل والاتساق الموزع دون حاجة مثبتة.
- Event Bus/RabbitMQ: مرفوض حالياً لأن الحركة والتصحيح تحتاج اتساقاً فورياً داخل معاملة واحدة.
- CQRS/MediatR/Event Sourcing: مرفوضة حالياً لأنها تضيف نمطاً وتشغيلاً إضافياً لا يلزم لقواعد المرحلة الأولى.

## Decision: ASP.NET Core + Blazor Web App on .NET 10

**Rationale**: Blazor Web App يسمح بتطبيق ويب داخلي موحد بواجهة عربية RTL وقابلية مشاركة نماذج التحقق بين الواجهة والخدمات. اختيار .NET 10 يتبع قرار المشروع ويوفر منصة واحدة للواجهة والخلفية.

**Alternatives considered**:
- Frontend SPA منفصل: مرفوض حالياً لتقليل عدد المشاريع والنشر والتكامل.
- Razor Pages فقط: ممكن، لكن Blazor أوضح لواجهات اختيار القطع والعمليات الجماعية والمعاينات التفاعلية.

## Decision: Entity Framework Core with PostgreSQL via Npgsql Provider

**Rationale**: EF Core مناسب لدومين CRUD + حركات + قيود تكامل، وPostgreSQL يوفر قيود unique/foreign keys ومعاملات موثوقة. Npgsql هو provider EF Core الرسمي عملياً لـ PostgreSQL.

**Alternatives considered**:
- SQL Server: غير مطابق لقرار المستخدم.
- وصول SQL يدوي فقط: مرفوض كبداية لأنه يزيد كلفة التطوير والاختبار دون حاجة مثبتة.
- قواعد متعددة: مرفوضة لأنها تخالف مصدر الحقيقة الواحد في المرحلة الأولى.

## Decision: ASP.NET Core Identity with Roles and Permission Policies

**Rationale**: Identity يوفر أساس مستخدمين وأدوار، والسياسات المبنية على permissions تمنع تضخم الأدوار وتسمح بالتوسع لاحقاً دون تغيير كل الواجهات. المرحلة الأولى تحتاج صلاحيات واضحة للعرض، الإدارة، الحركة، الاستيراد، التصحيح، والتدقيق.

**Alternatives considered**:
- Roles فقط: أبسط لكنه أقل مرونة عند إضافة شعب لاحقة.
- نظام صلاحيات مخصص بالكامل: مرفوض لأنه يزيد المخاطر ويكرر وظيفة موجودة في المنصة.

## Decision: ClosedXML for Excel Preview/Validation/Commit

**Rationale**: ClosedXML مكتبة .NET معروفة لقراءة وكتابة ملفات Excel 2007+ مثل `.xlsx`، وترخيصها MIT، ما يجعلها ملائمة لبيئة مؤسسية دون افتراض ترخيص تجاري. ستستخدم فقط كـ adapter داخل Infrastructure، بينما تبقى قواعد الاستيراد في Application/Domain.

**Alternatives considered**:
- EPPlus: قوي ومستقر، لكن منذ الإصدار 5 يستخدم نموذج Polyform Noncommercial أو ترخيص تجاري، لذلك يحتاج قرار ترخيص قبل اعتماده.
- NPOI: خيار واسع لدعم صيغ Office متعددة، لكنه أوسع من حاجة المرحلة الأولى وقد يضيف تعقيداً لا يلزم إذا كان المطلوب `.xlsx` أساساً.
- Microsoft Office automation: مرفوض على الخادم لأنه يربط التشغيل بوجود Office ويزيد الهشاشة.

## Decision: Preview -> Validation -> Explicit Commit Import Lifecycle

**Rationale**: بيانات Excel تمثل الجرد الفعلي، لكنها قد تحتوي تكرارات أو مواقع غير معروفة أو أعمدة ناقصة. لا يجوز تعديل السجل المركزي حتى يرى الموظف المعاينة والأخطاء ويؤكد الاعتماد.

**Alternatives considered**:
- Import مباشر: مرفوض لأنه يخاطر بإدخال بيانات خاطئة في مصدر الحقيقة.
- Validation فقط دون Preview: مرفوض لأن الموظف يحتاج رؤية أثر الاستيراد قبل الاعتماد.

## Decision: Current State Stored Separately from Immutable Movement History

**Rationale**: الموظف يحتاج معرفة أين توجد القطعة الآن بسرعة، بينما الدستور يتطلب تاريخ حركة لا يحذف. لذلك يحتفظ Artifact بالحالة الحالية والموقع/الجهة الحالية وآخر موقع خزن، بينما تبقى MovementRecord سجلاً تاريخياً append-only.

**Alternatives considered**:
- اشتقاق الحالة الحالية دائماً من آخر حركة: دقيق نظرياً لكنه يزيد كلفة البحث ويعقد شاشات الموظف في المرحلة الأولى.
- تحديث الحالة فقط دون تاريخ: مرفوض لأنه يكسر تتبع العهدة والحركة.

## Decision: Bulk Operations Are Atomic

**Rationale**: عند التعامل مع مجموعة قطع، التسجيل الجزئي قد يربك الموظف ويخلق فجوة بين الورق والنظام. رفض العملية كاملة عند وجود قطعة غير مؤهلة يحافظ على سلامة السجل، مع عرض أسباب الرفض ليسهل تعديل الاختيار وإعادة المحاولة.

**Alternatives considered**:
- تنفيذ جزئي واستبعاد غير المؤهل تلقائياً: مرفوض لأنه قد يؤدي لتسليم غير مقصود لمجموعة ناقصة.
- سؤال الموظف لكل قطعة غير مؤهلة: مرفوض لأنه يبطئ مسار العمل.

## Decision: Documented Corrections for Reconciliation Conflicts

**Rationale**: الجرد يجب أن يكشف التعارضات، لكن الواقع قد يتطلب تصحيح موقع مؤكد. التصحيح الموثق يسمح بتحديث الحالة الحالية مع سبب واضح وسجل تاريخي دون حذف الحركات السابقة.

**Alternatives considered**:
- عرض التعارضات فقط: يحافظ على السلامة لكنه يترك السجل غير مطابق للواقع بعد التأكد.
- تعديل الموقع مباشرة من الجرد دون توثيق: مرفوض لأنه يخفي سبب التغيير ويضعف audit trail.

## Decision: Optimistic Concurrency for Sensitive State Changes

**Rationale**: تسليم/استلام/تصحيح نفس القطعة من مستخدمين مختلفين قد يسبب حالة حالية غير صحيحة. optimistic concurrency على Artifact وImportBatch يكفي كبداية دون قفل طويل أو تعقيد عمليات.

**Alternatives considered**:
- لا concurrency control: مرفوض بسبب خطر double-delivery أو commit مزدوج.
- pessimistic locking واسع: مرفوض كبداية لأنه يبطئ واجهة الموظف ويزيد التعقيد.

## Decision: Audit Trail at Application Boundary

**Rationale**: كل عملية حساسة يجب أن تسجل من نفذها ومتى وماذا تغير. audit في حدود التطبيق والتخزين يغطي السجل المركزي والحركات والاستيراد والتصحيحات والصلاحيات دون إدخال Event Sourcing.

**Alternatives considered**:
- الاعتماد على logs فقط: مرفوض لأنها ليست سجلاً تشغيلياً قابلاً للمراجعة.
- Event Sourcing: مرفوض لأنه أكبر من حاجة المرحلة الأولى.

## References

- ASP.NET Core Blazor documentation: https://learn.microsoft.com/en-us/aspnet/core/blazor/?view=aspnetcore-10.0
- ASP.NET Core role authorization documentation: https://learn.microsoft.com/en-us/aspnet/core/mvc/security/authorization/roles
- Npgsql EF Core provider documentation: https://www.npgsql.org/efcore/index.html
- ClosedXML repository/license: https://github.com/ClosedXML/ClosedXML
- EPPlus license overview: https://www.epplussoftware.com/en/LicenseOverview
