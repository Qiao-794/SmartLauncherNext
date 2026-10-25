# SmartLauncher release publisher

`publish.ps1` packages a clean release and uploads it to either GitHub Releases or an S3-compatible bucket.

## What goes into the zip

Only:

- `SmartLauncherNext.exe`
- `README.md`
- `version.txt`

It deliberately does **not** include:

- `launcher.json`
- `launcher.json.bak`
- `Cache\Icons`

So an update cannot overwrite the user's config or icons. The updater should extract only the exe/README and keep the existing config and cache.

## Dry run

Always test packaging first:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\publish.ps1 `
  -Target GitHub `
  -Repository owner/repo `
  -Version 1.3.2 `
  -DryRun
```

It writes:

```text
out\release\v1.3.2\
out\release\SmartLauncherNext-1.3.2.zip
out\release\latest.json
```

## GitHub Releases

Requirements:

- A **public** release repository
- A GitHub token with `Contents: Read and write`
- The token in an environment variable, never in the script or git

```powershell
$env:GITHUB_TOKEN = "<fine-grained-token>"

.\tools\publish.ps1 `
  -Target GitHub `
  -Repository owner/smart-launcher-next-releases `
  -Branch main `
  -Version 1.3.2
```

What it does:

1. Creates/uses tag `v1.3.2`
2. Creates or reuses the GitHub Release
3. Uploads `SmartLauncherNext-1.3.2.zip`
4. Uploads `latest.json` as a release asset
5. Updates `latest.json` in the repository root through the Contents API

The stable update endpoint is:

```text
https://raw.githubusercontent.com/owner/repo/main/latest.json
```

The zip URL in `latest.json` points to:

```text
https://github.com/owner/repo/releases/download/v1.3.2/SmartLauncherNext-1.3.2.zip
```

The release repository must be public. A private repository requires authentication, which is not suitable for an anonymous Quicker download.

## S3-compatible object storage

Requirements:

- AWS CLI installed and on PATH
- Public-read bucket or CDN
- Credentials in environment variables, not the script

Cloudflare R2 example:

```powershell
$env:AWS_ACCESS_KEY_ID = "<key>"
$env:AWS_SECRET_ACCESS_KEY = "<secret>"
$env:S3_BUCKET = "smartlauncher"

.\tools\publish.ps1 `
  -Target S3 `
  -BaseUrl "https://cdn.example.com" `
  -Bucket $env:S3_BUCKET `
  -Prefix "smartlauncher" `
  -Endpoint "https://<account-id>.r2.cloudflarestorage.com" `
  -Version 1.3.2
```

This uploads:

```text
s3://bucket/smartlauncher/SmartLauncherNext-1.3.2.zip
s3://bucket/smartlauncher/latest.json
```

and publishes:

```text
https://cdn.example.com/smartlauncher/SmartLauncherNext-1.3.2.zip
https://cdn.example.com/smartlauncher/latest.json
```

Cache headers:

- versioned zip: `public,max-age=31536000,immutable`
- `latest.json`: `no-cache`

The same `-Target S3` mode works with MinIO and other S3-compatible endpoints. Aliyun OSS / Tencent COS usually work better with their own CLI (`ossutil` / `coscli`); those are not included in this script yet.

## latest.json format

```json
{
  "version": "1.3.2",
  "url": "https://.../SmartLauncherNext-1.3.2.zip",
  "sha256": "...",
  "size": 61090,
  "publishedAt": "2026-10-25T02:03:29Z"
}
```

Quicker can:

1. Download `latest.json`
2. Compare `version` with the local `version.txt`
3. Download `url` only if different
4. Verify `sha256`
5. Stop `SmartLauncherNext.exe`
6. Extract to a temp folder
7. Replace `SmartLauncherNext.exe` and `README.md`
8. Preserve `launcher.json` and `Cache`
9. Start `SmartLauncherNext.exe --show`

## Security

- Never commit a token.
- Never paste a token into chat or a Quicker action.
- Use a fine-grained token limited to the release repository.
- Revoke the token if it leaks.

## Version rules

`-Version` is optional; it defaults to the exe's `ProductVersion`. The exe version and the requested version must match, otherwise the script refuses to publish.
