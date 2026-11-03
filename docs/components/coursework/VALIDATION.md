# Validation after merging current main into PR #36

Date: **2026-11-03**. PR head before integration: `3f535351b3a3289fde78c6fa7b90580477863634`. Integrated main: `8420f3b6710dda51b483b3743691996951370c88`. The integration preserves all changes on that main, including merges after PR #33.

## Actual local checks

The Release solution build/test completed on Linux using .NET SDK 10.0.403. The selected solution includes every pre-existing main project and adds the KT1 tests once.

| Test project | Passed / total | Failed |
|---|---|---|
| platform.audit.tests | 71/71 | 0 |
| platform.authoring.tests | 55/55 | 0 |
| platform.kt1.tests | 63/63 | 0 |
| platform.tests | 42/42 | 0 |
| **Whole solution** | **231/231** | **0** |

The ACC-04/ADM-08 component has **63** cases: its previous 60 plus three composition regressions. The 231 solution cases also include other teams' tests and are not claimed as this team's own tests.

The actual PowerShell/Kestrel demonstration passed **41/41**, preserving the same delegate cookie issued before revocation. Frontend clean install and TypeScript/Vite build passed. Additional real HTTP checks passed: health, shared default-deny route, anonymous status-change denial, the main Development-stub status-change success, and anonymous account-registration denial. No database was needed for those authorization checks.

The three new `MainCompositionTests` verify that enabling the KT1 simulator retains main's Identity status controller and authorization, that the Identity Development header cannot authenticate/elevate a KT1 caller, and that a KT1 admin cookie cannot grant account-registration permission. These are coexistence tests; production identity/provider integration remains planned.

Current reports are in the delivery's `verification/` directory. Fresh Chromium testing was not repeated after this merge; the browser proof recorded in the previous validation belongs to the previous source version.

## CI preservation and current status

The existing main solution build/test, nonzero-test discovery gate, backend artifact `backend-test-results-${{ github.sha }}` and frontend job are preserved. The live KT1 demonstration and the `acc04-adm08-kt1-results` artifact are included in the same backend job. TRX filenames are generated per execution instead of forcing every test project to overwrite one shared filename. The live host receives the API content root so main's Development configuration loads consistently.

PR: https://github.com/owlCoder/security-lab/pull/36

Verified previous CI: https://github.com/owlCoder/security-lab/actions/runs/41501949640 — success for `3f535351b3a3289fde78c6fa7b90580477863634` before integration.

**A new CI run for the merged source is pending the merge commit being pushed to the same PR branch. No new remote success is claimed.** Preserve the actual tested SHA and direct run/artifact links after it completes. Real Tapiz links and neighboring-team contract acknowledgement remain separate external evidence.

## Scope check

`backend/Platform.Access` and `docs/components/ACC-05-ACC-08.md` match integrated main exactly. This PR implements only ACC-04 and ADM-08 policies; it does not introduce contextual/time-window ACC-08 policies. Legacy `docs/components/access/ACC-08.md`, `frontend/src/components/AccessDemo.tsx`, `backend/Platform.Access.Tests`, and the old `AccessPolicy`/`AttemptDraftService` files are not restored. PR #20/#21 are not cherry-picked or copied into the branch.

See `PR36-MAIN-SYNC.md` for the six conflict resolutions and history-preserving delivery procedure.
