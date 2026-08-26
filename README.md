# AutoStryke

## Pro composition lookup

AutoStryke can maintain a local SQLite database of 2026 VCT and VCL Stage 2
agent compositions and search it from Discord.

Before running the bot, install the Python dependency used by the importer:

```text
pip install -r data/requirements.txt
```

The bot host needs Python on its `PATH`. If it uses a different executable,
set the `PRO_COMP_PYTHON` environment variable to its full path.

In Discord, the server owner runs `/updateprocomps` to refresh `comps.db`.
Anyone can then use `/findprocomp`, providing five comma-separated agents and
optionally a map. For example: `Jett, Omen, Sova, Killjoy, Viper`.
