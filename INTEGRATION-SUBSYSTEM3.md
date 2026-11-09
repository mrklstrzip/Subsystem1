# Subsystem 3 integration into Subsystem 1

Subsystem 1 remains the host application (ASP.NET MVC 5 / .NET Framework 4.8) and continues to use `Sia_set1_db`. No second login system or database was added. Existing Dean schedule approval, defense schedule, Capstone Coordinator schedule generation, conflict handling, and profile flows remain intact.

## Added to the existing role areas

### Dean
- Added **Research Oversight** navigation and `OversightController`.
- Added read-only repository/records/cohort/exceptions/performance views using the existing `student_titles` collection.
- The page exposes title, authors/manager, program, adviser, abstract, keywords, status, and timestamps when those fields exist in the current documents.
- It does not mutate manuscript status or make clearance decisions. Existing Subsystem 1 schedule approval remains authoritative.

### Capstone Coordinator
- Added **Topic Screening** navigation and a MongoDB-backed screening workflow.
- Standard screening compares a proposed title/description against existing `student_titles` documents using normalized token overlap (Jaccard similarity), returns the top ten matches, and persists results.
- Added assessment-case creation/update, case listing, and audit history. New collections in the same `Sia_set1_db`: `topic_screenings`, `topic_cases`, and `capstone_audit`.
- AI/Gemini comparison from Subsystem 3 is intentionally not enabled: the archive does not include a configured and secured API key/secret-management path. Similarity scores are advisory and are not plagiarism determinations.

## Important schema notes

Subsystem 3's original integration source expects a separate `ProjectGroup`/workflow schema (`groups`, repository groups, history, requirements and file records). Subsystem 1's supplied project instead uses `student_titles` and `defense_schedules`. The integration therefore maps to the collections actually used by Subsystem 1 instead of pretending those schemas are interchangeable. The Dean oversight page is read-only and uses only fields found on existing title documents. To reproduce Subsystem 3's full filing-cycle metrics, historical audit timeline, manuscript file reader, and integrity checks, the source database must first contain the corresponding workflow/history/file data or a deliberate migration.

## Build / run

Open `Subsystem1.slnx` / the existing Subsystem1 project in Visual Studio 2022 with the .NET Framework 4.8 developer pack and restore the NuGet packages. Confirm the MongoDB connection string is configured for your environment before running. The execution environment used to prepare this merge did not have MSBuild/.NET Framework tooling, so a Windows Visual Studio build and live MongoDB smoke test are still required.

## Security

The uploaded `connectionStrings.config` contains a credential-bearing MongoDB connection string. Rotate that database credential if this archive has been shared outside the intended team, and move secrets to a protected environment-specific configuration before deployment.
