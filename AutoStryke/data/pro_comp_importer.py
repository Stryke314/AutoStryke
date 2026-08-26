import sqlite3
import time
from pathlib import Path

import vlrdevapi


REGIONS = ["americas", "emea", "pacific", "china"]
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
            vod TEXT
        )
    """)
    connection.execute("""
        CREATE UNIQUE INDEX IF NOT EXISTS idx_unique_comp
        ON compositions(team, map, comp, vod)
    """)
    connection.commit()
    return connection


def find_stage_2_events():
    events = {}
    for tier in ("vct", "vcl"):
        for region in REGIONS:
            try:
                result = vlrdevapi.event.list(tier=tier, region=region, return_all=True)
            except Exception as error:
                print(f"Could not load {tier.upper()} {region}: {error}")
                continue

            for event in result.events:
                name = event.name or ""
                if event.id is not None and "2026" in name and "Stage 2" in name:
                    events[event.id] = event
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
        return vlrdevapi.team.stats(team_id=team_id, event_id=event_id, agent_composition="basic")
    except Exception as error:
        print(f"Could not load team statistics: {error}")
        return None


def update_database():
    connection = create_database()
    rows_added = 0

    for event in find_stage_2_events():
        print(f"Processing {event.name}")
        for team in get_event_teams(event.id):
            stats = get_team_stats(team.id, event.id)
            if stats is None:
                continue

            for map_stats in stats.maps or []:
                if not map_stats.map_name:
                    continue
                for composition in map_stats.compositions or []:
                    if not composition.agents:
                        continue
                    comp = " / ".join(sorted(composition.agents))
                    cursor = connection.execute("""
                        INSERT OR IGNORE INTO compositions (team, map, comp, vod)
                        VALUES (?, ?, ?, '')
                    """, (team.name, map_stats.map_name, comp))
                    rows_added += cursor.rowcount
            connection.commit()
            time.sleep(REQUEST_DELAY)

    total = connection.execute("SELECT COUNT(*) FROM compositions").fetchone()[0]
    teams = connection.execute("SELECT COUNT(DISTINCT team) FROM compositions").fetchone()[0]
    connection.close()
    print(f"Update complete: {rows_added} new rows; {total} rows across {teams} teams.")


if __name__ == "__main__":
    update_database()
