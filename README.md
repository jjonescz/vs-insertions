# VSInsertions

VS insertions dashboard for use by Roslyn and Razor teams.

## Usage

Run the tool (no install needed):

```
dnx vs-insertions
```

It starts a local web server on <http://localhost:47213> and opens the dashboard in your browser.
Pass `--no-browser` (or set `VSINSERTIONS_NO_BROWSER=1`) to skip launching the browser.

Authentication uses your local CLI credentials, so make sure you are signed in:
- Azure DevOps: install the [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) and run `az login`.
- GitHub: install the [GitHub CLI](https://cli.github.com/) and run `gh auth login`.

## Development

Open in Visual Studio and start the app (<kbd>F5</kbd>)
or in VSCode and run the build task (<kbd>Ctrl+Shift+B</kbd>).
