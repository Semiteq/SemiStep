---
name: release
description: "Use this skill when the user wants to create a release. Checks working tree, proposes a tag message based on recent commits, gets user approval, then commits, tags, pushes, and monitors CI. Never releases without explicit user approval."
---

# Skill: Release

## Critical Rule

**Never create the tag or push without the user explicitly approving.**
Propose, wait, then execute. No exceptions.

---

## Step 1: Check Working Tree

```bash
git status
git diff --stat
```

If there are uncommitted changes, help the user handle them:
- Read the diff, propose a commit message based on what changed.
- Wait for the user to confirm or edit the message, then run `git add -A && git commit -m "..."`.
- If the user wants to handle it themselves, wait.

---

## Step 2: Determine Version

If the user has not stated a version, ask. Format: `vMAJOR.MINOR.PATCH`.
If the user gives a bare number like `1.2.0`, prepend `v` automatically.

Show the most recent tags for context:

```bash
git tag --sort=-version:refname | head -5
```

---

## Step 3: Propose Tag Message

Read recent commits since the last tag and propose concise release notes:

```bash
git log --oneline $(git describe --tags --abbrev=0 2>/dev/null)..HEAD 2>/dev/null || git log --oneline -10
```

Draft a tag message based on the actual commits. Present it to the user as an editable
proposal — do not ask them to write it from scratch. Wait for them to confirm or edit.

The tag message becomes the GitHub release body, with two quirks to mind:

1. **Do NOT repeat the version or product name as the first line of the tag body.**
   `release.yml` sets the release `name:` to `SemiStep <version>` via the workflow,
   so the GitHub release page already shows that as the page heading. If the body
   also starts with `SemiStep 0.2.0` (or similar), the title is duplicated. Start
   the body with the first content section (e.g. `Highlights since 0.1.0:` or the
   first `##` heading).

2. **`release.yml` runs the body through `sed '/^$/d'`, which strips every blank line.**
   Markdown paragraph and heading breaks WILL collapse — sections like
   `PLC integration` glue to the bullets below them and to the prose above them.
   Until the workflow is fixed, draft the tag message so it survives blank-line
   removal: prefix section labels with `##` (they still render distinctly even
   adjacent to bullets), or warn the user that they will need to edit the rendered
   release body manually after the workflow completes.

---

## Step 4: Get Approval and Execute

Once the user approves the version and tag message, execute without further ceremony:

```bash
git tag -a <version> -m "<approved message>"
git push origin <version>
```

After the push, report the Actions run URL:
`https://github.com/<owner>/<repo>/actions`

---

## Step 5: Monitor CI

Poll the workflow run status until it completes:

```bash
gh run list --limit 1
gh run watch <run-id>
```

- If CI passes: report success and the release URL
  `https://github.com/<owner>/<repo>/releases/tag/<version>`
- If CI fails: show the failing step and logs, then ask the user how to proceed.

---

## Step 6: Redo a Release (Delete and Retag)

If the tag needs to be redone (CI failure, wrong message, wrong commit):
Get user approval, then:

```bash
gh release delete <version> --yes
git push origin --delete <version>
git tag -d <version>
```

Then re-run from Step 3 with the corrected version or message.

---

## Local Installer Build (Only When Explicitly Requested)

Do not run this unless the user asks. Commands to build the installer locally:

```powershell
dotnet clean SemiStep.slnx -c Release
dotnet publish SemiStep/SemiStep.UI/SemiStep.UI.csproj -c Release -p:Version=<version>
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DAppVersion=<version> Installer\SemiStep.iss
```

Output: `Installer/Output/SemiStep-Setup.exe`

---

## Reminders

- Release workflow triggers on any `v*` tag push (`.github/workflows/release.yml`).
- Always use annotated tags (`git tag -a`). Never lightweight tags.
- The annotated tag message is fed into the GitHub release body, but the workflow
  pipes it through `sed '/^$/d'` which deletes every blank line. Markdown paragraph
  and heading breaks WILL collapse. See Step 3 for mitigation.
- Installer output file must remain `SemiStep-Setup.exe` — the workflow upload glob depends on it.
