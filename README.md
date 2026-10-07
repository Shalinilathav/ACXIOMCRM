# AcxiomCRM

A role-based CRM web application built with **ASP.NET Core 8 MVC**, covering the complete customer-sales lifecycle from lead capture through follow-up and opportunity management — with authentication, authorization, audit logging, REST APIs, and reporting.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 8 MVC |
| ORM | Entity Framework Core 8 |
| Database | SQLite |
| Auth | ASP.NET Core Identity |
| UI | Bootstrap 5 + Chart.js |
| Icons | Bootstrap Icons + Font Awesome |

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

---

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/Shalinilathav/ACXIOMCRM.git
cd ACXIOMCRM
```

### 2. Run the application

```bash
dotnet run --urls "http://localhost:5000"
```

**If .NET is not in your PATH:**
```powershell
# Windows (PowerShell)
$env:DOTNET_ROOT = "C:\path\to\.dotnet"
$env:PATH = "C:\path\to\.dotnet;$env:PATH"
dotnet run --project AcxiomCRM.csproj --urls "http://localhost:5000"

# Linux/macOS
export DOTNET_ROOT=/path/to/.dotnet
export PATH=$DOTNET_ROOT:$PATH
dotnet run --project AcxiomCRM.csproj --urls "http://localhost:5000"
```

The database is created and seed users are created automatically on first run.

### 3. Open in browser

Navigate to:
```
http://localhost:5000
```

The app will automatically:
- ✅ Create `AcxiomCRM.db` SQLite database
- ✅ Run EF Core migrations
- ✅ Seed default users (Admin, Manager, Sales Executive)
- ✅ Create required roles (Admin, Manager, SalesExecutive)

### 4. Stop the application

Press `Ctrl+C` in the terminal where the app is running.

---

## Default Login Credentials

| Role | Email | Password |
|---|---|---|
| Admin | admin@acxiomcrm.com | Admin@1234 |
| Manager | manager@acxiomcrm.com | Manager@1234 |
| Sales Executive | sales@acxiomcrm.com | Sales@1234 |

---

## Module Structure

```
AcxiomCRM
│
├── Authentication          Login, Register, Logout, Lockout
├── Dashboard               Role-based KPIs + Chart.js charts
├── Customer Management     CRUD, search, duplicate check
├── Lead Management         CRUD, status workflow, lead conversion
├── Opportunity Management  CRUD, pipeline stages, weighted amount
├── Follow-Up Management    Schedule, complete, overdue tracking
├── Activity Management     Calls, Meetings, Emails, Tasks
├── User & Role Management  Create users, assign roles, lock/unlock
├── Audit Log               Append-only security & business event log
├── REST API                Customers, Leads, Opportunities, Follow-Ups
└── Reports                 8 reports with filters and role scoping
```

---

## Role-Based Access

| Module | Admin | Manager | Sales Executive |
|---|---|---|---|
| Dashboard | Full | Team | Own/Assigned |
| Customers | Full | Team | Own/Assigned |
| Leads | Full | Team | Own/Assigned |
| Opportunities | Full | Team | Own/Assigned |
| Follow-Ups | Full | Team | Own/Assigned |
| Activities | Full | Team | Own/Assigned |
| Users & Roles | Full | — | — |
| Audit Log | Full | Limited | — |
| Reports | All | Team | Own |

---

## REST API Endpoints

All API endpoints require authentication (`[Authorize]`).

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/customers` | List customers |
| GET | `/api/customers/{id}` | Get customer by ID |
| POST | `/api/customers` | Create customer |
| PUT | `/api/customers/{id}` | Update customer |
| DELETE | `/api/customers/{id}` | Delete customer |
| GET | `/api/leads` | List leads |
| POST | `/api/leads` | Create lead |
| GET | `/api/opportunities` | List opportunities |
| POST | `/api/opportunities` | Create opportunity |
| GET | `/api/followups` | List follow-ups |
| POST | `/api/followups` | Create follow-up |

