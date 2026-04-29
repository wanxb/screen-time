# Screen Time Privacy and Security Notes

Screen Time stores only local, app-level usage data.

## Stored Locally

- App display name
- Process name
- Executable path
- Active seconds
- First and last used timestamps
- App category
- Reminder events
- User settings

Default location:

```text
%AppData%/ScreenTime/
```

## Not Collected

- Keyboard input
- Screenshots or screen recordings
- Chat or document content
- Browser URLs
- Cloud account data
- Website-level activity

## Security Notes

- JSON writes use atomic replacement to reduce corruption risk.
- Corrupt settings files are backed up before defaults are recreated.
- Startup integration uses the current user Run key only when enabled.
- The app does not install a service or attempt to prevent termination.