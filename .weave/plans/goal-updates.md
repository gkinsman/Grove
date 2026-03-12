# GOAL.md — Five Design Changes

## TL;DR
> **Summary**: Apply five confirmed design decisions to GOAL.md — multi-repo roots in scope, parent folder scanning, tray icon confirmed, Windows support confirmed, per-worktree env vars confirmed. These ripple through nearly every section of the document.
> **Estimated Effort**: Medium

## Context
### Original Request
The user wants five changes applied to the design document at `GOAL.md`. Each change resolves an open question or promotes a deferred feature into v1 scope. The changes interact — multi-repo roots and parent folder scanning together reshape the sidebar, data model, and detection logic.

### Key Findings
- GOAL.md is 221 lines, well-structured with clear sections: Overview, UI Layout (ASCII art), Screens, Functional Requirements, Data Model (JSON), Tech Stack, MVP Scope, Open Questions.
- Open Questions currently has 5 items (lines 217–221). All five are being resolved by this change set.
- The data model (lines 156–175) uses a single `rootPath` string — needs to become an array of root objects.
- The sidebar ASCII art (lines 28–47) shows a single repo — needs to show multiple roots grouped.
- Worktree detection (lines 124–129) describes a single-repo flow — needs a folder-scanning mode.

## Objectives
### Core Objective
Update GOAL.md to reflect all five confirmed design decisions, ensuring consistency across every section they touch.

### Deliverables
- [ ] Updated GOAL.md with all five changes applied consistently

### Definition of Done
- [ ] Open Questions section has 0 unresolved items from the original 5 (section can be removed or repurposed)
- [ ] Multi-repo roots appear in MVP "in scope" and are removed from "out of scope"
- [ ] Sidebar ASCII art shows multiple roots with nested worktrees
- [ ] Data model JSON supports multiple roots with both direct-repo and folder-scan modes
- [ ] Tray icon, Windows support, and env var overrides appear as confirmed requirements
- [ ] No internal contradictions between sections

### Guardrails (Must NOT)
- Do not change the Tech Stack section
- Do not add features beyond what the five changes describe
- Do not alter the document's overall structure/ordering of sections

## TODOs

- [ ] 1. **Update Overview paragraph** (lines 9–11)
  **What**: Adjust the overview to mention multi-repo support. Currently says "detects all git worktrees in a repository" — should say "detects all git worktrees across one or more repositories".
  **Files**: `GOAL.md` lines 9–11
  **Change**: Replace:
  ```
  Grove is a desktop developer tool that detects all git worktrees in a repository and presents them in a unified interface.
  ```
  With:
  ```
  Grove is a desktop developer tool that detects all git worktrees across one or more repositories and presents them in a unified interface. It can monitor a single repo or scan a parent folder to discover every git repo inside it.
  ```
  **Acceptance**: Overview mentions multi-repo and folder scanning.

- [ ] 2. **Update UI Layout description** (line 26)
  **What**: The intro sentence says "a sidebar listing worktrees" — update to mention grouping by repo root.
  **Files**: `GOAL.md` line 26
  **Change**: Replace:
  ```
  The app is a two-panel layout: a sidebar listing worktrees, and a main detail panel for the selected tree.
  ```
  With:
  ```
  The app is a two-panel layout: a sidebar listing repo roots with their worktrees nested underneath, and a main detail panel for the selected tree.
  ```
  **Acceptance**: Layout description reflects multi-root grouping.

- [ ] 3. **Replace sidebar ASCII art** (lines 28–47)
  **What**: The current ASCII art shows a single repo `MY-APP`. Replace with a multi-root layout showing two repos, each with nested worktrees. One root is a direct repo, the other discovered via folder scan.
  **Files**: `GOAL.md` lines 28–47
  **Change**: Replace the entire ASCII block with:
  ```
  ┌──────────────────────────────────────────────────────────────┐
  │  🌿 grove                                                    │
  ├─────────────────────┬────────────────────────────────────────┤
  │  ▾ MY-APP           │  main                                  │
  │    ● main           │  ~/code/my-app · origin/main   [running]│
  │    ○ feat/auth      ├────────────────────────────────────────┤
  │    ✕ fix/payment    │  COMMAND                               │
  │                     │  [ npm run dev              ] [restart] │
  │  ▾ API-SERVER       │  Presets: npm run dev · npm test · +   │
  │    ● main           ├────────────────────────────────────────┤
  │    ○ feat/graphql   │  ▶ vite dev                            │
  │                     │                                        │
  │  + add worktree     │    VITE v5.2.1  ready in 312ms         │
  │  + add root         │                                        │
  │                     │    ➜  Local:   http://localhost:5173/  │
  │                     │    ➜  Network: http://192.168.1.4:5173/│
  │                     │                                        │
  │                     │    [HMR] page reload (src/App.tsx)     │
  └─────────────────────┴────────────────────────────────────────┘
  ```
  Key changes:
  - Header no longer says `[repo: my-app]` — there's no single repo anymore
  - Two root groups: `▾ MY-APP` and `▾ API-SERVER` with collapse triangles
  - Worktrees nested under each root
  - New `+ add root` button alongside `+ add worktree`
  **Acceptance**: ASCII art shows two repo roots with nested worktrees and collapse indicators.

