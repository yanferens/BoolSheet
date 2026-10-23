<a id="readme-top"></a>

<!-- ABOUT THE PROJECT -->
## About The Project

**BoolSheet** is a spreadsheet with logical expression evaluation: the 1st lab project (advanced level, variant 30)
for the OOP course at the Faculty of Computer Science and Cybernetics, Taras Shevchenko National University of Kyiv.

Every cell holds an expression; its value is an integer of any length, and `0` means *false* while any other
number, negative ones included, means *true*. The application includes:

- Expressions with `+ - * /`, `mod`, `div`, comparisons `= <> < > <= >=`, `not` and unary minus, parsed with ANTLR 4
- References between cells (`A1 + 5 > B2`) with automatic recalculation of dependent cells
- Detection of circular references and clear error messages with the position of a syntax error
- Adding and deleting rows and columns; references to moved cells are rewritten automatically
- Two display modes: values (`ІСТИНА` / `ХИБНІСТЬ` / numbers) or the expressions themselves
- Saving and opening `.boolsheet` files (JSON) on disk
- Saving and opening tables on Google Drive (a table can be kept on disk and on Drive at the same time)
- Ukrainian interface, light or dark theme following Windows
- Protection against hostile input: huge numbers, very long or deeply nested expressions, long reference chains

The application targets **Windows only**.

<p align="right">(<a href="#readme-top">back to top</a>)</p>



### Built With

* .NET 9.0
* .NET MAUI 9.0.120
* .NET MAUI Community Toolkit 12.3.0
* ANTLR 4.13.1 (Antlr4.Runtime.Standard, Antlr4BuildTasks 12.14.0)
* Google APIs Drive v3 1.77.0.4301 (with Google APIs Auth)
* xUnit 2.9.2

<p align="right">(<a href="#readme-top">back to top</a>)</p>



### Project Structure

```
BoolSheet.sln
├── BoolSheet.Core/      class library: grammar, parser, expression tree, grid model, storage, services
│   ├── Grammar/         BoolSheet.g4 (the parser is generated from it at build time)
│   ├── Ast/             expression tree (Interpreter pattern)
│   ├── Parsing/         ANTLR → expression tree, syntax errors, reference rewriting
│   ├── Model/           Grid, Cell, CellAddress
│   ├── Storage/         JSON file format, local files, cloud storage abstraction
│   └── Services/        SpreadsheetService: the single entry point for the UI
├── BoolSheet/           .NET MAUI application (Windows)
│   ├── ViewModels/      MVVM view models (no MAUI dependency, unit-tested)
│   ├── Cloud/           Google Drive storage
│   └── Resources/       styles, colours, fonts, icons
└── BoolSheet.Tests/     xUnit tests for the core and the view models
```

<p align="right">(<a href="#readme-top">back to top</a>)</p>



<!-- GETTING STARTED -->
## Getting Started

This section explains how to build and run BoolSheet on your own computer. It was developed and tested on
Windows 11 with Visual Studio 2022 and the versions listed above.

### Prerequisites

1. Windows 10 (version 1809) or newer.
2. Install the .NET 9.0 SDK: https://dotnet.microsoft.com/en-us/download/dotnet/9.0
3. Install the .NET MAUI workload:
   ```sh
   dotnet workload install maui
   ```
   Or, in Visual Studio 2022 (17.12 or newer), select the **.NET Multi-platform App UI development** workload
   in the Visual Studio Installer.
4. An internet connection for the first build: NuGet packages are restored, and Antlr4BuildTasks downloads
   the ANTLR tool (and a Java runtime if none is installed) to generate the parser from `BoolSheet.g4`.

### Installation

1. Clone the repo
   ```sh
   git clone https://github.com/yanferens/BoolSheet.git
   cd BoolSheet
   ```
2. Restore the packages
   ```sh
   dotnet restore
   ```
3. *(Optional — only for Google Drive)* Create your own OAuth client, see
   [Google Drive setup](#google-drive-setup). Without it the application works normally;
   only the Google Drive commands report that `client_secret.json` is missing.

<p align="right">(<a href="#readme-top">back to top</a>)</p>



### Google Drive Setup

The OAuth client secret is personal, so it is **not** in the repository (see `.gitignore`).

1. Open https://console.cloud.google.com and create a project.
2. **APIs & Services → Library** → enable **Google Drive API**.
3. **Google Auth Platform** → *Get started*: app name, your e-mail, audience **External**.
4. **Data Access → Add or remove scopes** → add `https://www.googleapis.com/auth/drive`.
5. **Audience → Test users** → add the Google accounts that will sign in.
   While the app is in *Testing* mode, only these accounts can use it, and the sign-in expires every 7 days.
6. **Clients → Create client** → type **Desktop app** → *Download JSON*.
7. Rename the file to `client_secret.json` and put it into `BoolSheet/Resources/Raw/`.

On the first Google Drive command a browser window opens for sign-in; Google may show
*"Google hasn't verified this app"* — choose **Advanced → Continue**. The tokens are stored locally, so later
commands work without signing in again. **File → Вийти з Google акаунта** forgets them.

<p align="right">(<a href="#readme-top">back to top</a>)</p>



<!-- USAGE EXAMPLES -->
## Usage

### Run

From Visual Studio: open `BoolSheet.sln`, set **BoolSheet** as the startup project, choose **Windows Machine**
and press **F5**.

From the command line:
```sh
dotnet run --project BoolSheet/BoolSheet.csproj -f net9.0-windows10.0.19041.0
```

The built application is in `BoolSheet/bin/<Debug|Release>/net9.0-windows10.0.19041.0/win10-x64/`
(`BoolSheet.exe`).

### Test

```sh
dotnet test BoolSheet.Tests/BoolSheet.Tests.csproj
```

The tests cover the parser, the evaluation rules, the grid (cycles, deleting rows and columns), the file format,
local and cloud storage (with an in-memory fake of Google Drive) and the view models.

### Expressions

| Example | Value |
|---|---|
| `2 + 3` | `5` |
| `A1 + 5 > B2` | `ІСТИНА` or `ХИБНІСТЬ` |
| `not (A1 mod 2 = 0)` | `ІСТИНА` if A1 is odd |
| `7 / 2`, `7 div 2`, `7 mod 2` | `3`, `3`, `1` |
| `-7 / 2`, `-7 div 2`, `-7 mod 2` | `-3`, `-4`, `1` |
| `-5`, `2 - -3`, `-(2 + 3) * 2` | `-5`, `5`, `-10` |
| `not -5`, `-5 <> 0` | `ХИБНІСТЬ`, `ІСТИНА` — a negative number is *true* |
| `A1 = not B1` | `not` inside an expression applies to one operand |

* An empty cell equals `0`. Comparisons and `not` give `1` (*true*) or `0` (*false*).
* `/` truncates toward zero, `div` rounds down, `mod` is consistent with `div`.
* A cell address is one letter `A`–`Z` and a row number; the sheet is at most 100 × 26.
* The full rules are in **Довідка → Як користуватися** (F1).

<p align="right">(<a href="#readme-top">back to top</a>)</p>



### Top contributors:

<a href="https://github.com/yanferens/BoolSheet/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=yanferens/BoolSheet" alt="contrib.rocks image" />
</a>

<!-- LICENSE -->
## License

Distributed under the MIT License. See `LICENSE` for more information.

<p align="right">(<a href="#readme-top">back to top</a>)</p>
