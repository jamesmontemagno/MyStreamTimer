# My Stream Timer for Stream Deck

This folder contains the Stream Deck 2.x plugin built with Elgato's official TypeScript SDK.

## Prerequisites

- Node.js 24 or newer
- Stream Deck 7.1 or newer
- My Stream Timer installed for app-backed actions

## Develop

```bash
npm ci
npm run build
npm run link
npm run watch
```

Enable developer mode with `npx streamdeck dev` to inspect property inspectors at
`http://localhost:23654/`. Plugin logs are written to
`com.refractored.mystreamtimer.sdPlugin/logs`.

## Quality checks

```bash
npm run lint
npm run typecheck
npm test
npm run build
npm run validate
```

## Package

```bash
npm run pack
```

The installer is written to `dist`. Tagged releases use `v<version>-streamdeck`, for example
`v2.0.0-streamdeck`.

The release workflow reruns every quality check, stamps the four-part manifest version, packages
the installer, writes a SHA-256 checksum, and creates a GitHub Release. Upload that same installer
to [Elgato Maker Console](https://maker.elgato.com/) for Marketplace review; the official Stream
Deck CLI does not currently provide a documented Marketplace publish command.

## Actions

- **App Timer Start** controls any Countdown, Count Up, or Current Time output through the
  `mystreamtimer://` protocol.
- **App Timer Control** supports add/subtract minutes or seconds, pause, resume, reset, and
  stop. Current Time supports start and stop.
- **Stream Deck Start** starts or restarts a Countdown, Count Up, or Current Time text-file timer.
- **Stream Deck Control** controls the matching file timer by its configured output folder and file
  name. Countdown and Count Up support pause, resume, reset, and stop; Current Time supports start
  and stop.

File timer output defaults to `Documents/MyStreamTimerStreamDeck/countdown.txt`. The inspector's
**Copy output file path** control copies the precise resolved path. Runtime files are written outside
the packaged plugin so the installer remains compatible with Marketplace DRM integrity checks.

## Release

Create and push a release tag from the repository root:

```powershell
./scripts/create-release-tags.ps1 v2.0.0 -StreamDeck -Push
```

After the GitHub Release is created:

1. Install the attached `.streamDeckPlugin` on Windows and macOS.
2. Verify App Timer and Stream Deck actions.
3. Upload the same installer to Maker Console without selecting automatic publication.
4. Download and test the DRM-processed build, then submit it for Marketplace review.

## Clean-break upgrade

Version 2 uses a new official plugin UUID and action model. Existing buttons from the legacy
StreamDeckLib plugin must be removed and re-added.
