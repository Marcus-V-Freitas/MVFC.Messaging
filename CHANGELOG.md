# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.0.1] - 2026-04-03

### Added
- Enhanced `CI` workflow with an automated cleanup job that deletes GitHub Releases and tags using the `gh` CLI in case of pipeline failure.

### Fixed
- Updated `LocalStack` test container image to version `3` to ensure stability in AWS integration tests.

## [3.0.0] - 2026-04-03

### Breaking Changes
- Moved `RabbitMqPublisher` and `RabbitMqConsumer` factories to non-generic classes to resolve `CA1000`.

### Added
- Integrated `MinVer` for versioning and `Directory.Packages.props` for Central Package Management.
- Added `build.cake` for automated build, test, and coverage reporting using `dotnet-coverage`.
- Improved `CI` workflow with `Codecov v5` and automated GitHub Release notes.

### Changed
- Converted all private static helpers on generic classes to instance methods for project-wide consistency.
- Enabled `TreatWarningsAsErrors` and strict code analysis in `Directory.Build.props`.

## [2.0.4] - 2026-03-21

### Changed
- Corrected Codecov badge URLs in all README files to use the `main` branch.

## [2.0.3] - 2026-03-21

### Changed
- Renamed coverage verification step in CI for better clarity.

### Added
- Configured CI to run on all pushes to the `main` branch.

## [2.0.2] - 2026-03-21

### Changed
- Configured Codecov to fail CI on errors and added code coverage status thresholds.

## [2.0.1] - 2026-03-20

### Added
- Added multi-target support for `net9.0` and `net10.0` across all 9 provider packages and the Core library.

### Fixed
- Fixed `AmazonClientException` in CI by passing `AnonymousAWSCredentials` to `AmazonSQSClient` in integration tests, since LocalStack does not require real AWS credentials.

## [2.0.0] - 2026-03-19

### Added
- Created comprehensive, bilingual (en-us and pt-br) README files for all projects.
- Added detailed configuration and usage examples for all 9 providers:
  - AWS (SQS)
  - Azure (Service Bus)
  - Confluent (Kafka)
  - GCP (Pub/Sub)
  - InMemory (Testing)
  - Nats.IO
  - RabbitMQ
  - StackExchange (Redis Streams)
- Added core documentation for `IMessagePublisher<T>` and `IMessageConsumer<T>` in `MVFC.Messaging.Core`.
- Included NuGet badges (Version, Downloads) and CI/Coverage status in all READMEs.

### Changed
- Standardized README structure across the entire repository.
- Improved main repository README with provider overview and unified quick start.

## [1.0.2] - 2025-12-19

### Fixed
- Removed duplicate license file.
- Updated links to providers in the main README.

## [1.0.1] - 2025-12-19

### Added
- Added `.gitattributes` and `.gitignore`.
- Initial repository structure with Core and Provider projects.

## [1.0.0] - 2025-12-19

### Added
- Initial release of the MVFC.Messaging suite.
- Base abstractions for Publishers and Consumers.
- Initial implementations for AWS, Azure, Confluent, GCP, InMemory, Nats.IO, RabbitMQ, and StackExchange.

[3.0.1]: https://github.com/Marcus-V-Freitas/MVFC.Messaging/compare/v3.0.0...v3.0.1
[3.0.0]: https://github.com/Marcus-V-Freitas/MVFC.Messaging/compare/v2.0.4...v3.0.0
[2.0.4]: https://github.com/Marcus-V-Freitas/MVFC.Messaging/compare/v2.0.3...v2.0.4
[2.0.3]: https://github.com/Marcus-V-Freitas/MVFC.Messaging/compare/v2.0.2...v2.0.3
[2.0.2]: https://github.com/Marcus-V-Freitas/MVFC.Messaging/compare/v2.0.1...v2.0.2
[2.0.1]: https://github.com/Marcus-V-Freitas/MVFC.Messaging/compare/v2.0.0...v2.0.1
[2.0.0]: https://github.com/Marcus-V-Freitas/MVFC.Messaging/compare/v1.0.2...v2.0.0
[1.0.2]: https://github.com/Marcus-V-Freitas/MVFC.Messaging/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/Marcus-V-Freitas/MVFC.Messaging/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/Marcus-V-Freitas/MVFC.Messaging/releases/tag/v1.0.0
