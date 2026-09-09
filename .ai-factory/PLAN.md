<!-- handoff:task:ffa4c735-32b5-497c-a53c-5850cd1b34c0 -->

# Implementation Plan: Download existing repository

Branch: master
Created: 2026-09-09

## Settings
- [ ] Testing: no
- [ ] Logging: verbose
- [ ] Docs: no

## Tasks

### Phase 1: Prepare & Fetch
- [ ] Task 1: Create the initial commit on `master` containing the existing AI Factory scaffolding files (`.ai-factory.json`, `.ai-factory/`, `.claude/`, `.codex/`, `.opencode/`). `git status` currently reports no commits yet, so these files are untracked; committing them first ensures the history merge in Task 3 is a real git merge with conflict resolution, instead of a checkout that can fail or silently clobber files that share a path with the incoming repository.
- [ ] Task 2: Add `https://github.com/OctavanGodonoga/factory-ai-first-step.git` as a git remote named `origin` and fetch its history/branches verbosely (`git fetch origin -v`). Note: the remote's default branch is `main` (confirmed via `git ls-remote --symref`), not `master`.

### Phase 2: Integrate
- [ ] Task 3: Merge `origin/main` into local `master` with `git merge origin/main --allow-unrelated-histories` (required since the two histories share no common ancestor). Preserve the existing AI Factory setup files by resolving any path conflicts in favor of the local scaffolding version, and take the incoming repository's content for everything else. After the merge succeeds, rename the branch from `master` to `main` (`git branch -m master main`) to match both the remote's default branch and the project's configured base branch (`.ai-factory/config.yaml` → `git.base_branch: main`). (depends on 1, 2)

### Phase 3: Verify
- [ ] Task 4: Verify the working tree is clean (`git status`), the local branch is named `main`, `git log` shows the imported repository's commit history grafted onto the initial scaffolding commit, and the AI Factory setup files (`.ai-factory.json`, `.ai-factory/`, `.claude/`, `.codex/`, `.opencode/`) are present and unchanged. (depends on 3)

## Commit Plan
This plan performs git history operations directly: Task 1 creates the initial scaffolding commit and Task 3 creates the merge commit. No additional wrapping commit (e.g. a final "chore: import ..." commit) is needed or expected after Task 4's verification — do not squash or re-commit on top of the merge.
