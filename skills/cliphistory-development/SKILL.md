---
name: cliphistory-development
description: Safely continue, review, test, package, or publish the ClipHistory Windows clipboard-history application using its repository documentation, daily development logs, small verified steps, privacy rules, and release scripts. Use for any ClipHistory requirement, implementation, UI, bug-fix, testing, installer, release, or GitHub maintenance request.
---

# ClipHistory Development

Advance ClipHistory in small, reversible, evidence-backed steps. Treat the repository documents as the source of truth and preserve the user's local clipboard data and unrelated changes.

## Locate and orient

1. Locate the repository by finding `ClipHistory.sln`; do not assume a fixed drive or folder name.
2. Read `AGENTS.md` completely.
3. Read every document listed as required by `AGENTS.md`. At minimum, inspect:
   - `docs/01-product-requirements.md`
   - `docs/02-technical-architecture.md`
   - `docs/03-ui-ux-guidelines.md`
   - `docs/04-development-plan.md`
   - `docs/05-quality-security-testing.md`
   - `docs/06-documentation-and-logging.md`
   - `dev-logs/README.md`
4. Inspect `git status -sb` before modifying files. Preserve unrelated user changes.

If these files are absent, stop using project-specific assumptions and ask whether this is the intended repository.

## Start a development step

1. Run:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/Update-DevLog.ps1 -Start
   ```

2. Select the smallest user-visible or technically verifiable outcome that moves the current request forward.
3. State the outcome, affected area, and acceptance check in a concise commentary update.
4. Record the plan with `Update-DevLog.ps1 -Plan "..."`.

Do not expand the feature set beyond the user's request. Ask before making choices that materially change behavior, data retention, privacy, installation scope, or external publishing.

## Implement safely

- Prefer focused patches and existing project patterns over broad rewrites.
- Keep clipboard content local. Never upload clipboard history, images, file paths, databases, logs, or settings.
- Store copied files as paths only; never copy or delete the original files.
- Restrict cleanup to application-owned data under `%LOCALAPPDATA%/ClipHistory/` and validate paths before deletion.
- Preserve retention behavior, pinning semantics, deduplication, pause recording, localization, startup, tray, hotkey, and single-instance behavior unless the current requirement explicitly changes them.
- Update the matching `docs/` file whenever product behavior, architecture, UI rules, development stages, testing rules, or release procedures change.
- Avoid committing generated `artifacts/`, `bin/`, `obj/`, databases, settings, tokens, or user-specific files.

## Verify each step

Choose checks proportional to the change, then finish with the repository verification when code or dependencies changed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/Verify-Build.ps1
```

Use `-NoRestore` only after a valid locked restore. If locked restore fails because a publish command changed runtime identifiers, normalize locks deliberately with `dotnet restore ./ClipHistory.sln --force-evaluate`, review the lock-file diff, and rerun verification.

For UI-only changes, also inspect the relevant XAML and perform a smoke test when the environment can launch the application. Report automated and manual checks separately. Never claim success from compilation alone when the behavior can be tested.

## Continue automatically

When the user asks to continue through multiple small steps, repeat the start, implement, verify, and log cycle until reaching the requested usable milestone. Pause immediately for:

- a destructive or irreversible operation;
- missing credentials, signing certificates, or external authority;
- ambiguous behavior that would materially change the product;
- failing verification that cannot be safely corrected within the current scope;
- unexpected unrelated worktree changes.

Send progress updates during long builds or packaging work. Do not silently broaden the milestone.

## Package a release

Run the following only when the user requests packaging or release work:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/Publish-Windows.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/Build-Installer.ps1
```

Verify the self-contained executable with `--smoke-test`. For an installer, perform an isolated silent install, run the installed application's smoke test, and silently uninstall. Report the output paths and SHA-256 hashes. Explain that an unsigned installer may trigger SmartScreen; do not imply code-signing trust without a real certificate.

## Commit and publish

Before staging, inspect `git status`, the diff, ignored files, and likely secrets. Stage only the intended source, documentation, tests, scripts, and approved assets. Run the relevant verification before committing. Confirm the remote and branch before pushing; never overwrite remote history or force-push without explicit authorization.

## Finish and log

Record completion, verification, decisions, and the next concrete task:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/Update-DevLog.ps1 `
  -Completed "..." `
  -Verification "..." `
  -Decision "..." `
  -Todo "..."
```

End with the achieved outcome, important file or artifact links, verification counts, and any remaining limitation. Keep the explanation accessible to a non-technical user.