---

## Reports

| Report | URL |
|---|---|
| Customer Report | `/Reports/CustomerReport` |
| Lead Report | `/Reports/LeadReport` |
| Opportunity Report | `/Reports/OpportunityReport` |
| Follow-Up Report | `/Reports/FollowUpReport` |
| Pipeline Report | `/Reports/PipelineReport` |
| Sales & Conversion Report | `/Reports/SalesConversionReport` |
| User Activity Report | `/Reports/UserActivityReport` |
| Audit Report | `/Reports/AuditReport` |

---

## Validation

- **Client-side**: Required, Email, Phone, Length, Date, Numeric (jQuery Unobtrusive Validation)
- **Server-side**: ModelState + business rule enforcement on every POST
- **Business rules**:
  - Opportunity Amount must be > 0
  - Probability must be 0–100
  - Expected Close Date cannot be in the past
  - Follow-Up Date cannot be before today
  - Customer Email and Phone must be unique

---

## Security Features

- Passwords hashed via ASP.NET Core Identity (BCrypt/PBKDF2)
- Password policy: min 8 chars, uppercase, lowercase, digit required
- Account lockout after **5** failed attempts for **15 minutes**
- Anti-forgery tokens on all state-changing forms
- Role-based authorization enforced server-side (not just UI hiding)
- Audit logging for all authentication and CRUD events

---

## Project Structure

```
AcxiomCRM/
├── Controllers/
│   ├── Api/                    REST API controllers
│   ├── AccountController.cs
│   ├── DashboardController.cs
│   ├── CustomersController.cs
│   ├── LeadsController.cs
│   ├── OpportunitiesController.cs
│   ├── FollowUpsController.cs
│   ├── ActivitiesController.cs
│   ├── UsersController.cs
│   ├── AuditLogController.cs
│   └── ReportsController.cs
├── Data/
│   └── ApplicationDbContext.cs
├── DTOs/                       API Data Transfer Objects
├── Models/
│   ├── ApplicationUser.cs
│   ├── Customer.cs
│   ├── Lead.cs
│   ├── Opportunity.cs
│   ├── FollowUp.cs
│   ├── Activity.cs
│   ├── AuditLog.cs
│   └── ViewModels/
├── Services/
│   ├── AuditService.cs
│   ├── DashboardService.cs
│   └── CodeGeneratorService.cs
├── Views/
│   ├── Shared/                 Layout, partials
│   ├── Dashboard/
│   ├── Customers/
│   ├── Leads/
│   ├── Opportunities/
│   ├── FollowUps/
│   ├── Activities/
│   ├── Users/
│   ├── AuditLog/
│   └── Reports/
├── Program.cs
├── appsettings.json
└── AcxiomCRM.db               SQLite database (auto-created)
```

---

## Database

SQLite database is auto-created as `AcxiomCRM.db` on first run. No manual migration steps required — migrations run automatically at startup.

---

## Development Notes

### Project Structure
This is an ASP.NET Core 8 MVC application with:
- **Authentication**: ASP.NET Core Identity with role-based access (Admin, Manager, Sales Executive)
- **Database**: SQLite with Entity Framework Core migrations
- **API**: RESTful endpoints for programmatic access
- **UI**: Server-side rendered Razor views with Bootstrap 5

### Running Migrations Manually
If you need to add new migrations:

```bash
# Add a migration
dotnet ef migrations add MigrationName

# Update database
dotnet ef database update
```

### Building for Production

```bash
# Build in Release mode
dotnet build -c Release

# Publish self-contained application
dotnet publish -c Release -o ./publish
```

---

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/your-feature`)
3. Commit your changes (`git commit -m 'Add some feature'`)
4. Push to the branch (`git push origin feature/your-feature`)
5. Open a Pull Request

---

## License

This project is for educational/demonstration purposes.
