# Task Tracker API (.NET/C#)

A REST API with CRUD endpoints built with ASP.NET Core, Entity Framework Core, and SQLite.

## Install dependencies

All dependencies are declared in `TaskTrackerApi.csproj`. Install them with one command:

```powershell
dotnet restore
```

## Run locally

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), open a terminal in this folder, and run:

```powershell
dotnet restore
dotnet run --urls http://localhost:5192
```

Open `http://localhost:5192/swagger` to explore and test the API. The `tasks.db` SQLite database is created automatically on first launch.

## Run tests

```powershell
dotnet test TaskTrackerApi.Tests/TaskTrackerApi.Tests.csproj
```

## Endpoints

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/tasks` | List tasks; optional `?completed=true` filter |
| `GET` | `/api/tasks/stats` | Get total, completed, and open task counts |
| `GET` | `/api/tasks/{id}` | Get one task |
| `POST` | `/api/tasks` | Create a task |
| `PUT` | `/api/tasks/{id}` | Update a task |
| `PATCH` | `/api/tasks/{id}/complete?completed=true` | Set completion |
| `DELETE` | `/api/tasks/{id}` | Delete a task |

Invalid request bodies return `400 Bad Request` with field-specific validation messages. The API also emits structured informational logs when tasks are created, updated, completed, or deleted; logging levels and providers can be configured through `appsettings.json`.

Example create body:

```json
{
  "title": "Buy groceries",
  "description": "Milk, bread, and coffee"
}
```
