# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