- [ ] 4. **Update Screens § Worktree list (sidebar)** (lines 63–68)
  **What**: Rewrite the sidebar screen description to reflect multi-root grouping and folder scanning.
  **Files**: `GOAL.md` lines 63–68
  **Change**: Replace:
  ```
  ### 1. Worktree list (sidebar)

  - Auto-detected on launch by running `git worktree list` from any git repo in the working directory, or from a user-configured root path.
  - Each entry shows: branch name, short path, status dot.
  - Clicking an entry opens it in the detail panel.
  - An **"+ add worktree"** button at the bottom opens a shell prompt or modal to create a new worktree via `git worktree add`.
  ```
  With:
  ```
  ### 1. Worktree list (sidebar)

  - Roots are listed as collapsible top-level groups. Each root shows its repo name (derived from the folder name).
  - Under each root, worktrees are auto-detected by running `git worktree list --porcelain` from the repo's directory.
  - If a root is a parent folder (scan mode), Grove recursively discovers all git repos inside it and lists each as a sub-group with its own worktrees.
  - Each worktree entry shows: branch name, short path, status dot.
  - Clicking a worktree entry opens it in the detail panel.
  - **"+ add worktree"** button creates a new worktree under the currently selected root via `git worktree add`.
  - **"+ add root"** button opens a folder picker to add a new repo root or scan folder.
  ```
  **Acceptance**: Sidebar description covers multi-root, collapsible groups, folder scanning, and both add buttons.

- [ ] 5. **Update Screens § Settings page** (lines 95–118)
  **What**: Three changes to the Settings page:
  (a) Replace "Root path" with a roots manager (add/remove roots, set scan mode).
  (b) Add env var overrides to the "Per-worktree overrides" section.
  (c) Keep everything else intact.
  **Files**: `GOAL.md` lines 95–118
  **Change**: Replace the Settings page section with:
  ```
  ### 3. Settings page

  Accessible via a gear icon. Contains:

  **Global defaults**

  - Default command to run for new worktrees (e.g. `npm run dev`)
  - Auto-start: toggle whether Grove runs the default command on worktree selection

  **Roots manager**

  - List of configured roots, each with:
    - Path (absolute)
    - Mode: `repo` (single git repo) or `scan` (parent folder — discover all repos inside)
  - Add / remove roots
  - Roots are scanned on launch and on manual refresh

  **Presets manager**

  - Named command presets (name + command string)
  - Reorder, edit, delete
  - Presets are global and available to all worktrees

  **Per-worktree overrides**

  - Override the default command for a specific worktree
  - Override environment variables per worktree (e.g. `PORT=3001`, `NODE_ENV=staging`)
  - Shown in a table: branch → command, env vars

  **Appearance**

  - Light / Dark / System theme
  - Font size for console output
  ```
  Key changes: "Root path" line removed from Global defaults, new "Roots manager" subsection added, env var overrides added to Per-worktree overrides.
  **Acceptance**: Settings page has Roots manager, env var overrides, and no single-root-path field.

- [ ] 6. **Update Functional Requirements § Worktree detection** (lines 124–129)
  **What**: Add folder-scanning detection mode alongside the existing single-repo approach.
  **Files**: `GOAL.md` lines 124–129
  **Change**: Replace:
  ```
  ### Worktree detection

  - Run `git worktree list --porcelain` on launch and on manual refresh
  - Parse output to extract: path, HEAD commit, branch name
  - Watch for filesystem changes to auto-refresh (optional v2 feature)
  - Handle bare repos and repos with no worktrees gracefully
  ```
  With:
  ```
  ### Worktree detection

  - For each configured root:
    - **Repo mode**: Run `git worktree list --porcelain` directly in the root path. Parse output to extract: path, HEAD commit, branch name.
    - **Scan mode**: Recursively search the root folder for directories containing a `.git` folder or file. For each discovered repo, run `git worktree list --porcelain` and group results under that repo.
  - Detection runs on launch and on manual refresh
  - Handle bare repos and repos with no worktrees gracefully
  - Watch for filesystem changes to auto-refresh (optional v2 feature)
  ```
  **Acceptance**: Detection section describes both repo mode and scan mode.

