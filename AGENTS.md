# sumit: local verification and cost policy

Owner instruction (2026-11-22):
- GitHub-hosted CI is reserved for manual review/epic milestones; NEVER add automatic `push`, `pull_request` or scheduled Actions triggers without explicit owner approval.
- Before EVERY push, run local checks and build/tests using `bash .githooks/pre-push`. Install tracked pre-push hook in each clone/worktree: `git config --local core.hooksPath .githooks` and `chmod +x .githooks/pre-push` (Git Bash on Windows).
- Fail closed when a required local toolchain, build, or test fails. Do not bypass with `--no-verify` or falsely claim a pass; report blockers clearly.
- Documentation-only / legacy check-only repositories require real tests to be added before claiming a full local CI equivalent.
- After code review, the owner may choose a manual GitHub Actions run (about once per epic), never on every commit. Respect any branch-protection required-check and independent-review rules; do not circumvent those even when a local check passed.
- Record exact tested SHA, commands and results with the PR or agent handoff.
