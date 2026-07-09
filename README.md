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
- **Non-admin access** — optionally expose the actors page to all users (not just admins) via Plugin Pages, scoped to each user's library permissions

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

## Making the actors page available to non-admin users

By default, Jellyfin only exposes plugin pages to server administrators (via the Dashboard). Trombee's actors grid is a browsing feature meant for **all** users, so it can optionally be exposed outside the Dashboard using the community **Plugin Pages** plugin.

### Prerequisites

Install these two plugins (in this order) from their custom repository, **before** Trombee can register its user-facing page:

1. In Jellyfin, go to **Dashboard → Plugins → Repositories**, click **+**, and add:
   - Name: `IAmParadox27`
   - URL: `https://www.iamparadox.dev/jellyfin/plugins/manifest.json`
2. Go to **Dashboard → Plugins → Catalog** and install, in order:
   - **File Transformation**
   - **Plugin Pages**
3. Restart Jellyfin after each install (or once at the end, then verify both loaded successfully in **Dashboard → Logs**)

### How it works

Once both plugins are installed and Jellyfin has restarted, Trombee automatically registers its actors page with Plugin Pages on startup — no configuration needed on your part. You'll see a log line confirming it:

```
Trombee browse page registered with Plugin Pages successfully.
```

Any signed-in user (not just admins) can then reach the page from the **hamburger menu**, under the section Plugin Pages adds (shown alongside other user-facing plugin pages, e.g. "Modular Home" if you use Home Screen Sections). The page itself only shows actors from the libraries that user actually has access to — it respects the same library permissions and parental controls as the rest of Jellyfin.

The **Settings** button (⚙) inside the actors page is automatically hidden for non-admin users, and the underlying settings page — along with all maintenance actions (channel refresh, self-update, Home button injection) — is locked to administrators only, both in the interface and at the API level.

> If Plugin Pages isn't installed, the actors page remains reachable only through **Dashboard → Plugins → Trombee** (admin-only), exactly like before.

### Troubleshooting

- **"Page not found" right after installing Plugin Pages** — your browser likely cached an old version of the Jellyfin web client before Plugin Pages patched it. Do a hard refresh (Ctrl+Shift+R) or clear the site's cache, then try again.
- **Still not visible** — check **Dashboard → Logs** for a line containing `PluginPagesRegistrationService`; a warning there means Plugin Pages wasn't detected at Trombee's startup (double-check both prerequisite plugins are installed **and** Jellyfin was restarted after installing them, not before).

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
