# Count Things!

A friendly Umbraco backoffice dashboard that shows counts of content, media, users, schema, forms and more.  

## Features
- Counts Content (published, unpublished, trashed, redirects)
- Counts Media (folders, images, videos, large files, other files, by file type)
- Counts Users, groups, members
- Counts Schema (doctypes, templates, partials, etc.)
- Counts Umbraco Forms (forms + entries)
- Export all counts to CSV for reporting

## Compatibility
- ✅ Umbraco 13, 14, 15, 16, 17
- ✅ .NET 8.0
- ✅ .NET 9.0
- ✅ .NET 10.0

## Installation
- Install via Nuget https://www.nuget.org/packages/DigitalWonderlab.CountThings/latest
- Go to Settings and click the Count Things tab
- Counts are stored in local storage for faster loading.

## Security (1.5.0)
- **Upgrade to 1.5.0.** Up to 1.4.4 the Count Things API (`/umbraco/api/countthings/*`) did not require a backoffice login, so anyone who knew the address could read a site's counts (users, members, document type aliases, media sizes, Forms entry counts). No names, emails or content were exposed. 1.5.0 requires a signed-in backoffice user for every request.
- The confetti script loaded from jsDelivr is now pinned with an integrity hash.
- Release builds no longer embed the build machine's file path inside the DLLs.

## Telemetry
Once a day Count Things sends Digital Wonderlab a small anonymous report so we know how many sites use it and on which versions. It is on by default and easy to turn off.

- **Sent:** a random install ID (created on first run, stored in your Umbraco database, not derived from anything about your site), the package, Umbraco and .NET versions, environment type, hosting type, database type, the site's rough size in bands such as `100-999` (content, media, members, users, document types), whether Umbraco Forms is installed, and how many times counts were run in the last day (banded).
- **Never sent:** hostnames, URLs, content, names, emails or IP addresses.
- **See it / turn it off:** the dashboard's **Settings** tab shows exactly what is sent and has an on/off switch. Or set `"CountThings": { "Telemetry": { "Enabled": false } }`, or `"DigitalWonderlab": { "Telemetry": { "Enabled": false } }` for every Digital Wonderlab package.
- Local development (`ASPNETCORE_ENVIRONMENT=Development`) never sends anything. Data is kept for 13 months.

## Issues / Suggestions
- To report an issue or suggest a new count or feature to be added please use the GitHub issue tracker - https://github.com/dwlkev/DigitalWonderlab.CountThings/issues

## Screenshots
![Dashboard](https://i.postimg.cc/Pxyx6M1y/ct-ss1.png)