# ProductsCRUD API

Product management API where every database operation goes through a stored procedure. Includes a bulk insert endpoint that handles anything from three rows to a couple hundred thousand, and a GitHub lookup as the external API integration.

Built on .NET 10, SQL Server, and Dapper.

## Getting started

Requirements: .NET SDK 10 and SQL Server — developed against SQL Server Express with Windows Authentication.

```bash
git clone https://github.com/Juanpaaab/ProductsCRUD_API.git
cd ProductsCRUD_API
```

### 1. Database

Run `database/01_setup.sql` against your SQL Server instance:

```bash
sqlcmd -S localhost\SQLEXPRESS -E -C -i database/01_setup.sql
```

It's safe to run more than once — every object is created only if missing. From SSMS or DBeaver, execute the whole file at once rather than line by line, since it relies on `GO` to split batches.

What it creates: the `ProductsCRUD` database, `Products`, a `Products_Staging` table used by the large-batch insert path, the `ProductTableType` table type, and seven stored procedures.

### 2. Connection string

`appsettings.json` assumes SQL Server Express on the local machine with Windows Authentication:

```
Server=localhost\SQLEXPRESS;Database=ProductsCRUD;Trusted_Connection=True;TrustServerCertificate=True;
```

Change it if your instance name differs or you're using SQL Authentication.

### 3. Run

```bash
dotnet run
```

The API comes up on `http://localhost:5146`. Open `http://localhost:5146/scalar/v1` for the Scalar UI, a browser client for every endpoint — it's wired up in development only.

That's the `http` launch profile, which is the one `dotnet run` picks by default. For HTTPS on port 7031, ask for the other profile:

```bash
dotnet run --launch-profile https
```

Either way, `ProductsCRUD_API.http` in the repo root has prewritten requests for everything, including the failure cases — 404s, a negative price, an empty batch.

## API reference

| Method | Route | Returns |
|---|---|---|
| `GET` | `/api/products` | Page of active products, newest first |
| `GET` | `/api/products/{id}` | One product, or 404 |
| `POST` | `/api/products` | 201 with the created product |
| `PUT` | `/api/products/{id}` | 200 with the updated product, or 404 |
| `DELETE` | `/api/products/{id}` | 204, or 404 if already gone |
| `POST` | `/api/products/bulk` | 201 with insert count and strategy used |
| `GET` | `/api/github/{username}` | GitHub profile summary, or 404 |

Listing takes `page` and `pageSize` as query parameters, defaulting to `1` and `20`. `pageSize` is clamped to 100 — a client asking for a million rows gets a hundred.

## Bulk inserts

`POST /api/products/bulk` takes a JSON array and picks how to insert it based on how much you sent:

| Batch size | Path | How it works |
|---|---|---|
| ≤ 50,000 | Table-valued parameter | `DataTable` → `sp_Product_BulkCreate`, one round trip |
| > 50,000 | `SqlBulkCopy` | Stream into staging tagged with a `BatchId`, then a SP moves that batch into `Products` |

Both are transactional. The `SqlBulkCopy` path shares one connection and one transaction across the copy and the stored procedure call, so if the SP throws, the staged rows roll back with it and nothing is orphaned. The per-request `BatchId` is what keeps two concurrent uploads from picking up each other's rows out of the shared staging table.

The response says which path ran, which makes the threshold easy to observe:

```json
{ "inserted": 60000, "strategy": "BulkCopy" }
```

`SqlBulkCopyThreshold` and `MaxBatchSize` are both in `appsettings.json`. The 50,000 figure is a starting point, not a measurement — the real crossover depends on the server and the row width.

`ProductsCRUD_API.http` includes a small batch that exercises the table-valued parameter path. Crossing the 50,000 threshold needs a generated payload — or you can lower `SqlBulkCopyThreshold` in `appsettings.json` and watch the `SqlBulkCopy` path run against a batch small enough to write by hand.

## Why it's built this way

**Dapper over EF Core.** With every query behind a stored procedure, EF Core's selling points — LINQ, change tracking, migrations — don't apply. Dapper maps result sets with minimal overhead and hands parameters straight to the SP. DTOs go in as-is and Dapper reflects over their properties to build `@Name`, `@Description`, `@Price`, so there's no manual parameter code outside the bulk path, where a TVP forces it.

**No service layer.** Controllers call the repository directly. A service tier here would forward calls and nothing else.

**Create and update return the row from SQL.** `sp_Product_Create` selects the inserted record via `SCOPE_IDENTITY()` instead of using an `OUTPUT` parameter, and `sp_Product_Update` returns the row it just modified — empty if the id doesn't exist. The controller reads a `null` as a 404. This keeps everything to one round trip and avoids the check-then-act race that a preliminary `GetById` would introduce, since the lookup and the write happen inside the same statement.

**Soft deletes.** `IsActive` flips to `0` and reads filter on it. History stays.

**UTC timestamps, `DATETIMEOFFSET`, set by the database.** `CreatedDate` defaults to `SYSUTCDATETIME()`. Nothing in C# ever writes it, so there's one clock rather than two drifting ones, and no local-offset assumption baked into stored data. `DATETIMEOFFSET` maps to `DateTimeOffset` and serializes with the offset attached, so a consumer never has to guess what timezone a timestamp is in.

**Errors go through middleware.** `ProblemDetails` out, exception detail to the log. Constraint violations map to 400; everything else is a 500.

## Gotchas worth knowing

Three things in here would be easy to break without noticing:

*Table-valued parameters match by position.* The `DataTable` built in `ProductRepository` must list columns in the same order as `dbo.ProductTableType` declares them. Reorder one side and SQL Server won't complain — it'll write values into the wrong columns.

*`SqlBulkCopy` needs explicit mappings.* Its default is also positional, and the staging table carries a `BatchId` the destination ordering doesn't line up with, so the mappings are declared by name deliberately.

*`SYSUTCDATETIME()` is a per-statement value.* Insert 60,000 rows in one statement and all 60,000 share a timestamp. That's why the listing orders by `CreatedDate DESC, Id DESC` — without the id as tiebreaker, paging through a bulk-loaded set would skip and repeat rows.

## A note on the OpenAPI dependency

.NET's Web API template dropped Swashbuckle in .NET 9, so this project uses Scalar over the built-in `AddOpenApi()`.

The template pulls `Microsoft.OpenApi 2.0.0`, which has a high-severity advisory against it (GHSA-v5pm-xwqc-g5wc — circular `$ref` chains blow the stack during parsing). Upgrading to 3.x seems like the fix but breaks the build: `AddOpenApi`'s source generator targets the 2.x object model, and 3.x made `IOpenApiMediaType.Example` read-only, so the generated file no longer compiles. The advisory is patched on both lines, so the package is pinned to `2.11.0` — current on the 2.x branch, patched, and compatible with the generator.

## Not included

Authentication, automated tests, health checks, retry with backoff on the GitHub call, and a streaming ingest path for batches in the millions, where holding the whole array in memory stops being reasonable.
