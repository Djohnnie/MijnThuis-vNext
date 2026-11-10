# Copilot instructions

## Architecture and stack
- Target .NET 11 with ASP.NET Core Minimal APIs and EF Core. Follow Clean/Onion Architecture, organized into vertical functional slices: domain rules must not depend on infrastructure, and infrastructure implements application abstractions.
- Use MediatR for application requests. Put each `QueryHandler` or `CommandHandler` in its own file with its corresponding `Query` or `Command` and `Response` objects. Keep API endpoints thin and dispatch requests through MediatR.
- Use Blazor WebAssembly with a backend for frontend (BFF). Keep credentials and sensitive tokens on the server, not in browser code.
- Use AutoMapper for object mapping and Spectre.Console for console applications.
- Use Azure SQL for persistence, including local testing; do not substitute a different database for local tests. Use Aspire to orchestrate local development and testing.

## C# conventions
- Never use expression-bodied methods, including local and extension methods; use block bodies instead.
- Use the newer C# `extension` block syntax for new extension members.

## Configuration and testing
- Provide configuration through `appsettings.Development.json`; do not commit this file or secrets. The current `.gitignore` does not exclude it, so add an appropriate ignore rule when introducing the file.
- Use xUnit v3 for tests, Moq for mocks, and Reqnroll for behavior scenarios. Add tests alongside behavior changes.
- The repository currently has no solution, projects, tests, or CI workflows. Do not assume build or test commands or project paths exist; document exact commands once the relevant projects are added.
