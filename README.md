# RomaERP

نظام تخطيط موارد المؤسسات (ERP) — يبدأ بموديولي **المحاسبة المالية** و**الموارد البشرية**، مبني بـ Clean Architecture.

## التقنيات

- **Backend:** ASP.NET Core 8 Web API (C#), Clean Architecture (Domain / Application / Infrastructure / API)
- **Database:** SQL Server + Entity Framework Core
- **Auth:** ASP.NET Core Identity + JWT
- **Frontend:** React + TypeScript + Vite

## هيكل المشروع

```
src/
  RomaERP.Domain          # الكيانات والـ Enums (لا تعتمد على أي مكتبة خارجية)
  RomaERP.Application     # DTOs + منطق العمل (Services) + الـ Interfaces
  RomaERP.Infrastructure  # EF Core DbContext, Migrations, Identity, JWT
  RomaERP.API             # Controllers, Program.cs, Swagger
tests/
  RomaERP.UnitTests
frontend/                 # React + TypeScript
```

## الموديولات الحالية

### المحاسبة المالية
- شجرة حسابات كاملة (أصول / خصوم / حقوق ملكية / إيرادات / مصروفات) مبنية على أساس الاستحقاق المحاسبي، بما فيها حسابات **المصروفات/الإيرادات المقدمة والمستحقة**.
- سنوات وفترات محاسبية.
- قيود يومية بنظام القيد المزدوج (Double-Entry) مع التحقق من التوازن، والترحيل، وعكس القيود.
- ميزان المراجعة.

### الموارد البشرية
- الأقسام والوظائف والموظفون.
- عناصر الأجر (بدلات / استقطاعات) قابلة للربط بحسابات دليل الحسابات.
- دورات رواتب: إنشاء وحساب تلقائي → اعتماد → ترحيل، وينتج عنها قيد محاسبي تلقائي (مصروف مرتبات مقابل مرتبات مستحقة).

## التشغيل محليًا

### المتطلبات
- .NET 8 SDK
- SQL Server (أو container عبر Docker)
- Node.js 18+

### 1. قاعدة البيانات

```bash
docker run -d --name romaerp-sql -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong@Passw0rd" -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
```

عدّل `ConnectionStrings:DefaultConnection` في `src/RomaERP.API/appsettings.json` عند الحاجة.

### 2. الـ Backend

```bash
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/RomaERP.Infrastructure --startup-project src/RomaERP.API
dotnet run --project src/RomaERP.API
```

عند أول تشغيل (Development) يتم تلقائيًا زرع (Seed):
- دليل الحسابات الأساسي.
- السنة المالية الحالية وفتراتها الشهرية.
- مستخدم مدير: `admin@romaerp.local` / `Admin@12345`.

الـ API متاح على `https://localhost:xxxx/swagger`.

### 3. الـ Frontend

```bash
cd frontend
npm install
npm run dev
```

عدّل `VITE_API_URL` في `frontend/.env` إذا لزم.

## الخطوات القادمة
- موديول المخزون وربطه بتكلفة البضاعة المباعة ومراكز التكلفة.
- تقارير مالية إضافية (قائمة الدخل، الميزانية العمومية).
- صلاحيات أدق حسب الدور.
