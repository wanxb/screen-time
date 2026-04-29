# Screen Time Threat Model

## Assets

- Local usage records under `%AppData%/ScreenTime/`.
- User settings.
- Startup registry entry.
- App executable and generated tray icon frames.

## Trust Boundaries

- Windows foreground window and process APIs.
- Local filesystem.
- HKCU startup registry key.
- Optional user-provided icon frames in the assets directory.

## Main Risks

- Local data disclosure if another local process reads AppData.
- Corrupt JSON causing settings or usage loss.
- Malformed icon assets causing tray icon load failures.
- Startup entry pointing to an outdated executable after relocation.

## Mitigations

- Keep all data local and document exactly what is stored.
- Use atomic writes and backup corrupt JSON.
- Treat icon assets as optional and fall back to generated frames.
- Do not collect content, screenshots, browser URLs, or keystrokes.
- Do not run elevated or install a privileged service.