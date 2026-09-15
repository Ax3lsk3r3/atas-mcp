# Contributing to ATAS MCP Bridge

Thank you for your interest in contributing to ATAS MCP Bridge.

---

## Development Setup

1. Fork and clone the repository.
2. Ensure you have:
   - .NET 8 SDK or higher (`dotnet --version`)
   - Python 3.10 or higher (`python --version`)
   - ATAS Platform (7.x) or ATAS X (8.x)
3. Install dependencies:
   ```bash
   pip install -r requirements.txt
   pip install -e .
   ```

---

## Building and Testing

1. To compile the C# addon and deploy to your local ATAS strategy folders:
   ```bash
   build.bat
   ```
2. To run the automated smoke test suite:
   ```bash
   python test_client.py
   ```
   All tests must pass before submitting a pull request.

---

## Code and Documentation Style

- **No Emojis:** This repository strictly enforces zero emojis in code comments, docstrings, commit messages, and documentation files.
- **Portability:** Never hardcode absolute personal paths (e.g. `C:\Users\<username>\...`) or private credentials. Use relative paths, environment variables, or standard placeholders like `<PATH_TO_ATAS_MCP>`.
- **Thread Safety:** Any code calling ATAS API functions must execute on the ATAS calculation thread inside `OnCalculate()`.

---

## Submitting Pull Requests

1. Create a feature branch (`git checkout -b feature/my-feature`).
2. Make your changes and run `python test_client.py`.
3. Commit with clear, descriptive commit messages.
4. Push to your fork and submit a Pull Request describing your changes.
