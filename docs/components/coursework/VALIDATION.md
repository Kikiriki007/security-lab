# Actual local validation — ACC-04 and ADM-08

Package validation date: **2026-11-01**. Source basis: `main` commit `0d2d5ced289d7b23abca4b1ad35895d1cb3b9057`. Results below concern this new ACC-04 + ADM-08 source package, not earlier ACC-04/context-window PRs.

| Check actually executed | Result | Evidence |
|---|---|---|
| .NET 10 Release build + NUnit unit/HTTP/contract tests | **60/60 passed; 0 failed, 0 skipped** | `verification/acc04-adm08-kt1.trx` in the delivery ZIP; regenerated under `TestResults/` locally |
| PowerShell script against a running real Kestrel API | **41/41 passed** | `verification/acc04-adm08-http-demo.json`, `powershell-demo.log` |
| Frontend clean install and TypeScript/Vite build | **PASS** | `verification/frontend-build.log` |
| Actual Chromium browser | **PASS** | Owned draft update, identity switch clears displayed content, IDOR denial, course delegation, settings update, UI revocation in independent browser contexts, unchanged pre-revocation delegate cookie, exactly one delegate sign-in, read/write denial; 420px layout without horizontal overflow |
| Diagram rendering | Six Mermaid sources rendered as six SVGs; visual inspection passed | `diagrams/*.mmd` and `*.svg` |
| Safe worktree installer | PASS on Linux PowerShell: paths with spaces, exact Git index tree, actual original staged/unstaged/untracked work preserved, repeat install refuses overwrite | `verification/installer-validation.txt` |

The runtime used for local verification is Linux with .NET SDK 10.0.403 and PowerShell 7.6.9. Windows PowerShell 5.1-compatible syntax is used for the install/demo/task scripts, but this package was not executed on the student's Windows PC. Run the supplied Windows commands and retain that machine's actual outputs.

The actual HTTP demo checks: anonymous read/write, owner read/write, persisted answer, stale update, immutable submitted/completed history, invalid input, missing mutation header, foreign ID/write, teacher scope and draft privacy, blocked account, assistant denial, admin with no answer bypass, isolated A/B administration, whitelist/self-grant/duplicate constraints, scoped delegation, delegate settings update, stale course version, no cross-course access or redelegation, cross-course/stale revocation, successful revoke and immediate denial of the delegate's existing cookie.

The automated suite also verifies provider outages, snapshot-bound reads/CAS, exactly one winner among concurrent draft updates, administrative revocation at the final storage boundary, secret-free audit, production/disabled-demo route absence and compatibility with the existing shared default-deny contract.

## Remote evidence

- New PR: **PENDING** — create a new ACC-04 + ADM-08 PR from the main-based branch.
- New GitHub Actions run for this source: **PENDING** — not executed or claimed before upload.
- Expected artifact name configured in CI: `acc04-adm08-kt1-results` — its remote existence must be confirmed from that actual run.
- Real Tapiz task/card IDs and links: **PENDING**.
- Actual neighboring-team contract acknowledgement: **PENDING**, see `CONTRACTS.md`.

The GitHub workflow retains the existing backend and frontend jobs and adds tests, live demonstration and artifact upload to the backend job. It runs on pull requests and pushes to main. A local feature-branch push by itself is not proof of a run. Replace pending entries only after checking real URLs, the tested commit and the successful result. The packaged reports are a local snapshot, not a remote CI artifact or proof of the student's earlier authorship.

## Results after the detailed-review corrections

The two dedicated `RevocationRejectsTheSamePreviouslyIssuedCookie` cases cover read and write using an explicitly captured cookie with automatic cookie handling disabled. The lifecycle test also keeps independent admin/delegate clients. The live PowerShell script reports 41/41 and records `revocationProof` with separate clients, one delegate sign-in and the same cookie before/after revocation. Actual Chromium verification uses two browser contexts and confirms both read/write denial without a new delegate login. No cookie value is written into the proof JSON.

Both DFDs were regenerated and visually inspected with provider/store return flows, explicit rejection paths and application audit. Transport rejections that occur before application processing are shown separately and are not falsely claimed to produce an application audit event.

`record-kt1-evidence.ps1` parses and was exercised against a disposable synthetic fixture: missing required fields cause no writes, valid references are stored, and CI success is not inferred from a URL. Source external-evidence fields remain pending. PowerShell syntax checks and the fresh-install / prior-package update / no-overwrite checks are included in the delivery verification folder. Windows execution and a new remote Actions run remain unconfirmed until actually performed.
