# AutoStryke

A Discord bot for Valorant team management — pro-team composition lookup, your
own team's comp planner, scrim/match scheduling, and automatic Premier match
result tracking.

## Requirements

- **.NET 8 SDK**
- **Python 3.11 or newer**, with these packages installed:
  ```
  pip install vlrdevapi requests beautifulsoup4
  ```
  Note: `vlrdevapi` specifically requires Python 3.11+. If your system's
  default `python3` is older than that, install a newer version alongside it
  (don't replace the system default) and point the bot at it — see
  `pythonInterpreter` below.

## Setup

1. Create `AutoStryke/config/config.json` (this file is intentionally
   **not** committed to git — it holds live secrets):

   ```json
   {
     "token": "your Discord bot token",
     "prefix": "!",
     "henrikApiKey": "your HenrikDev API key",
     "premierTeamName": "your Premier team name",
     "premierTeamTag": "your Premier team tag",
     "premierRegion": "eu",
     "pythonInterpreter": "python3.11"
   }
   ```

   | Field | What it's for | Where to get it |
   |---|---|---|
   | `token` | Discord bot login | Discord Developer Portal → your app → Bot tab → Reset Token |
   | `prefix` | Prefix for text commands like `!ping` | Pick anything, e.g. `!` |
   | `henrikApiKey` | Powers automatic Premier match tracking | HenrikDev's Discord — request a "Basic Key" |
   | `premierTeamName` / `premierTeamTag` | Which Premier team to track | Your team's exact name and tag, e.g. `Sea Creatures` / `SEA` |
   | `premierRegion` | Your Valorant server region | One of `na`, `eu`, `ap`, `kr`, `latam`, `br` |
   | `pythonInterpreter` | Which Python to launch for the pro-comp updater | e.g. `python3.11` — must be 3.11+ |

   **Never commit this file.** If you ever see a real key show up in
   `git status` as a new/modified file, stop and check `.gitignore` before
   committing.

2. Run the bot from the `AutoStryke/AutoStryke` project folder:
   ```
   dotnet run
   ```

## Slash commands

### Pro-comp database (VCT + VCL 2026 Stage 2)

- **`/updateprocomps`** — refreshes the pro-comp database by scraping VLR.gg
  data (via `pro_comp_importer.py`). Server owner or Stryke only. This is a
  large job (every VCT and VCL region, thousands of teams) and can take a
  long time to run — watch the progress bar it posts. If the bar stops
  moving after ~15 minutes, that's usually just Discord's edit token
  expiring, not the job actually stopping; check the bot's console for a
  final "Update complete" line to confirm it finished.
- **`/findprocomp`** — search for pro teams that have played a given
  5-agent composition on a map. Pick five agents (autocomplete helps avoid
  typos) and a map. Returns exact matches plus similar comps ranked by a
  similarity score (agents are compared by role and by what their kit
  actually does — flashes, smokes, recon, etc. — not just exact identity).
  Results include a link to the match. Two buttons appear on the result:
  - **Show all** — if there are more matches than fit in one message
  - **💾 Save to team** — pick one of the results and save it into your own
    team's comp book (see `/addcomp` below)

### Your team's comp book

- **`/addcomp`** — save a team's 5-agent comp for a specific map (comma
  separated list of agents).
- **`/comps`** — view every comp saved for a given team, across all maps.
  Includes a dropdown to pick a map and edit that comp directly (opens a
  pre-filled form).

### Scrims and matches

- **`/Creatematch`** — schedule a scrim, match, or VOD review. Stryke only.
- **`/matches`** — view the next 5 upcoming scrims/matches.
- **`/deletematch`** — remove a scheduled scrim/match. Stryke only.
- **`/matchresults`** — view recent match results. These are filled in
  either manually (via the "Submit Result" button the bot posts after a
  scheduled match ends) or automatically — see Premier auto-tracking below.

### Fun / misc (text commands, not slash — use the configured prefix)

`ping`, `cringe`, `mc`, `j`, `pwea`, `articulate`, `astra`, `yap`, `stryke2`

## Premier auto-tracking

Every 10 minutes, the bot checks your Premier team's match history (via
HenrikDev's API) for any newly completed matches and automatically adds them
to `matchResults.json` — no manual entry needed. This only works if your
team actually plays through Valorant Premier; it can't see custom-lobby
scrims, since Riot doesn't expose that data through any public API.

## Files the bot creates/manages

These live alongside the bot and aren't part of the source code — don't
worry if you don't see them in a fresh checkout, they're generated on first
run:

- `data/comps.db` — the pro-comp database (`/findprocomp` reads this)
- `comps.json` — your team's comp book (`/addcomp`, `/comps`)
- `scrims.json` — scheduled scrims/matches
- `matchResults.json` — match results, manual and Premier-auto-tracked
- `schedule.json` — used for the automatic "submit result" prompts
- `premier_seen_matches.json` — tracks which Premier matches have already
  been imported, so they aren't added twice

## Troubleshooting

- **"Application did not respond" on a slash command** — usually means an
  unhandled error happened before the bot could acknowledge the
  interaction. Check the console for the real exception.
- **`ModuleNotFoundError: No module named 'vlrdevapi'`** — either the
  package isn't installed for the Python version the bot is actually
  launching, or `pythonInterpreter` in `config.json` points at the wrong
  one. Confirm with:
  ```
  python3.11 -c "import vlrdevapi; print('ok')"
  ```
- **`table compositions has no column named ...`** — the `comps.db` file
  on disk was built with an older version of the importer script. Delete
  it and run `/updateprocomps` again to regenerate it with the current
  schema.
- **A slash command shows up twice in Discord** — usually leftover
  guild-scoped commands from earlier testing alongside the current global
  ones. Needs a one-time cleanup call to
  `BulkOverwriteGuildApplicationCommandsAsync` with an empty command list
  for the affected server.