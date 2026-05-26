# Pull Request Workflow

This document describes the standard procedure for contributing code to this repository. Every new piece of work must follow this workflow to keep the `main` branch stable and the history clean.

## Overview

The workflow has five stages:

1. Open an issue (or pick an existing one).
2. Create a branch directly from that issue.
3. Develop and push your changes.
4. Open a pull request and wait for review.
5. Merge and clean up the branch.

---

## 1. Start from a GitHub Issue

All work begins from an issue. This keeps every change traceable to a requirement or bug report.

- Go to the **Issues** tab of the repository.
- Open an existing issue you want to work on, or create a new one describing the change.
- On the right-hand sidebar of the issue, click **Create a branch** under the "Development" section.
- Accept the default branch name (GitHub will suggest something like `42-fix-login-redirect`) or customize it.
- Make sure the branch source is `main`.
- Click **Create branch**.

GitHub will create the branch on the remote and link it to the issue automatically.

## 2. Check Out the Branch Locally

Fetch the new branch and switch to it:

```bash
git fetch origin
git checkout <branch-name>
```

## 3. Develop and Push

Work on your changes locally, committing as you go:

```bash
git add .
git commit -m "Short, descriptive message"
git push origin <branch-name>
```

## 4. Open a Pull Request

When the work is ready for review:

- Go to the repository on GitHub.
- You'll see a banner suggesting to open a pull request for your recently pushed branch — click **Compare & pull request**.
- Set the base branch to `main` and the compare branch to your feature branch.
- Fill in the PR description:
  - Reference the issue with `Closes #<issue-number>` so it auto-closes on merge.
  - Briefly explain what changed and why.
  - List anything reviewers should pay particular attention to.
- Request a review from someone with merge permissions.
- Click **Create pull request**.

## 5. Review and Merge

- Wait for the reviewer to approve the PR.
- Address any feedback by pushing additional commits to the same branch — the PR updates automatically.
- Only users with merge permissions can merge into `main`.
- Once approved, the reviewer (or you, if you have permission) merges the PR.

## 6. Delete the Branch Automatically

To keep the repository tidy, branches should be removed once merged. This can be automated in two ways:

### Repository setting (recommended)

A maintainer should enable this once for the whole repo:

- Go to **Settings → General → Pull Requests**.
- Check **Automatically delete head branches**.

With this setting on, GitHub deletes the branch on the remote as soon as the PR is merged.

### Manual cleanup (local)

After the remote branch is deleted, clean up your local copy:

```bash
git checkout main
git pull origin main
git branch -d <branch-name>
git fetch --prune
```

---

## Quick Reference

| Step | Action | Where |
|------|--------|-------|
| 1 | Create branch from issue | GitHub issue page |
| 2 | Checkout branch | Local terminal |
| 3 | Commit and push changes | Local terminal |
| 4 | Open pull request | GitHub |
| 5 | Wait for approval and merge | GitHub |
| 6 | Branch deleted automatically | GitHub (auto) |

## Rules

- **Never push directly to `main`.** All changes go through a PR.
- **Never merge your own PR** unless explicitly authorized.
