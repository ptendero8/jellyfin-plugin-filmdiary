# Film Diary for Jellyfin

Rate your films from 0 to 5 stars (with half stars), mark the ones you love, log when you watched them and keep private notes — right inside Jellyfin's movie page. Export everything as a CSV that Letterboxd's importer accepts.

> Status: v0.1, tested on Jellyfin 12.2 (Windows).

## Features

- **Half-star ratings** (0.5–5), with mouse and keyboard (arrow keys).
- **Like** (heart), **watched date**, **rewatch** flag.
- **Notes / review** per film, autosaved.
- **Per user**: each Jellyfin user has their own diary.
- **Export to Letterboxd**: `Title, Year, imdbID, tmdbID, Rating, WatchedDate, Rewatch, Review`.
- No changes to Jellyfin's files on disk: the web script is injected in memory when `index.html` is served.

## Install (manual)

1. Build (see below) or download `Jellyfin.Plugin.FilmDiary.dll`.
2. Create `<jellyfin data>/plugins/Film Diary_0.1.1.0/` and copy the DLL and `meta.json` there
   (Windows: `C:\ProgramData\Jellyfin\Server\plugins\`).
3. Restart Jellyfin and reload the web client with Ctrl+F5.

## Build

Requires the .NET 10 SDK.

```bash
# Option A: against the DLLs of your own Jellyfin install (exact ABI match)
mkdir lib && cp "<jellyfin install>/"{MediaBrowser.Common,MediaBrowser.Controller,MediaBrowser.Model,Jellyfin.Data,Jellyfin.Extensions,Jellyfin.Database.Implementations}.dll lib/
dotnet build src/Jellyfin.Plugin.FilmDiary -c Release

# Option B: against the official NuGet packages (used automatically when lib/ is missing)
dotnet build src/Jellyfin.Plugin.FilmDiary -c Release
```

## API

All endpoints require a Jellyfin session except `client.js`.

| Method | Path | Description |
|---|---|---|
| GET | `/FilmDiary/Items/{itemId}` | Current user's entry for a film |
| POST | `/FilmDiary/Items/{itemId}` | Save `{ Rating, Liked, WatchedDate, Rewatch, Notes }` |
| DELETE | `/FilmDiary/Items/{itemId}` | Delete the entry |
| GET | `/FilmDiary/Entries` | Whole diary of the current user |
| GET | `/FilmDiary/Export/Letterboxd` | CSV for Letterboxd's importer |
| GET | `/FilmDiary/client.js` | Web client script |

Data is stored as one JSON file per user in `<jellyfin data>/data/filmdiary/`.

## Roadmap

- Diary page (list of everything you've logged), filters and stats.
- Show your rating on posters.
- Import from a Letterboxd export.
- Aspect ratio control in the player.

## License

GPL-2.0-or-later, like Jellyfin.
