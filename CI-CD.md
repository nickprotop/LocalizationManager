# Using LRM in CI/CD

LRM integrates seamlessly into GitHub Actions, GitLab CI, Azure Pipelines, Jenkins, and other CI/CD platforms.

## Table of Contents

- [GitHub Actions](#github-actions)
- [GitLab CI](#gitlab-ci)
- [Azure Pipelines](#azure-pipelines)
- [Jenkins](#jenkins)
- [Exit Codes](#exit-codes)
- [Common Use Cases](#common-use-cases)

---

## GitHub Actions

### Using the Official LRM Action (Recommended)

The easiest way to integrate LRM is using the official GitHub Action:

```yaml
name: Validate Localizations

on: [push, pull_request]

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Validate .resx files
        uses: nickprotop/LocalizationManager@v0
        with:
          command: validate
          path: ./Resources

      - name: Check translation coverage
        uses: nickprotop/LocalizationManager@v0
        with:
          command: stats
          path: ./Resources
```

**Available inputs:**
- `command`: LRM command to run (validate, stats, view, export, etc.)
- `path`: Path to resource files (default: `.`)
- `args`: Additional arguments to pass to LRM
- `version`: LRM version to use (default: `latest`)

**Outputs:**
- `exit-code`: Command exit code (0 = success, 1 = validation failed)
- `output`: Full command output

### Manual Download in GitHub Actions

If you prefer to download the binary directly:

```yaml
- name: Download LRM
  run: |
    wget https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-linux-x64.tar.gz
    tar -xzf lrm-linux-x64.tar.gz
    chmod +x linux-x64/lrm

- name: Validate resources
  run: ./linux-x64/lrm validate --path ./Resources
```

---

## GitLab CI

```yaml
validate-translations:
  stage: test
  image: ubuntu:latest
  before_script:
    - apt-get update && apt-get install -y wget
    - wget https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-linux-x64.tar.gz
    - tar -xzf lrm-linux-x64.tar.gz
    - chmod +x linux-x64/lrm
  script:
    - ./linux-x64/lrm validate --path ./Resources
    - ./linux-x64/lrm stats --path ./Resources
  rules:
    - if: '$CI_PIPELINE_SOURCE == "merge_request_event"'
    - if: '$CI_COMMIT_BRANCH == "main"'
```

---

## Azure Pipelines

```yaml
- task: Bash@3
  displayName: 'Validate Localizations'
  inputs:
    targetType: 'inline'
    script: |
      wget https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-linux-x64.tar.gz
      tar -xzf lrm-linux-x64.tar.gz
      chmod +x linux-x64/lrm
      ./linux-x64/lrm validate --path $(Build.SourcesDirectory)/Resources
```

---

## Jenkins

```groovy
pipeline {
    agent any
    stages {
        stage('Validate Translations') {
            steps {
                sh '''
                    wget https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-linux-x64.tar.gz
                    tar -xzf lrm-linux-x64.tar.gz
                    chmod +x linux-x64/lrm
                    ./linux-x64/lrm validate --path ./Resources
                '''
            }
        }
    }
}
```

---

## Exit Codes

LRM uses standard exit codes for CI integration:
- `0` - Success (validation passed, command completed)
- `1` - Failure (validation failed, errors found)

**Example: Fail CI on validation errors:**
```bash
lrm validate --path ./Resources || exit 1
```

**Example: Continue on validation errors:**
```bash
lrm validate --path ./Resources || true
```

### JSON Format for CI/CD

Use `--format json` for commands to get structured output that's easy to parse in CI/CD pipelines:

```bash
# Validate and save results as JSON
lrm validate --format json > validation.json

# Parse validation results with jq
IS_VALID=$(lrm validate --format json | jq '.isValid')
ISSUE_COUNT=$(lrm validate --format json | jq '.totalIssues')

# Get statistics as JSON
lrm stats --format json > stats.json

# Parse specific values
TOTAL_KEYS=$(lrm stats --format json | jq '.statistics[0].totalKeys')

# Export in JSON format for processing
lrm export --format json -o translations.json
```

**Benefits of JSON format:**
- Easily parsable by scripts and tools (jq, Python, etc.)
- Structured data for reporting and dashboards
- Machine-readable for automation
- Can be stored as CI/CD artifacts for historical analysis

---

## Common Use Cases

### 1. Validate on Pull Requests

```yaml
name: Validate Translations

on: pull_request

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: nickprotop/LocalizationManager@v0
        with:
          command: validate
          path: ./Resources
```

### 2. Export Translations for Review

```yaml
- uses: nickprotop/LocalizationManager@v0
  with:
    command: export
    path: ./Resources
    args: -o translations.csv

- uses: actions/upload-artifact@v4
  with:
    name: translations
    path: translations.csv
```

### 3. Check Coverage Thresholds

```yaml
- name: Check translation coverage
  run: |
    # Use JSON format for programmatic parsing
    lrm stats --format json --path ./Resources > stats.json

    # Parse with jq to check coverage
    COVERAGE=$(jq '.statistics[] | select(.language == "Greek") | .coveragePercentage' stats.json)

    if (( $(echo "$COVERAGE < 90" | bc -l) )); then
      echo "Coverage too low: $COVERAGE%"
      exit 1
    fi

    echo "Coverage OK: $COVERAGE%"
```

### 4. Validate Multiple Resource Folders

```yaml
- name: Validate all resource folders
  run: |
    for dir in src/**/Resources; do
      echo "Validating $dir"
      lrm validate --path "$dir"
    done
```

### 5. Block Merges with Missing Translations

```yaml
name: Require Complete Translations

on: pull_request

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: nickprotop/LocalizationManager@v0
        with:
          command: validate
          path: ./Resources
        # This will fail the PR if validation finds issues
```

### 6. Weekly Translation Report

```yaml
name: Weekly Translation Report

on:
  schedule:
    - cron: '0 9 * * 1'  # Every Monday at 9 AM

jobs:
  report:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: nickprotop/LocalizationManager@v0
        with:
          command: stats
          path: ./Resources

      - uses: nickprotop/LocalizationManager@v0
        with:
          command: export
          path: ./Resources
          args: -o weekly-report.csv

      - uses: actions/upload-artifact@v4
        with:
          name: weekly-translation-report
          path: weekly-report.csv
```

---

## Platform-Specific Notes

### Windows Runners

For Windows runners in GitHub Actions, use the Windows binary:

```yaml
- name: Download LRM (Windows)
  run: |
    Invoke-WebRequest -Uri "https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-win-x64.zip" -OutFile "lrm.zip"
    Expand-Archive -Path "lrm.zip" -DestinationPath "."

- name: Validate resources
  run: .\win-x64\lrm.exe validate --path .\Resources
```

### ARM64 Runners

For ARM64 runners (e.g., AWS Graviton), use the ARM64 binaries:

```yaml
- name: Download LRM (ARM64)
  run: |
    wget https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-linux-arm64.tar.gz
    tar -xzf lrm-linux-arm64.tar.gz
    chmod +x linux-arm64/lrm
```

---

## Troubleshooting

**Binary not found:**
```bash
# Make sure the binary is executable
chmod +x linux-x64/lrm

# Check if it's in PATH
which lrm

# Run with full path if needed
./linux-x64/lrm validate --path ./Resources
```

**Permission denied:**
```bash
# Ensure execute permissions
chmod +x linux-x64/lrm
```

**Wrong architecture:**
```bash
# Check your runner architecture
uname -m

# x86_64 = use linux-x64 or win-x64
# aarch64 / arm64 = use linux-arm64 or win-arm64
```

---

For more information, see:
- [GitHub Action Source](https://github.com/nickprotop/LocalizationManager/blob/main/action.yml)
- [Installation Guide](INSTALLATION.md)
- [Command Reference](COMMANDS.md)
- [Contributing Guide](CONTRIBUTING.md)
