# VSInsertions

VS insertions dashboard for use by Roslyn and Razor teams.

## Development

Fill the following in `secrets.json` (get its path via `dotnet user-secrets --project src/VsInsertions list --verbose`):
- `GitHub:ClientId`
- `GitHub:ClientSecret`

Open in Visual Studio and press <kbd>F5</kbd> or if you prefer command line:

```ps1
dotnet watch -lp https --project src/VsInsertions
```