- [ ] 7. **Add Functional Requirements § Process persistence** (after line 151, before Data Model)
  **What**: Add a new subsection for process persistence with tray icon — this was Open Question #3, now confirmed.
  **Files**: `GOAL.md` — insert after the Console subsection (after line 151), before the `---` separator and Data Model.
  **Change**: Insert:
  ```

  ### Process persistence

  - When the Grove window is closed, processes continue running in the background
  - Grove minimises to the system tray (notification area on Windows)
  - Tray icon shows aggregate status: green if all processes healthy, red if any errored, grey if all idle
  - Tray context menu: list of running processes, "Show Grove", "Quit" (stops all processes and exits)
  ```
  **Acceptance**: Process persistence is a documented functional requirement with tray icon behaviour.

- [ ] 8. **Add Functional Requirements § Platform support** (after the new Process persistence section)
  **What**: Add a subsection confirming Windows platform branching — this was Open Question #4, now confirmed.
  **Files**: `GOAL.md` — insert after Process persistence, before `---` and Data Model.
  **Change**: Insert:
  ```

  ### Platform support

  - Shell execution uses platform-appropriate invocation: `cmd /c` on Windows, `sh -c` on Unix
  - Process termination: graceful shutdown via `taskkill` on Windows, `SIGTERM` on Unix, with force-kill fallback after timeout
  - Tray icon uses platform-native system tray APIs (Avalonia's `TrayIcon` support)
  - Config path: `%APPDATA%\grove\` on Windows, `~/.config/grove/` on Unix
  ```
  **Acceptance**: Platform support is a documented functional requirement with Windows-specific details.

- [ ] 9. **Add Functional Requirements § Environment variables** (after Platform support)
  **What**: Add env var override support — this was Open Question #5, now confirmed.
  **Files**: `GOAL.md` — insert after Platform support, before `---` and Data Model.
  **Change**: Insert:
  ```

  ### Environment variables

  - Per-worktree environment variable overrides, stored in config
  - Env vars are merged with the system environment when spawning processes (overrides take precedence)
  - Configured as key-value pairs in the settings UI and in the data model
  ```
  **Acceptance**: Env var overrides are a documented functional requirement.

- [ ] 10. **Replace Data Model JSON** (lines 156–175)
  **What**: Rewrite the data model to support multiple roots (with mode), and per-worktree env var overrides.
  **Files**: `GOAL.md` lines 156–175
  **Change**: Replace:
  ```json
  {
    "rootPath": "~/code/my-app",
    "defaultCommand": "npm run dev",
    "autoStart": false,
    "presets": [
      { "id": "p1", "name": "Dev server", "command": "npm run dev" },
      { "id": "p2", "name": "Tests", "command": "npm test" },
      { "id": "p3", "name": "Build", "command": "npm run build" }
    ],
    "worktrees": {
      "~/code/my-app": {
        "command": "npm run dev"
      },
      "~/code/my-app-auth": {
        "command": "npm run dev -- --port 5174"
      }
    }
  }
  ```
  With:
  ```json
  {
    "roots": [
      { "id": "r1", "path": "~/code/my-app", "mode": "repo" },
      { "id": "r2", "path": "~/code/projects", "mode": "scan" }
    ],
    "defaultCommand": "npm run dev",
    "autoStart": false,
    "presets": [
      { "id": "p1", "name": "Dev server", "command": "npm run dev" },
      { "id": "p2", "name": "Tests", "command": "npm test" },
      { "id": "p3", "name": "Build", "command": "npm run build" }
    ],
    "worktrees": {
      "~/code/my-app": {
        "command": "npm run dev",
        "env": {}
      },
      "~/code/my-app-auth": {
        "command": "npm run dev -- --port 5174",
        "env": { "PORT": "5174" }
      },
      "~/code/projects/api-server": {
        "command": "npm start",
        "env": { "PORT": "4000", "NODE_ENV": "development" }
      }
    }
  }
  ```
  Key changes:
  - `rootPath` (string) → `roots` (array of objects with `id`, `path`, `mode`)
  - `mode` is either `"repo"` (single git repo) or `"scan"` (parent folder)
  - Each worktree entry gains an `env` object for environment variable overrides
  - Added a third worktree example from a scan-discovered repo
  **Acceptance**: Data model supports multiple roots with modes and per-worktree env vars.

