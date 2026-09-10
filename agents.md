# Agent instructions

Before finishing an agent session, ensure the checks defined in `.github/workflows/ci.yml` pass. Do not treat a known or pre-existing CI failure as a successful validation.

- Use the .NET SDK specified by `src/global.json`.
- Run `dotnet format ActionsImporter.sln --verify-no-changes` from `src`.
- Run the workflow's restore, build, unit-test, license-validation, and cross-platform publish checks.
- After pushing changes, inspect the PR's CI results and investigate any failed jobs before finishing. If approval or an environment limitation prevents validation, report the blocker explicitly rather than claiming the checks passed.
- Keep generated build outputs and test results out of commits.
