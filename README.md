# Security Lab

Security Lab is a collaborative student project for the Fundamentals of Information Security course. Its goal is to build an interactive laboratory platform as a modular monolith with well-defined module boundaries and contracts.

The repository contains a starter application, not a complete implementation: authentication, laboratories, authorization, evaluation, and persistent storage still need to be developed. The course project specification is available in [its original Serbian version](docs/spec/PROJECT_SPECIFICATION_OIB.pdf).

## Technology

The reference stack is React and TypeScript for the frontend, ASP.NET Core and C# for the API, PostgreSQL for data storage, and NUnit for tests. Alternative components may be used when justified and compatible with the domain rules and security boundaries.

## Getting started

Requirements: .NET SDK 10 and Node.js 22.12 or later (or Node.js 24 or later).

1. Start the API: `cd backend/Platform.Api && dotnet run --urls http://localhost:5080`
2. In another terminal, start the frontend: `cd frontend && npm ci && npm run dev`
3. Open `http://localhost:5173`. The API health endpoint is `http://localhost:5080/api/health`.

The optional local PostgreSQL service can be started with Docker Compose after copying `.env.example` to `.env` and setting the required credentials.

## Repository structure

- `backend/Platform.Api/` — API entry point.
- `frontend/` — React application.
- `docs/ARCHITECTURE.md` — conceptual architecture and module boundaries.
- `docs/DATA_FLOW.md` — platform-level data flow example.
- `docs/PROJECT_COMPONENTS.md` — project component assignments.
- `docs/templates/PROJECT_COMPONENT.md` — component documentation template.

See the course specification for the full requirements. Use short-lived branches and pull requests for changes; keep credentials and other secrets out of the repository.

## ACC-04 and ADM-08 KT1 implementation

The started coursework component adds concrete attempt ownership checks and course-scoped administrative delegation through the existing shared `Platform.Access` contract. It includes read/update of an own draft, version checks, two isolated courses, limited delegation/revocation, metadata-only audit, automated negative tests and a React demonstration.

Start the synthetic API with `dotnet run --project backend/Platform.Api/Platform.Api.csproj --launch-profile AccessKt1`, then run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/kt1-demo.ps1` in a second terminal. The frontend starts with the existing commands above. Docker is not needed for this in-memory demonstration.

Build/test: `dotnet test backend/SecurityLab.slnx --configuration Release`. The existing backend/frontend CI jobs are retained; the backend job additionally runs the KT1 tests and live demo and uploads their results. A new remote run must still be confirmed after pushing.

See [the KT1 checklist](docs/components/coursework/KT1.md), [startup and demonstration](docs/components/coursework/DEMO.md), [architecture and DFD diagrams](docs/components/coursework/diagrams), [domain/access matrix](docs/components/coursework/DOMAIN_MODEL.md), [threat model](docs/components/coursework/THREAT_MODEL.md), [provider contracts](docs/components/coursework/CONTRACTS.md), [backlog](docs/components/coursework/BACKLOG.md) and [actual local validation](docs/components/coursework/VALIDATION.md) and [external closure steps](docs/components/coursework/CLOSURE.md).

The synthetic login is enabled only in Development with `Kt1Demo:Enabled=true`; no production authentication, persistent database or actual external contract acceptance is claimed. Other teams' shared components are preserved.