- [ ] 11. **Update MVP Scope — In scope** (lines 195–201)
  **What**: Add multi-repo roots, parent folder scanning, tray icon persistence, and env var overrides to the "in scope" list.
  **Files**: `GOAL.md` lines 195–201
  **Change**: Replace:
  ```
  **In scope for v1:**

  - Worktree detection from a single root repo
  - Per-worktree command configuration
  - Run / stop / restart process
  - Live console output panel
  - Global presets
  - Settings page (command defaults, presets manager)
  - Light/dark theme
  ```
  With:
  ```
  **In scope for v1:**

  - Multi-repo roots — sidebar groups worktrees under each root
  - Parent folder scanning — set a root to a folder and discover all repos inside
  - Per-worktree command configuration
  - Per-worktree environment variable overrides
  - Run / stop / restart process
  - Live console output panel
  - Process persistence with system tray icon
  - Global presets
  - Settings page (roots manager, command defaults, presets manager, per-worktree overrides)
  - Light/dark theme
  - Windows and Unix platform support
  ```
  **Acceptance**: All five confirmed features appear in the v1 scope list.

- [ ] 12. **Update MVP Scope — Out of scope** (lines 203–210)
  **What**: Remove "Multiple repo roots" from out-of-scope (it's now in scope). Keep the rest.
  **Files**: `GOAL.md` lines 203–210
  **Change**: Replace:
  ```
  **Out of scope for v1:**

  - Multiple repo roots
  - Filesystem watcher for auto-refresh
  - CLI (`grove` command)
  - Log export
  - SSH / remote worktrees
  ```
  With:
  ```
  **Out of scope for v1:**

  - Filesystem watcher for auto-refresh
  - CLI (`grove` command)
  - Log export
  - SSH / remote worktrees
  ```
  **Acceptance**: "Multiple repo roots" no longer appears in out-of-scope.

- [ ] 13. **Replace Open Questions section** (lines 214–221)
  **What**: All five original open questions are now resolved. Replace the section with a note that all original questions have been resolved, and optionally list any new questions that arise from the changes.
  **Files**: `GOAL.md` lines 214–221
  **Change**: Replace:
  ```
  ## Open Questions

  1. **Multi-repo support** — should Grove manage worktrees across multiple repos, or stay scoped to one repo at a time?
  2. **Auto-detection root** — detect from CWD on launch, or always prompt for a root path?
  3. **Process persistence** — should processes keep running if the Grove window is closed? (Probably yes, with a tray icon.)
  4. **Windows support** — SIGTERM behaviour differs on Windows; shell command execution will need platform branching.
  5. **Environment variables** — should Grove support per-worktree env var overrides (e.g. `PORT=3001`)?
  ```
  With:
  ```
  ## Open Questions

  1. **Auto-detection root** — detect from CWD on launch, or always prompt for a root path? (Partially resolved: roots are explicitly configured in settings, but first-launch UX is TBD.)
  2. **Scan depth limit** — when scanning a parent folder, should there be a max recursion depth to avoid scanning huge directory trees?
  3. **Root ordering** — should the user be able to reorder roots in the sidebar, or are they always alphabetical?
  ```
  Note: Questions 1 (multi-repo), 3 (tray icon), 4 (Windows), and 5 (env vars) are fully resolved and removed. Question 2 (auto-detection root) is partially resolved — roots are now explicitly configured, but the first-launch experience is still open. Two new questions arise naturally from the folder-scanning feature.
  **Acceptance**: Original questions 1, 3, 4, 5 are gone. Remaining/new questions reflect the updated design.

## Verification
- [ ] Read through the final GOAL.md top-to-bottom and confirm no section still references "single root" or "rootPath" as a singular concept
- [ ] Confirm the ASCII art renders correctly (consistent column widths, box-drawing characters aligned)
- [ ] Confirm the JSON data model is valid JSON
- [ ] Confirm no original Open Question (1, 3, 4, 5) remains unresolved
- [ ] Confirm all five changes are reflected in at least: MVP Scope, Functional Requirements, and one other relevant section
