# Solarious

<img src="Images/assets/solarious.png" alt="Solarious eclipse logo" width="112">

**The privacy-first Roblox launcher.** It is a portable Windows x64 launcher for the official Roblox desktop player, with public charts, a saved game library, and NASA imagery.

## Download, extract, and run

1. Download **Solarious-Portable.zip** from [GitHub Releases](https://github.com/Itxbreeze/Solarious/releases) when a release is available.
2. Extract the ZIP and keep the files in the **Solarious** folder together.
3. Open **Solarious.exe**. The package includes the .NET runtime.
4. Install the [official Roblox desktop player](https://www.roblox.com/download) separately if you need it.

## Features

- **Public Top Charts and library** — Browse popular experiences, pin favorites, and launch from saved games or a Roblox link. Look up public profiles and friend presence without signing in to Solarious.
- **Local playtime analytics** — See daily and weekly activity, streaks, peak hours, and frequently played experiences. The launcher reads Roblox logs for server information and can ping the connected endpoint on request.
- **Custom Discord Rich Presence** — Choose status text, game visibility, and a session timer. Presence is opt-in and uses your Discord application ID.
- **Screenshot gallery** — Browse, search, preview, and copy screenshots from your local Roblox Pictures folder.
- **NASA backdrops** — Choose from [Apollo Earthrise](https://www.nasa.gov/image-article/earth-rising-over-moons-horizon/), [Solar Eclipse Corona](https://science.nasa.gov/image-article/centers-and-facilities/goddard/2024-total-solar-eclipse-prediction-vs-reality/), and the [Hadley–Apennine lunar surface](https://www.nasa.gov/image-article/looking-forward-to-the-moon/). The Solar Eclipse Corona is the default. All three photographs are bundled for offline use and credited in the launcher.

## Privacy and Roblox compatibility

**Zero trackers. Local storage.** Solarious has no telemetry, ads, crash reporting, or access to Roblox credentials or cookies. Profiles, settings, playtime, and the charts cache are stored in `%AppData%\Solarious`. Saved games, playtime history, and the local screenshot gallery work offline. Screenshots stay in your Pictures folder. Public Roblox requests power charts, metadata, profile and presence lookups. If you enable Rich Presence, Discord displays the activity you choose to share.

Settings → Privacy & Local Data lists the files Solarious creates and provides Open Folder and Clear Local Data. On first run, Solarious copies existing launcher data from the previous Eclipsed Client folder if the Solarious folder does not exist.

**Official Roblox player.** Solarious is designed to respect [Roblox’s rules on modified clients](https://en.help.roblox.com/hc/en-us/articles/24275616578708-Anti-cheat-Messages). It launches the signed, unmodified desktop player and includes no FastFlag overrides, file patches, or injection. Roblox controls sign-in, experience access, and enforcement.

**Side-by-side coexistence.** Direct launch works independently of the Windows Roblox protocol handler. Settings provides separate opt-ins for default-handler registration and Windows URI fallback. Windows can route an enabled fallback through another launcher.

## Build from source

Install the Windows .NET 10 SDK, then run these commands in PowerShell.

```powershell
git clone --recurse-submodules https://github.com/Itxbreeze/Solarious.git
cd Solarious
./build-portable.ps1
```

The script produces `dist/Solarious/` and `dist/Solarious-Portable.zip`. Use `./Build/Publish.ps1` for the optional standalone single-file executable. GitHub Actions builds the portable ZIP on `main` and attaches it to a GitHub release when a version tag such as `v1.0.0` is pushed.

## License

Solarious, formerly Eclipsed Client, is an independent [MIT-licensed](LICENSE) fork of [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap), originally created by pizzaboxer. Solarious is unaffiliated with Roblox Corporation, Bloxstrap Labs, and Moonsworth. See [third-party notices](THIRD-PARTY-NOTICES) for dependency attribution.

## Credits

Development assistance by [ChatGPT (OpenAI)](https://chatgpt.com/).
