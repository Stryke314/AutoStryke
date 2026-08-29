import sqlite3
import time
import json
from datetime import date
from pathlib import Path

import vlrdevapi


VCT_REGIONS = ["americas", "emea", "pacific", "china"]
VCL_REGION = "all"  # VCL isn't split into continents like VCT is
DATABASE_PATH = Path(__file__).parent / "comps.db"
REQUEST_DELAY = 0.05


def create_database():
    connection = sqlite3.connect(DATABASE_PATH)
    connection.execute("""
        CREATE TABLE IF NOT EXISTS compositions (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            team TEXT NOT NULL,
            map TEXT NOT NULL,
            comp TEXT NOT NULL,
            match_link TEXT
        )
    """)
    connection.execute("""
        CREATE UNIQUE INDEX IF NOT EXISTS idx_unique_comp
        ON compositions(team, map, comp, match_link)
    """)
    connection.commit()
    return connection


def find_stage_2_events():
    events = {}

    for region in VCT_REGIONS:
        try:
            result = vlrdevapi.event.list(tier="vct", region=region, return_all=True)
        except Exception as error:
            print(f"Could not load VCT {region}: {error}")
            continue

        for event in result.events:
            name = event.name or ""
            if event.id is not None and "2026" in name and "Stage 2" in name:
                events[event.id] = event

    try:
        result = vlrdevapi.event.list(tier="vcl", region=VCL_REGION, return_all=True)
        for event in result.events:
            name = event.name or ""
            if event.id is None or "2026" not in name:
                continue
            if "Stage 2" not in name and "Split 2" not in name:
                continue
            events[event.id] = event
    except Exception as error:
        print(f"Could not load VCL events: {error}")

    return list(events.values())


def get_event_teams(event_id):
    try:
        teams = {}
        for stage in vlrdevapi.event(event_id).teams():
            for team in stage.teams:
                if team.id is not None:
                    teams[team.id] = team
        return list(teams.values())
    except Exception as error:
        print(f"Could not load event teams: {error}")
        return []


def get_team_stats(team_id, event_id):
    try:
        return vlrdevapi.team.stats(
            team_id=team_id,
            event_id=event_id,
            agent_composition="detailed",
            date_start=date(2026, 1, 1),
            date_end=date.today(),
        )
    except Exception as error:
        print(f"Could not load team statistics: {error}")
        return None


def update_database():
    connection = create_database()
    rows_added = 0

    print("Finding events...", flush=True)
    events = find_stage_2_events()

    # Build the full work list up front so we know the true total
    # (team count varies wildly between events - some VCL qualifiers
    # have 70+ teams, others have 8), giving an accurate progress bar
    # instead of one that jumps unevenly per event.
    work_items = []
    for event in events:
        for team in get_event_teams(event.id):
            work_items.append((event, team))

    total = len(work_items)
    print(f"TOTAL {total}", flush=True)

    for done, (event, team) in enumerate(work_items, start=1):

        stats = get_team_stats(team.id, event.id)

        if stats is not None:
            for map_stats in stats.maps or []:
                if not map_stats.map_name:
                    continue
                for composition in map_stats.compositions or []:
                    if not composition.agents:
                        continue
                    comp = " / ".join(sorted(composition.agents))
                    matches = composition.matches or []

                    if not matches:
                        cursor = connection.execute("""
                            INSERT OR IGNORE INTO compositions (team, map, comp, match_link)
                            VALUES (?, ?, ?, '')
                        """, (team.name, map_stats.map_name, comp))
                        rows_added += cursor.rowcount
                        continue

                    for match in matches:
                        link = f"https://www.vlr.gg/{match.series_id}" if match.series_id else ""
                        cursor = connection.execute("""
                            INSERT OR IGNORE INTO compositions (team, map, comp, match_link)
                            VALUES (?, ?, ?, ?)
                        """, (team.name, map_stats.map_name, comp, link))
                        rows_added += cursor.rowcount

            connection.commit()

        print(f"PROGRESS {done} {total} {event.name} :: {team.name}", flush=True)
        time.sleep(REQUEST_DELAY)

    total_rows = connection.execute("SELECT COUNT(*) FROM compositions").fetchone()[0]
    teams = connection.execute("SELECT COUNT(DISTINCT team) FROM compositions").fetchone()[0]
    connection.close()
    print(f"Update complete: {rows_added} new rows; {total_rows} rows across {teams} teams.", flush=True)


if __name__ == "__main__":
    update_database()