# Trombee — Jellyfin Plugin

Browse all the actors in your Jellyfin library on a single screen: a full grid with photos, appearance counts, and a direct link to their biography.

![Trombee screenshot](Jellyfin.Plugin.ActorsIndex/Images/thumb.png)

## Features

- **Actors grid** — all actors/actresses, sortable by number of appearances or name A→Z
- **Real-time search** — filter by name as you type
- **Pagination** — configurable page size (default 60 actors per page)
- **Floating button** — add a persistent 🎬 button on every Jellyfin page
- **Channel integration** — actors accessible as a Jellyfin channel on the home screen
- **Appearance filter** — hide actors with fewer than N appearances
- **Automatic update** — updates itself from the settings page, no manual build required

## Requirements

- Jellyfin **10.11.x** or later
- .NET 9 (included in Jellyfin 10.11+)

## Installation

### Method A — Custom repository (recommended)

1. In Jellyfin, go to **Dashboard → Plugins → Repositories**
2. Click **+** and add:
   - Name: `Trombee`
   - URL: `https://raw.githubusercontent.com/drbuju/Jellyfin.Plugin.Trombee/main/manifest.json`
3. Go to **Dashboard → Plugins → Catalog**, find **Trombee**, click **Install**
4. Restart Jellyfin

### Method B — Manual installation

1. Download the latest `Jellyfin.Plugin.Trombee_x.x.x.x.zip` from the [Releases](https://github.com/drbuju/Jellyfin.Plugin.Trombee/releases) page
2. Extract and copy all the files into Jellyfin's plugin folder:
   - **Linux**: `/var/lib/jellyfin/plugins/ActorsIndex/`
   - **Windows**: `%LOCALAPPDATA%\jellyfin\plugins\ActorsIndex\`
3. Restart Jellyfin

## Initial setup

After installing and restarting Jellyfin:

1. Go to **Dashboard → Plugins → Trombee → Settings**
2. In the **"Home Button"** section, click **🎬 Inject Home button**
   — this adds a persistent button on every Jellyfin page

> **Linux only:** the "Inject" feature requires write permission on `/usr/share/jellyfin/web/index.html`.
> To grant it permanently, run this command **once**:
> ```bash
> sudo bash -c 'mkdir -p /etc/systemd/system/jellyfin.service.d && printf "[Service]\nExecStartPre=+/bin/chown jellyfin /usr/share/jellyfin/web/index.html\n" > /etc/systemd/system/jellyfin.service.d/fix-webroot-perms.conf && systemctl daemon-reload && systemctl restart jellyfin'
> ```
> After a Jellyfin update, click **"Inject"** again to restore the button.

---

## What to do after a Jellyfin update

### The 🎬 button disappeared from the Home page

Jellyfin overwrote `index.html` during the update.

**Fix:** Dashboard → Plugins → Trombee → **"Inject Home button"**. Done.

---

### Red error when clicking "Inject" (or "Remove")

The plugin was built for a previous Jellyfin version and is no longer compatible.

**Fix in 4 clicks:**

1. Dashboard → Plugins → Trombee → **"Check for updates"**
2. If a newer version appears → click **"Download and install update"**
3. Click **"Restart Jellyfin"**
4. Click **"Inject Home button"**

✅ No terminal, no manual build.

---

### Error persists even after updating the plugin (Linux only)

After `apt upgrade jellyfin` on Debian/Ubuntu, `index.html` is owned by `root` again and the plugin can't write to it.

**Fix:** re-run the one-time command from the "Initial setup" section above. This only needs to be redone if you fully uninstall and reinstall Jellyfin.

---

## How automatic updates work

The plugin updates itself without you having to build anything:

1. When Jellyfin releases a new version, **Renovate** automatically opens a Pull Request on this repository to update dependencies
2. Once the PR is merged, **GitHub Actions** builds the plugin, creates a signed ZIP, and publishes a new [Release](https://github.com/drbuju/Jellyfin.Plugin.Trombee/releases)
3. You click **"Check for updates"** in the plugin's settings — everything else happens automatically

## Building from source (developers only)

```bash
git clone https://github.com/drbuju/Jellyfin.Plugin.Trombee.git
cd Jellyfin.Plugin.ActorsIndex
dotnet publish --configuration=Release Jellyfin.Plugin.ActorsIndex.sln
```

Output: `Jellyfin.Plugin.ActorsIndex/bin/Release/net9.0/publish/`

## License

[GPL-3.0](LICENSE)
