# PR #36 — current-main integration

Team 8 scope: **ACC-04 / ADM-08**, as confirmed in the PR review. Main input: `8420f3b6710dda51b483b3743691996951370c88`. Feature input: `3f535351b3a3289fde78c6fa7b90580477863634`.

A normal merge keeps both histories and needs no force push. The delivered resolved tree contains main's merged Identity, Audit and Authoring work and this component's contributions.

| Conflict | Resolution |
|---|---|
| `.github/workflows/ci.yml` | Keep main's solution-wide build/test, test-discovery guard, backend artifact and frontend job; include the live KT1 check and its artifact |
| `backend/SecurityLab.slnx` | Keep every current-main entry and include `Platform.Kt1.Tests` exactly once |
| `backend/Platform.Api/Program.cs` | Keep main's Identity/Audit registration, controllers, session middleware, development seeds and decision-interface aliases; compose explicitly enabled Development-only KT1 services/routes |
| `frontend/src/App.tsx` | Keep the account-creation form and render the separate coursework component |
| `frontend/src/style.css` | Keep main's styles; scope every coursework-specific addition under `.kt1` |
| `README.md` | Keep the complete main README and append only the ACC-04/ADM-08 section |

Three composition tests were added in `MainCompositionTests.cs`. The local test helper uses generated TRX filenames for the multi-project solution. No other team's module implementation is edited.

## Scope safeguards

- The shared Team 3 access implementation/documentation matches main byte-for-byte at the Git blob level.
- Owned policies have actions `attempt.read`, `attempt.draft.update`, `course.admin.read`, `course.settings.update`, `course.delegation.create` and `course.delegation.revoke`.
- No contextual-policy port, time-window fixture or old ACC-08 component is added.
- No replay/cherry-pick of PR #20/#21 is used. The new PR diff is measured against integrated main, which is a parent of the eventual merge commit.

## Publication

Use the delivered PowerShell procedure to create an isolated repair worktree, perform the real merge, apply only the pinned reviewed resolutions, and verify the resolved Git tree. Original local files/staging remain untouched. After local checks, create a normal merge commit and push `HEAD:feature/acc04-adm08-kt1`; this updates **the existing PR #36**, not a new PR. The new remote CI result must be checked after that push.
