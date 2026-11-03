
# T20 - Laboratory Persistence

## Owner
Team 20 - Person 2

## Requirements
- LAB-02: Persistent storage of laboratory metadata.
- LAB-06: Persistent storage of laboratory prerequisites.

## Architecture
- Module: Platform.Authoring
- ORM: Entity Framework Core 10
- Database: PostgreSQL 17
- Schema: authoring
- Dependency injection: AddAuthoringModule()

## Database tables

### authoring.laboratories
- Id: UUID, primary key
- Title: varchar(120), required
- Goal: varchar(2000), required
- Difficulty: varchar(30), required
- Type: varchar(50), required
- Tags: PostgreSQL text[], required

### authoring.lab_prerequisites
- LaboratoryId: UUID, foreign key
- PrerequisiteLabId: UUID, foreign key
- Composite primary key: (LaboratoryId, PrerequisiteLabId)
- Foreign keys reference authoring.laboratories
- Self-references are prohibited
- Deletion of referenced laboratories is restricted

## Application contract

Interface:
Platform.Authoring.Application.ILaboratoryRepository

Data model:
Platform.Authoring.Application.Models.LaboratoryData

Supported operations:
- GetByIdAsync()
- AddAsync()
- UpdateAsync()
- AddPrerequisiteAsync()
- RemovePrerequisiteAsync()
- GetPrerequisiteIdsAsync()

Infrastructure implementation:
Platform.Authoring.Infrastructure.Persistence.LaboratoryRepository

## Database migrations

Migration:
InitialLaboratoryPersistence

EF Core context:
AuthoringDbContext

Migrations are stored under:
Infrastructure/Persistence/Migrations

## Integration tests

Tests use NUnit and a real PostgreSQL database.

Verified scenarios:
- PostgreSQL connection
- Laboratory creation and retrieval
- Laboratory metadata updates
- Stable laboratory identifiers
- Prerequisite persistence and removal
- Prevention of direct self-dependencies
- Rejection of duplicate prerequisites
- Rejection of nonexistent prerequisite references

## Responsibilities and limitations

Persistence is responsible for storing and retrieving data.

Business validation, authorization, cycle detection,
and student unlocking rules are outside this component.

Difficulty values are currently stored as strings.
Domain owners must define valid difficulty values.

Database constraint violations are surfaced as
EF Core exceptions. Callers must handle these errors.

After a failed SaveChangesAsync operation, a DbContext
may contain tracked entities in an invalid state.
Use a fresh context or explicitly recover its state.

## Handoff

Other team members should use ILaboratoryRepository
and LaboratoryData instead of accessing EF Core entities
or DbContext directly.

The repository is registered through AddAuthoringModule().

ConnectionStrings:Authoring must be configured locally.
Secrets must not be committed to Git.

The initial migration must be applied before using
the repository against a new PostgreSQL database.
