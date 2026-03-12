# Grove — Design Document

> A git worktree manager with per-tree command execution and a live console output panel.

---

## Overview

Grove is a desktop developer tool that detects all git worktrees across one or more repositories and presents them in a unified interface. It can monitor a single repo or scan a parent folder to discover every git repo inside it. For each worktree, you can configure and run commands (e.g. `npm run dev`, custom scripts, shell commands) and observe their output in a live console panel — all without switching terminal windows.

**Core value proposition:** When working across multiple worktrees and repos simultaneously, Grove eliminates the overhead of managing multiple terminal sessions and remembering which command runs in which branch.

---

## Name & Identity

- **Name:** Grove
- **Tagline:** _Your worktrees, all running._
- **Concept:** A grove is a collection of trees — a natural metaphor for git worktrees co-existing and growing in parallel.
- **CLI namespace:** `grove` (e.g. `grove list`, `grove run`, `grove open`)

---

## UI Layout

The app is a two-panel layout: a sidebar listing repo roots with their worktrees nested underneath, and a main detail panel for the selected tree.

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

### Status indicators (sidebar dots)

| Indicator | Meaning                   |
| --------- | ------------------------- |
| `●` green | Process running           |
| `○` grey  | Idle / no process         |
| `✕` red   | Process exited with error |
| `◌` amber | Starting / pending        |

---

## Screens

### 1. Worktree list (sidebar)

- Roots are listed as collapsible top-level groups. Each root shows its repo name (derived from the folder name).
- Under each root, worktrees are auto-detected by running `git worktree list --porcelain` from the repo's directory.
- If a root is a parent folder (scan mode), Grove recursively discovers all git repos inside it and lists each as a sub-group with its own worktrees.
- Each worktree entry shows: branch name, short path, status dot.
- Clicking a worktree entry opens it in the detail panel.
- **"+ add worktree"** button creates a new worktree under the currently selected root via `git worktree add`.
- **"+ add root"** button opens a folder picker to add a new repo root or scan folder.

### 2. Worktree detail panel

Divided into three zones:

**Header**

- Branch name (large)
- Full path and upstream branch (muted)
- Status badge: `running` / `idle` / `error`

**Command bar**

- Text input showing the active command for this worktree
- **Run / Stop / Restart** button (context-aware — shows the right action)
- **Presets strip** — quick-access chips for saved commands. Click to load into the command bar.

**Console output**

- Dark terminal-style panel
- Streams stdout/stderr from the running process in real time
- Monospace font, scrollable
- Shows a dim header line: `grove · <branch> · started Xm ago`
- Clear button to wipe output without stopping the process

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

---

## Functional Requirements

### Worktree detection

- For each configured root:
  - **Repo mode**: Run `git worktree list --porcelain` directly in the root path. Parse output to extract: path, HEAD commit, branch name.
  - **Scan mode**: Recursively search the root folder for directories containing a `.git` folder or file. For each discovered repo, run `git worktree list --porcelain` and group results under that repo.
- Detection runs on launch and on manual refresh
- Handle bare repos and repos with no worktrees gracefully
- Watch for filesystem changes to auto-refresh (optional v2 feature)

### Process management

- Spawn processes using the configured command string, in the worktree's directory as the working directory
- Capture stdout and stderr and stream to the console panel
- Track exit codes — exit 0 → idle, exit non-zero → error state
- Support stop (SIGTERM), force-kill (SIGKILL after timeout), and restart
- One process per worktree maximum — prevent double-starts

### Command configuration

- Commands are stored per worktree path, persisted to a local config file (e.g. `~/.config/grove/config.json`)
- Global presets are also stored in config
- Command strings support shell expansion (run via platform shell: `cmd /c` on Windows, `sh -c` on Unix)

### Console

- Ring buffer: retain last 10,000 lines per worktree
- ANSI colour code support
- Timestamps optional (toggle in settings)
- Copy-to-clipboard button for full console output

### Process persistence

- When the Grove window is closed, processes continue running in the background
- Grove minimises to the system tray (notification area on Windows)
- Tray icon shows aggregate status: green if all processes healthy, red if any errored, grey if all idle
- Tray context menu: list of running processes, "Show Grove", "Quit" (stops all processes and exits)

### Platform support

- Shell execution uses platform-appropriate invocation: `cmd /c` on Windows, `sh -c` on Unix
- Process termination: graceful shutdown via `taskkill` on Windows, `SIGTERM` on Unix, with force-kill fallback after timeout
- Tray icon uses platform-native system tray APIs (Avalonia's `TrayIcon` support)
- Config path: `%APPDATA%\grove\` on Windows, `~/.config/grove/` on Unix

### Environment variables

- Per-worktree environment variable overrides, stored in config
- Env vars are merged with the system environment when spawning processes (overrides take precedence)
- Configured as key-value pairs in the settings UI and in the data model

---

## Data Model

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

---

## Tech Stack

| Layer           | Choice                                     | Notes                                          |
| --------------- | ------------------------------------------ | ---------------------------------------------- |
| Framework       | Avalonia UI (.NET)                         | Cross-platform native desktop, single codebase |
| Language        | C#                                         | .NET 10                                        |
| UI pattern      | MVVM (Model-View-ViewModel)                | Avalonia's natural pattern; ReactiveUI or CommunityToolkit.Mvvm |
| Styling         | Custom Avalonia styles/themes              | Custom control templates for terminal-style console, sidebar, etc. |
| Process mgmt    | `System.Diagnostics.Process`               | Native .NET process management                 |
| Config          | JSON file in `~/.config/grove/`            | Simple, inspectable; `System.Text.Json`        |
| Git integration | Shell out to `git` CLI                     | No libgit2 dependency needed                   |

---

## MVP Scope

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

**Out of scope for v1:**

- Filesystem watcher for auto-refresh
- CLI (`grove` command)
- Log export
- SSH / remote worktrees

---

## Open Questions

1. **Auto-detection root** — detect from CWD on launch, or always prompt for a root path? (Partially resolved: roots are explicitly configured in settings, but first-launch UX is TBD.)
2. **Scan depth limit** — when scanning a parent folder, should there be a max recursion depth to avoid scanning huge directory trees?
3. **Root ordering** — should the user be able to reorder roots in the sidebar, or are they always alphabetical?
