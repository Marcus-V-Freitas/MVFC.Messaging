# Contributing to MVFC.Messaging

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download) or later
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) running locally
- Git

## Running locally

```sh
git clone https://github.com/Marcus-V-Freitas/MVFC.Messaging.git
cd MVFC.Messaging
dotnet restore MVFC.Messaging.slnx
dotnet build MVFC.Messaging.slnx --configuration Release
```

## Running tests

The tests require Docker to be running.

```sh
dotnet test tests/MVFC.Messaging.Tests/MVFC.Messaging.Tests.csproj --configuration Release
```

## Adding a new helper

1. Create a new folder under `src/MVFC.Messaging.{ServiceName}/`
2. Follow the structure of an existing helper (e.g. `MVFC.Messaging.RabbitMQ`)
3. Add the new project to `MVFC.Messaging.slnx`
4. Add the package version to `Directory.Packages.props`
5. Add integration tests in `tests/MVFC.Messaging.Tests/`
6. Update `README.md` and `README.pt-BR.md` with the new package entry

## Branch naming

- `feat/` — new feature or helper
- `fix/` — bug fix
- `chore/` — dependency update or maintenance
- `docs/` — documentation only
- `test/` — tests only
- `refactor/` — no feature change, no bug fix

Example: `feat/add-rabbitmq-helper`

## Commit convention

This project follows [Conventional Commits](https://www.conventionalcommits.org/):

- `feat: add rabbitmq helper`
- `fix: fix rabbitmq connection timeout`
- `docs: update README badges`
- `chore: bump RabbitMQ to 7.0.0`
- `test: add rabbitmq integration tests`
- `refactor: simplify rabbitmq commander setup`

## Pull Request process

1. Fork and create your branch from `main`
2. Make your changes and ensure all tests pass locally
3. Open a PR against `main` and fill in the PR template
4. Wait for the CI to pass
5. A maintainer will review and merge
