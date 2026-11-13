# Eclipsed Client

<img src="Images/assets/eclipse-moon-textured.png" alt="Cratered eclipse moon" width="96">

Eclipsed Client is a portable Windows x64 launcher for the official Roblox desktop player. It combines public Roblox charts, a saved game library, and a NASA-themed Home screen in a single desktop app.

## Download, extract, and run

1. Download **EclipsedClient-Portable.zip** from [GitHub Releases](https://github.com/Itxbreeze/EclipsedClient/releases) when a release is available.
2. Extract the ZIP and keep the files in the **EclipsedClient** folder together.
3. Open **EclipsedClient.exe**. The package includes the .NET runtime.
4. Install the [official Roblox desktop player](https://www.roblox.com/download) separately if you need it.

## Features

- **Public Top Charts and library** — Browse popular experiences, pin favorites, and launch from saved games or a Roblox link. Look up public profiles and friend presence without signing in to Eclipsed Client.
- **Local playtime analytics** — See daily and weekly activity, streaks, peak hours, and frequently played experiences. The launcher reads Roblox logs for server information and can ping the connected endpoint on request.
- **Custom Discord Rich Presence** — Choose status text, game visibility, and a session timer. Presence is opt-in and uses your Discord application ID.
- **Screenshot gallery** — Browse, search, preview, and copy screenshots from your local Roblox Pictures folder.
- **NASA backdrops** — Switch between [Apollo Earthrise](https://www.nasa.gov/image-article/earth-rising-over-moons-horizon/), [Solar Eclipse Corona](https://science.nasa.gov/image-article/centers-and-facilities/goddard/2024-total-solar-eclipse-prediction-vs-reality/), and the [Hadley–Apennine lunar surface](https://www.nasa.gov/image-article/looking-forward-to-the-moon/). The photographs are bundled for offline use and credited in the launcher.

## Privacy and Roblox compatibility

**Zero trackers. Local storage.** Eclipsed Client has no analytics, crash reporting, or Roblox credential or cookie access. Profiles, settings, playtime, and the charts cache are stored in `%AppData%\EclipsedClient`. Screenshots stay in your Pictures folder. Public Roblox requests power charts, metadata, profile and presence lookups. If you enable Rich Presence, Discord displays the activity you choose to share.

**Official Roblox player.** Eclipsed Client is designed to respect [Roblox’s rules on modified clients](https://en.help.roblox.com/hc/en-us/articles/24275616578708-Anti-cheat-Messages). It launches the signed, unmodified desktop player and includes no FastFlag overrides, file patches, or injection. Roblox controls sign-in, experience access, and enforcement.

**Side-by-side coexistence.** Direct launch works independently of the Windows Roblox protocol handler. Settings provides separate opt-ins for default-handler registration and Windows URI fallback. Windows can route an enabled fallback through another launcher.

## Build from source

Install the Windows .NET 10 SDK, then run these commands in PowerShell.

```powershell
git clone --recurse-submodules https://github.com/Itxbreeze/EclipsedClient.git
cd EclipsedClient
./build-portable.ps1
```

The script produces `dist/EclipsedClient/` and `dist/EclipsedClient-Portable.zip`. Use `./Build/Publish.ps1` for the optional standalone single-file executable. GitHub Actions builds the portable ZIP on `main` and attaches it to a GitHub release when a version tag such as `v1.0.0` is pushed.

## License

Eclipsed Client is an independent [MIT-licensed](LICENSE) fork of [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap), originally created by pizzaboxer. Eclipsed Client is unaffiliated with Roblox Corporation, Bloxstrap Labs, and Moonsworth. See [third-party notices](THIRD-PARTY-NOTICES) for dependency attribution.

## Credits

Development assistance by [ChatGPT (OpenAI)](https://chatgpt.com/).
