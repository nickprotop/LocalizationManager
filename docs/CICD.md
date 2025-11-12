# CI/CD Automation

Automate your localization workflow with continuous integration and deployment pipelines. LocalizationManager integrates seamlessly with GitHub Actions, GitLab CI, Azure DevOps, and other CI/CD platforms.

## Table of Contents

- [Overview](#overview)
- [Quick Start](#quick-start)
- [GitHub Actions](#github-actions)
- [GitLab CI](#gitlab-ci)
- [Azure DevOps](#azure-devops)
- [Jenkins](#jenkins)
- [Shell Scripts](#shell-scripts)
- [JSON Format for CI/CD](#json-format-for-cicd)
- [Configuration File Support](#configuration-file-support)
- [Platform-Specific Notes](#platform-specific-notes)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)

## Overview

### The Automated Workflow

```
┌─────────────────────────────────────────────────────────────┐
│  1. Validate All Keys        ✓ Check XML & key consistency  │
│  2. Check Missing             → Identify untranslated keys   │
│  3. Auto-Translate            🌐 Fill with AI translation    │
│  4. Re-validate               ✓ Ensure quality              │
│  5. Report & Commit           📊 Track changes              │
└─────────────────────────────────────────────────────────────┘
```

### Benefits

- **🚀 Zero Manual Work**: Translations happen automatically on every commit
- **✅ Quality Assured**: Validation runs before and after translation
- **📊 Full Visibility**: Detailed reports show exactly what was translated
- **💰 Cost Efficient**: Only translates missing keys, uses caching
- **🔒 Secure**: API keys managed through CI/CD secrets

## Quick Start

### Installation Methods

#### Option 1: Official GitHub Action (Recommended for GitHub Actions)

```yaml
- name: Validate .resx files
  uses: nickprotop/LocalizationManager@v0
  with:
    command: validate
    path: ./Resources
```

**Available inputs:**
- `command`: LRM command to run (validate, stats, view, translate, etc.)
- `path`: Path to resource files (default: `.`)
- `args`: Additional arguments to pass to LRM
- `version`: LRM version to use (default: `latest`)

**Outputs:**
- `exit-code`: Command exit code (0 = success, 1 = validation failed)
- `output`: Full command output

#### Option 2: Manual Binary Download

For all CI/CD platforms (GitHub Actions, GitLab CI, Azure DevOps, Jenkins):

```bash
# Download the latest release
wget https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-linux-x64.tar.gz
tar -xzf lrm-linux-x64.tar.gz
chmod +x linux-x64/lrm

# Run commands
./linux-x64/lrm validate --path ./Resources
```

**Available platforms:**
- `lrm-linux-x64.tar.gz` - Intel/AMD Linux
- `lrm-linux-arm64.tar.gz` - ARM Linux (Raspberry Pi, AWS Graviton)
- `lrm-win-x64.zip` - Intel/AMD Windows
- `lrm-win-arm64.zip` - ARM Windows

### Prerequisites

1. Translation provider API key (Google, DeepL, or LibreTranslate)
2. Resource files (`.resx`) in your repository
3. API key configured as CI/CD secret

### Basic Flow

```bash
# 1. Validate
lrm validate

# 2. Check for missing
lrm validate --missing-only --format json > missing.json

# 3. Translate if needed
if [ -s missing.json ]; then
  lrm translate --only-missing --provider google
fi

# 4. Re-validate
lrm validate
```

### Enhanced Flow with Code Scanning

```bash
# 1. Validate .resx files
lrm validate

# 2. Scan code for missing keys
lrm scan --format json > scan-results.json

# 3. Add missing keys found in code
if [ -s scan-results.json ]; then
  missing_keys=$(jq -r '.missingKeys[]?.key // empty' scan-results.json)
  for key in $missing_keys; do
    lrm add --key "$key" --value "$key" --comment "Auto-added from code scan"
  done
fi

# 4. Translate missing keys
lrm translate --only-missing --provider google

# 5. Re-validate
lrm validate
```

### Exit Codes

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

## GitHub Actions

### Using the Official LRM Action (Recommended)

The easiest way to integrate LRM into GitHub Actions is using the official action:

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

### Complete Workflow with Code Scanning

Create `.github/workflows/auto-translate-with-scan.yml`:

```yaml
name: Auto-Translate with Code Scan

on:
  push:
    branches: [ main, develop ]
    paths:
      - 'Resources/**/*.resx'
      - 'src/**/*.cs'
      - 'src/**/*.razor'
  pull_request:
    paths:
      - 'Resources/**/*.resx'
      - 'src/**/*.cs'
      - 'src/**/*.razor'
  workflow_dispatch:

jobs:
  scan-and-translate:
    runs-on: ubuntu-latest
    permissions:
      contents: write
      pull-requests: write

    steps:
      - name: 📥 Checkout Repository
        uses: actions/checkout@v4
        with:
          token: ${{ secrets.GITHUB_TOKEN }}

      - name: 🔧 Download LRM
        run: |
          wget https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-linux-x64.tar.gz
          tar -xzf lrm-linux-x64.tar.gz
          chmod +x linux-x64/lrm
          echo "${{ github.workspace }}/linux-x64" >> $GITHUB_PATH

      - name: ✅ Step 1 - Validate .resx Files
        id: validate
        run: |
          echo "### 🔍 Validation Report" >> $GITHUB_STEP_SUMMARY
          lrm validate -p ./Resources || {
            echo "❌ Validation failed" >> $GITHUB_STEP_SUMMARY
            exit 1
          }
          echo "✅ All resource files are valid" >> $GITHUB_STEP_SUMMARY

      - name: 🔎 Step 2 - Scan Code for Missing Keys
        id: scan
        run: |
          lrm scan -p ./Resources --source-path ./src --format json > scan-results.json
          cat scan-results.json

          missing_count=$(jq -r '.summary.missingKeys // 0' scan-results.json)
          echo "missing_keys_count=$missing_count" >> $GITHUB_OUTPUT

          echo "" >> $GITHUB_STEP_SUMMARY
          echo "### 🔍 Code Scan Results" >> $GITHUB_STEP_SUMMARY

          if [ "$missing_count" -eq "0" ]; then
            echo "✨ No missing keys found in code!" >> $GITHUB_STEP_SUMMARY
          else
            echo "Found **$missing_count** keys used in code but missing from .resx:" >> $GITHUB_STEP_SUMMARY
            echo "" >> $GITHUB_STEP_SUMMARY
            echo "<details>" >> $GITHUB_STEP_SUMMARY
            echo "<summary>View missing keys</summary>" >> $GITHUB_STEP_SUMMARY
            echo "" >> $GITHUB_STEP_SUMMARY
            jq -r '.missingKeys[]? | "- `\(.key)` (\(.referenceCount) refs) - \(.references[0].file):\(.references[0].line)"' scan-results.json >> $GITHUB_STEP_SUMMARY
            echo "" >> $GITHUB_STEP_SUMMARY
            echo "</details>" >> $GITHUB_STEP_SUMMARY
          fi

      - name: ➕ Step 3 - Add Missing Keys from Code
        if: steps.scan.outputs.missing_keys_count != '0'
        run: |
          echo "" >> $GITHUB_STEP_SUMMARY
          echo "### ➕ Adding Missing Keys" >> $GITHUB_STEP_SUMMARY

          added_count=0
          while IFS= read -r key; do
            if [ -n "$key" ]; then
              lrm add -p ./Resources --key "$key" --value "$key" --comment "Auto-added from code scan" || true
              added_count=$((added_count + 1))
            fi
          done < <(jq -r '.missingKeys[]?.key // empty' scan-results.json)

          echo "Added **$added_count** new keys to resource files" >> $GITHUB_STEP_SUMMARY

      - name: 🔎 Step 4 - Check for Untranslated Keys
        id: check_missing
        run: |
          lrm validate -p ./Resources --missing-only --format json > missing.json
          cat missing.json

          missing_count=$(jq -r '.missingCount // 0' missing.json)
          echo "translation_missing_count=$missing_count" >> $GITHUB_OUTPUT

          echo "" >> $GITHUB_STEP_SUMMARY
          echo "### 📊 Untranslated Keys" >> $GITHUB_STEP_SUMMARY

          if [ "$missing_count" -eq "0" ]; then
            echo "✨ No missing translations!" >> $GITHUB_STEP_SUMMARY
          else
            echo "Found **$missing_count** keys needing translation:" >> $GITHUB_STEP_SUMMARY
            echo "" >> $GITHUB_STEP_SUMMARY
            jq -r '.languages[]? | "- **\(.code)**: \(.missingCount) keys"' missing.json >> $GITHUB_STEP_SUMMARY
          fi

      - name: 🌐 Step 5 - Auto-Translate Missing Keys
        if: steps.check_missing.outputs.translation_missing_count != '0'
        env:
          LRM_GOOGLE_API_KEY: ${{ secrets.GOOGLE_TRANSLATE_API_KEY }}
        run: |
          lrm translate -p ./Resources --only-missing --provider google --format json > translation-results.json

          echo "" >> $GITHUB_STEP_SUMMARY
          echo "### 🤖 Translation Results" >> $GITHUB_STEP_SUMMARY
          echo "" >> $GITHUB_STEP_SUMMARY

          jq -r '
            .translations // [] |
            group_by(.language) |
            map({
              language: .[0].language,
              translated: map(select(.status == "success" or .status == "✓")) | length,
              total: length
            }) |
            .[] | "- **\(.language)**: \(.translated)/\(.total) keys translated"
          ' translation-results.json >> $GITHUB_STEP_SUMMARY

      - name: ✅ Step 6 - Final Validation
        run: |
          echo "" >> $GITHUB_STEP_SUMMARY
          echo "### 🔄 Final Validation" >> $GITHUB_STEP_SUMMARY
          lrm validate -p ./Resources || {
            echo "⚠️ Validation failed after changes" >> $GITHUB_STEP_SUMMARY
            exit 1
          }
          echo "✅ All validation checks passed" >> $GITHUB_STEP_SUMMARY

      - name: 📋 Step 7 - Final Status
        run: |
          lrm scan -p ./Resources --source-path ./src --format json > final-scan.json
          lrm validate -p ./Resources --missing-only --format json > final-missing.json

          missing_in_code=$(jq -r '.summary.missingKeys // 0' final-scan.json)
          missing_translations=$(jq -r '.missingCount // 0' final-missing.json)

          echo "" >> $GITHUB_STEP_SUMMARY
          echo "### ✨ Final Status" >> $GITHUB_STEP_SUMMARY
          echo "" >> $GITHUB_STEP_SUMMARY
          echo "- Keys missing from .resx (found in code): **$missing_in_code**" >> $GITHUB_STEP_SUMMARY
          echo "- Keys needing translation: **$missing_translations**" >> $GITHUB_STEP_SUMMARY

          if [ "$missing_in_code" -eq "0" ] && [ "$missing_translations" -eq "0" ]; then
            echo "" >> $GITHUB_STEP_SUMMARY
            echo "🎉 **Perfect!** All keys synchronized and translated!" >> $GITHUB_STEP_SUMMARY
          fi

      - name: 📦 Upload Reports
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: translation-reports
          path: |
            scan-results.json
            missing.json
            translation-results.json
            final-scan.json
            final-missing.json
          retention-days: 30

      - name: 💾 Commit Changes
        if: (steps.scan.outputs.missing_keys_count != '0' || steps.check_missing.outputs.translation_missing_count != '0') && github.event_name != 'pull_request'
        run: |
          git config user.name "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"

          git add Resources/**/*.resx

          commit_msg="🌐 Auto-sync and translate localization keys\n\n"

          if [ "${{ steps.scan.outputs.missing_keys_count }}" != "0" ]; then
            commit_msg+="Added ${{ steps.scan.outputs.missing_keys_count }} keys found in code\n"
          fi

          if [ "${{ steps.check_missing.outputs.translation_missing_count }}" != "0" ]; then
            commit_msg+="Translated ${{ steps.check_missing.outputs.translation_missing_count }} missing keys\n"
          fi

          commit_msg+="\n🤖 Generated by LocalizationManager"

          git commit -m "$commit_msg" || echo "No changes to commit"
          git push
```

### Complete Workflow with Tracking (Original)

Create `.github/workflows/auto-translate.yml`:

```yaml
name: Auto-Translate Localization

on:
  push:
    branches: [ main, develop ]
    paths:
      - 'Resources/**/*.resx'
  pull_request:
    paths:
      - 'Resources/**/*.resx'
  workflow_dispatch:

jobs:
  validate-and-translate:
    runs-on: ubuntu-latest
    permissions:
      contents: write
      pull-requests: write

    steps:
      - name: 📥 Checkout Repository
        uses: actions/checkout@v4
        with:
          token: ${{ secrets.GITHUB_TOKEN }}

      - name: 🔧 Download LRM
        run: |
          wget https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-linux-x64.tar.gz
          tar -xzf lrm-linux-x64.tar.gz
          chmod +x linux-x64/lrm
          echo "${{ github.workspace }}/linux-x64" >> $GITHUB_PATH

      - name: ✅ Step 1 - Initial Validation
        id: validate
        run: |
          echo "### 🔍 Validation Report" >> $GITHUB_STEP_SUMMARY
          lrm validate || {
            echo "❌ Validation failed" >> $GITHUB_STEP_SUMMARY
            exit 1
          }
          echo "✅ All resource files are valid" >> $GITHUB_STEP_SUMMARY

      - name: 🔎 Step 2 - Check for Missing Translations
        id: check_missing
        run: |
          lrm validate --missing-only --format json > missing.json
          cat missing.json

          missing_count=$(jq -r '.missingCount // 0' missing.json)
          echo "missing_count=$missing_count" >> $GITHUB_OUTPUT

          echo "" >> $GITHUB_STEP_SUMMARY
          echo "### 📊 Missing Translations" >> $GITHUB_STEP_SUMMARY

          if [ "$missing_count" -eq "0" ]; then
            echo "✨ No missing translations!" >> $GITHUB_STEP_SUMMARY
          else
            echo "Found **$missing_count** missing translations:" >> $GITHUB_STEP_SUMMARY
            echo "" >> $GITHUB_STEP_SUMMARY
            jq -r '.languages[]? | "- **\(.code)**: \(.missingCount) keys"' missing.json >> $GITHUB_STEP_SUMMARY
          fi

      - name: 🌐 Step 3 - Auto-Translate Missing Keys
        if: steps.check_missing.outputs.missing_count != '0'
        env:
          LRM_GOOGLE_API_KEY: ${{ secrets.GOOGLE_TRANSLATE_API_KEY }}
          # Or use other providers:
          # LRM_DEEPL_API_KEY: ${{ secrets.DEEPL_API_KEY }}
          # LRM_LIBRETRANSLATE_API_KEY: ${{ secrets.LIBRETRANSLATE_API_KEY }}
        run: |
          lrm translate --only-missing --provider google --format json > results.json

          echo "" >> $GITHUB_STEP_SUMMARY
          echo "### 🤖 Translation Results" >> $GITHUB_STEP_SUMMARY
          echo "" >> $GITHUB_STEP_SUMMARY

          # Summary by language
          jq -r '
            .translations // [] |
            group_by(.language) |
            map({
              language: .[0].language,
              translated: map(select(.status == "success" or .status == "✓")) | length,
              total: length
            }) |
            .[] | "- **\(.language)**: \(.translated)/\(.total) keys translated"
          ' results.json >> $GITHUB_STEP_SUMMARY

          echo "" >> $GITHUB_STEP_SUMMARY
          echo "<details>" >> $GITHUB_STEP_SUMMARY
          echo "<summary>📝 View all translated keys</summary>" >> $GITHUB_STEP_SUMMARY
          echo "" >> $GITHUB_STEP_SUMMARY
          jq -r '.translations // [] | .[] | "- [\(.language)] `\(.key)`"' results.json >> $GITHUB_STEP_SUMMARY
          echo "" >> $GITHUB_STEP_SUMMARY
          echo "</details>" >> $GITHUB_STEP_SUMMARY

      - name: ✅ Step 4 - Re-validate After Translation
        if: steps.check_missing.outputs.missing_count != '0'
        run: |
          echo "" >> $GITHUB_STEP_SUMMARY
          echo "### 🔄 Post-Translation Validation" >> $GITHUB_STEP_SUMMARY
          lrm validate || {
            echo "⚠️ Validation failed after translation" >> $GITHUB_STEP_SUMMARY
            exit 1
          }
          echo "✅ Validation passed" >> $GITHUB_STEP_SUMMARY

      - name: 📋 Step 5 - Final Status Check
        run: |
          lrm validate --missing-only --format json > final.json

          remaining=$(jq -r '.missingCount // 0' final.json)

          echo "" >> $GITHUB_STEP_SUMMARY
          echo "### ✨ Final Status" >> $GITHUB_STEP_SUMMARY

          if [ "$remaining" -eq "0" ]; then
            echo "🎉 All translations completed successfully!" >> $GITHUB_STEP_SUMMARY
          else
            echo "⚠️ **$remaining** translations still missing:" >> $GITHUB_STEP_SUMMARY
            jq -r '.missing // [] | .[] | "- [\(.language)] `\(.key)`"' final.json >> $GITHUB_STEP_SUMMARY
          fi

      - name: 📦 Upload Translation Reports
        if: steps.check_missing.outputs.missing_count != '0'
        uses: actions/upload-artifact@v4
        with:
          name: translation-reports
          path: |
            missing.json
            results.json
            final.json
          retention-days: 30

      - name: 💾 Commit Translated Files
        if: steps.check_missing.outputs.missing_count != '0' && github.event_name != 'pull_request'
        run: |
          git config user.name "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"

          git add Resources/**/*.resx

          # Create detailed commit message
          commit_msg="🌐 Auto-translate missing keys\n\n"
          commit_msg+="Translation summary:\n"
          commit_msg+=$(jq -r '
            .translations // [] |
            group_by(.language) |
            map("- \(.[0].language): \(length) keys") |
            .[]
          ' results.json)
          commit_msg+="\n\n🤖 Generated by LocalizationManager"

          git commit -m "$commit_msg" || echo "No changes to commit"
          git push
```

### Pull Request Comments

Add this step to post translation results as PR comments:

```yaml
      - name: 💬 Comment on PR
        if: steps.check_missing.outputs.missing_count != '0' && github.event_name == 'pull_request'
        uses: actions/github-script@v7
        with:
          script: |
            const fs = require('fs');
            const results = JSON.parse(fs.readFileSync('results.json', 'utf8'));

            const summary = results.translations
              .reduce((acc, t) => {
                if (!acc[t.language]) acc[t.language] = 0;
                acc[t.language]++;
                return acc;
              }, {});

            const comment = `## 🌐 Auto-Translation Results

            ${Object.entries(summary)
              .map(([lang, count]) => `- **${lang}**: ${count} keys translated`)
              .join('\n')}

            <details>
            <summary>View Details</summary>

            ${results.translations.map(t => `- [${t.language}] \`${t.key}\``).join('\n')}

            </details>
            `;

            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body: comment
            });
```

## GitLab CI

Create `.gitlab-ci.yml`:

```yaml
stages:
  - validate
  - translate
  - commit

variables:
  LRM_VERSION: "latest"

validate:
  stage: validate
  image: ubuntu:latest
  before_script:
    - apt-get update && apt-get install -y wget jq
    - wget https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-linux-x64.tar.gz
    - tar -xzf lrm-linux-x64.tar.gz
    - chmod +x linux-x64/lrm
    - export PATH="$PWD/linux-x64:$PATH"
  script:
    - lrm validate
    - lrm validate --missing-only --format json > missing.json
  artifacts:
    reports:
      dotenv: missing.json
    paths:
      - missing.json
    expire_in: 1 day
  rules:
    - if: '$CI_PIPELINE_SOURCE == "merge_request_event"'
    - if: '$CI_COMMIT_BRANCH == "main"'

auto-translate:
  stage: translate
  image: ubuntu:latest
  dependencies:
    - validate
  before_script:
    - apt-get update && apt-get install -y wget jq
    - wget https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-linux-x64.tar.gz
    - tar -xzf lrm-linux-x64.tar.gz
    - chmod +x linux-x64/lrm
    - export PATH="$PWD/linux-x64:$PATH"
  script:
    - |
      if [ -s missing.json ]; then
        missing_count=$(jq -r '.missingCount // 0' missing.json)
        if [ "$missing_count" -gt "0" ]; then
          echo "Found $missing_count missing translations"
          lrm translate --only-missing --provider google --format json > results.json
          cat results.json
        fi
      fi
    - lrm validate
  artifacts:
    paths:
      - results.json
      - Resources/**/*.resx
    expire_in: 1 day
  only:
    - main
    - develop

commit-changes:
  stage: commit
  image: ubuntu:latest
  dependencies:
    - auto-translate
  before_script:
    - apt-get update && apt-get install -y git jq
  script:
    - |
      if [ -f results.json ]; then
        git config user.name "GitLab CI"
        git config user.email "ci@gitlab.com"
        git add Resources/**/*.resx

        commit_msg="🌐 Auto-translate missing keys\n\n"
        commit_msg+=$(jq -r '
          .translations // [] |
          group_by(.language) |
          map("- \(.[0].language): \(length) keys") |
          .[]
        ' results.json)

        git commit -m "$commit_msg" || echo "No changes"
        git push "https://oauth2:${CI_PUSH_TOKEN}@${CI_SERVER_HOST}/${CI_PROJECT_PATH}.git" HEAD:${CI_COMMIT_REF_NAME}
      fi
  only:
    - main
```

## Azure DevOps

Create `azure-pipelines.yml`:

```yaml
trigger:
  branches:
    include:
      - main
      - develop
  paths:
    include:
      - Resources/**/*.resx

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: Bash@3
  displayName: 'Download LRM'
  inputs:
    targetType: 'inline'
    script: |
      wget https://github.com/nickprotop/LocalizationManager/releases/latest/download/lrm-linux-x64.tar.gz
      tar -xzf lrm-linux-x64.tar.gz
      chmod +x linux-x64/lrm
      echo "##vso[task.prependpath]$(Build.SourcesDirectory)/linux-x64"

- script: |
    lrm validate
  displayName: 'Step 1 - Validate Resources'

- script: |
    lrm validate --missing-only --format json > $(Build.ArtifactStagingDirectory)/missing.json
    cat $(Build.ArtifactStagingDirectory)/missing.json
  displayName: 'Step 2 - Check Missing Translations'

- script: |
    if [ -s $(Build.ArtifactStagingDirectory)/missing.json ]; then
      missing_count=$(jq -r '.missingCount // 0' $(Build.ArtifactStagingDirectory)/missing.json)
      if [ "$missing_count" -gt "0" ]; then
        lrm translate --only-missing --provider google --format json > $(Build.ArtifactStagingDirectory)/results.json
      fi
    fi
  displayName: 'Step 3 - Auto-Translate'
  env:
    LRM_GOOGLE_API_KEY: $(GoogleTranslateApiKey)

- script: |
    lrm validate
  displayName: 'Step 4 - Re-validate'
  condition: succeeded()

- script: |
    lrm validate --missing-only --format json > $(Build.ArtifactStagingDirectory)/final.json

    remaining=$(jq -r '.missingCount // 0' $(Build.ArtifactStagingDirectory)/final.json)

    if [ "$remaining" -eq "0" ]; then
      echo "##vso[task.complete result=Succeeded;]All translations completed"
    else
      echo "##vso[task.logissue type=warning]$remaining translations still missing"
    fi
  displayName: 'Step 5 - Final Check'

- task: PublishBuildArtifacts@1
  inputs:
    pathToPublish: '$(Build.ArtifactStagingDirectory)'
    artifactName: 'translation-reports'
  displayName: 'Publish Reports'

- script: |
    git config user.name "Azure DevOps"
    git config user.email "devops@azure.com"
    git add Resources/**/*.resx
    git commit -m "🌐 Auto-translate missing keys" || echo "No changes"
    git push origin HEAD:$(Build.SourceBranchName)
  displayName: 'Commit Changes'
  condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
```

## Jenkins

Create a Jenkins pipeline:

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

        stage('Check Missing') {
            steps {
                sh '''
                    ./linux-x64/lrm validate --missing-only --format json > missing.json
                    cat missing.json
                '''
            }
        }

        stage('Auto-Translate') {
            when {
                expression {
                    def missing = readJSON file: 'missing.json'
                    return missing.missingCount > 0
                }
            }
            environment {
                LRM_GOOGLE_API_KEY = credentials('google-translate-api-key')
            }
            steps {
                sh '''
                    ./linux-x64/lrm translate --only-missing --provider google --format json > results.json
                    ./linux-x64/lrm validate
                '''
            }
        }

        stage('Commit Changes') {
            when {
                branch 'main'
                expression {
                    return fileExists('results.json')
                }
            }
            steps {
                sh '''
                    git config user.name "Jenkins"
                    git config user.email "jenkins@ci.example.com"
                    git add Resources/**/*.resx
                    git commit -m "🌐 Auto-translate missing keys" || echo "No changes"
                    git push origin main
                '''
            }
        }
    }
    post {
        always {
            archiveArtifacts artifacts: '*.json', allowEmptyArchive: true
        }
    }
}
```

## Shell Scripts

### Bash Script (Linux/macOS)

#### Enhanced Script with Code Scanning

Create `scripts/auto-translate-with-scan.sh`:

```bash
#!/bin/bash
set -e

echo "🚀 LocalizationManager Auto-Translation with Code Scan"
echo "======================================================"
echo ""

# Configuration
RESOURCES_PATH="${RESOURCES_PATH:-./Resources}"
SOURCE_PATH="${SOURCE_PATH:-./src}"
PROVIDER="${PROVIDER:-google}"

# Step 1: Validate .resx files
echo "📋 Step 1: Validating .resx files..."
lrm validate -p "$RESOURCES_PATH"
echo "✅ Validation passed"
echo ""

# Step 2: Scan code for missing keys
echo "🔍 Step 2: Scanning code for missing keys..."
lrm scan -p "$RESOURCES_PATH" --source-path "$SOURCE_PATH" --format json > scan-results.json

missing_in_code=$(jq -r '.summary.missingKeys // 0' scan-results.json)

if [ "$missing_in_code" -eq "0" ]; then
    echo "✨ No missing keys found in code!"
else
    echo "📊 Found $missing_in_code keys in code but missing from .resx:"
    jq -r '.missingKeys[] | "  - \(.key) (\(.referenceCount) refs)"' scan-results.json
fi
echo ""

# Step 3: Add missing keys from code
if [ "$missing_in_code" -gt "0" ]; then
    echo "➕ Step 3: Adding missing keys to .resx..."
    added_count=0
    while IFS= read -r key; do
        if [ -n "$key" ]; then
            lrm add -p "$RESOURCES_PATH" --key "$key" --value "$key" --comment "Auto-added from code scan" || true
            added_count=$((added_count + 1))
        fi
    done < <(jq -r '.missingKeys[]?.key // empty' scan-results.json)
    echo "✅ Added $added_count keys"
    echo ""
fi

# Step 4: Check for missing translations
echo "🔍 Step 4: Checking for missing translations..."
lrm validate -p "$RESOURCES_PATH" --missing-only --format json > missing.json

missing_count=$(jq -r '.missingCount // 0' missing.json)

if [ "$missing_count" -eq "0" ]; then
    echo "✨ No missing translations found!"
else
    echo "📊 Found $missing_count missing translations:"
    jq -r '.languages[] | "  - \(.code): \(.missingCount) keys"' missing.json
fi
echo ""

# Step 5: Translate missing keys
if [ "$missing_count" -gt "0" ]; then
    echo "🌐 Step 5: Translating missing keys..."
    lrm translate -p "$RESOURCES_PATH" --only-missing --provider "$PROVIDER" --format json > translation-results.json

    echo ""
    echo "📝 Translation results by language:"
    jq -r '
      .translations |
      group_by(.language) |
      map({
        language: .[0].language,
        count: length,
        success: map(select(.status == "success" or .status == "✓")) | length
      }) |
      .[] | "  - \(.language): \(.success)/\(.count) translated"
    ' translation-results.json
    echo ""
fi

# Step 6: Final validation
echo "✅ Step 6: Final validation..."
lrm validate -p "$RESOURCES_PATH"
echo "✅ Validation passed"
echo ""

# Step 7: Final status
echo "🔍 Step 7: Final status check..."
lrm scan -p "$RESOURCES_PATH" --source-path "$SOURCE_PATH" --format json > final-scan.json
lrm validate -p "$RESOURCES_PATH" --missing-only --format json > final-missing.json

final_missing_code=$(jq -r '.summary.missingKeys // 0' final-scan.json)
final_missing_trans=$(jq -r '.missingCount // 0' final-missing.json)

echo "📊 Final Status:"
echo "  - Keys missing from .resx (found in code): $final_missing_code"
echo "  - Keys needing translation: $final_missing_trans"
echo ""

if [ "$final_missing_code" -eq "0" ] && [ "$final_missing_trans" -eq "0" ]; then
    echo "🎉 Perfect! All keys synchronized and translated!"
else
    echo "⚠️  Some keys still need attention"
fi

echo ""
echo "📋 Reports saved:"
echo "  - scan-results.json (initial code scan)"
echo "  - missing.json (missing translations)"
if [ -f translation-results.json ]; then
    echo "  - translation-results.json (translation results)"
fi
echo "  - final-scan.json (final code scan)"
echo "  - final-missing.json (final translation status)"
echo ""
echo "✨ Done!"
```

Make it executable:
```bash
chmod +x scripts/auto-translate-with-scan.sh
```

#### Basic Script (Original)

Create `scripts/auto-translate.sh`:

```bash
#!/bin/bash
set -e

echo "🚀 LocalizationManager Auto-Translation Workflow"
echo "================================================"
echo ""

# Step 1: Initial validation
echo "📋 Step 1: Validating all resource keys..."
lrm validate
echo "✅ Validation passed"
echo ""

# Step 2: Check for missing translations
echo "🔍 Step 2: Checking for missing translations..."
lrm validate --missing-only --format json > missing.json

if [ ! -s missing.json ]; then
    echo "✨ No missing translations found!"
    exit 0
fi

missing_count=$(jq -r '.missingCount // 0' missing.json)

if [ "$missing_count" -eq "0" ]; then
    echo "✨ No missing translations found!"
    exit 0
fi

echo "📊 Found $missing_count missing translations:"
jq -r '.languages[] | "  - \(.code): \(.missingCount) keys"' missing.json
echo ""

# Step 3: Translate
echo "🌐 Step 3: Translating missing keys..."
lrm translate --only-missing --provider google --format json > results.json

echo ""
echo "📝 Translation results by language:"
jq -r '
  .translations |
  group_by(.language) |
  map({
    language: .[0].language,
    count: length,
    success: map(select(.status == "success" or .status == "✓")) | length
  }) |
  .[] | "  - \(.language): \(.success)/\(.count) translated"
' results.json
echo ""

# Step 4: Re-validate
echo "✅ Step 4: Re-validating after translation..."
lrm validate
echo "✅ Validation passed"
echo ""

# Step 5: Final check
echo "🔍 Step 5: Final check for missing translations..."
lrm validate --missing-only --format json > final.json

remaining=$(jq -r '.missingCount // 0' final.json)

if [ "$remaining" -eq "0" ]; then
    echo "🎉 All translations completed successfully!"
    echo ""
    echo "📋 Reports saved:"
    echo "  - missing.json (initial state)"
    echo "  - results.json (translation results)"
    echo "  - final.json (final state)"
else
    echo "⚠️  $remaining translations still missing:"
    jq -r '.missing[] | "  - [\(.language)] \(.key)"' final.json
fi

echo ""
echo "✨ Done!"
```

Make it executable:
```bash
chmod +x scripts/auto-translate.sh
```

### PowerShell Script (Windows)

Create `scripts/Auto-Translate.ps1`:

```powershell
$ErrorActionPreference = "Stop"

Write-Host "🚀 LocalizationManager Auto-Translation Workflow" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Validate
Write-Host "📋 Step 1: Validating all resource keys..." -ForegroundColor Yellow
& lrm validate
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Validation failed" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Validation passed" -ForegroundColor Green
Write-Host ""

# Step 2: Check missing
Write-Host "🔍 Step 2: Checking for missing translations..." -ForegroundColor Yellow
& lrm validate --missing-only --format json > missing.json

$missing = Get-Content missing.json | ConvertFrom-Json
$missingCount = $missing.missingCount

if ($missingCount -eq 0) {
    Write-Host "✨ No missing translations found!" -ForegroundColor Green
    exit 0
}

Write-Host "📊 Found $missingCount missing translations:" -ForegroundColor Cyan
foreach ($lang in $missing.languages) {
    Write-Host "  - $($lang.code): $($lang.missingCount) keys"
}
Write-Host ""

# Step 3: Translate
Write-Host "🌐 Step 3: Translating missing keys..." -ForegroundColor Yellow
& lrm translate --only-missing --provider google --format json > results.json

$results = Get-Content results.json | ConvertFrom-Json
Write-Host ""
Write-Host "📝 Translation results:" -ForegroundColor Cyan
$results.translations | Group-Object -Property language | ForEach-Object {
    $success = ($_.Group | Where-Object { $_.status -eq "success" -or $_.status -eq "✓" }).Count
    Write-Host "  - $($_.Name): $success/$($_.Count) translated"
}
Write-Host ""

# Step 4: Re-validate
Write-Host "✅ Step 4: Re-validating..." -ForegroundColor Yellow
& lrm validate
Write-Host "✅ Validation passed" -ForegroundColor Green
Write-Host ""

# Step 5: Final check
Write-Host "🔍 Step 5: Final check..." -ForegroundColor Yellow
& lrm validate --missing-only --format json > final.json

$final = Get-Content final.json | ConvertFrom-Json
$remaining = $final.missingCount

if ($remaining -eq 0) {
    Write-Host "🎉 All translations completed!" -ForegroundColor Green
} else {
    Write-Host "⚠️  $remaining translations still missing" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "✨ Done!" -ForegroundColor Green
```

## JSON Format for CI/CD

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

## Configuration File Support

LRM supports configuration files to avoid repeating common options. Create a `lrm.json` file in your resource directory:

```json
{
  "DefaultLanguageCode": "en"
}
```

**Configuration Options:**
- `DefaultLanguageCode` (string, optional): The language code to display for the default language (e.g., "en", "fr"). If not set, displays "default". Only affects display output in Table, Simple, and TUI formats. Does not affect JSON/CSV exports or internal logic.

LRM will automatically discover and use `lrm.json` in the resource path, or you can specify a custom config file:

```bash
# Use auto-discovered lrm.json
lrm validate --path ./Resources

# Use custom config file
lrm validate --config-file ./my-config.json --path ./Resources
```

In CI/CD workflows, you can commit your `lrm.json` to version control and all commands will use it automatically.

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

## Best Practices

### 1. API Key Management

**Never commit API keys to your repository!**

✅ **DO:**
- Use CI/CD secrets/variables
- Use environment variables
- Use secure credential stores

❌ **DON'T:**
- Put API keys in code
- Commit `lrm.json` with API keys
- Share keys in chat/email

### 2. Translation Strategy

**When to auto-translate:**
- Development/staging environments
- Draft translations for review
- Placeholder text during development

**When to use human translation:**
- Production/customer-facing content
- Marketing materials
- Legal text

### 3. Validation Gates

Always validate before and after translation:

```yaml
- validate → check missing → translate → re-validate → commit
```

### 4. Cost Control

- Use `--only-missing` to avoid re-translating
- Enable caching (enabled by default)
- Set appropriate rate limits
- Monitor API usage in provider dashboard

### 5. Review Process

```yaml
# Option 1: Auto-commit to separate branch
branches:
  - translations-auto

# Option 2: Create PR for review
- create pull request
- manual review
- merge when approved
```

### 6. Reporting

Track what was translated:

```bash
# Save reports
- missing.json     # What was missing
- results.json     # What was translated
- final.json       # Final state

# Upload as artifacts
# Create summaries
# Post to Slack/Teams
```

## Troubleshooting

### API Rate Limits

If you hit rate limits:

```yaml
- name: Translate with retry
  run: |
    lrm translate --only-missing --batch-size 5
  timeout-minutes: 30
```

### Large Translation Jobs

For 100+ keys:

```bash
# Translate in batches
lrm translate --only-missing --batch-size 10

# Or translate specific languages
lrm translate --only-missing --target-languages fr,de
lrm translate --only-missing --target-languages es,it
```

### Validation Failures

If validation fails after translation:

```bash
# Check what changed
git diff Resources/

# Re-run validation with details
lrm validate --verbose

# Manual review in TUI
lrm edit
```

## Support

For issues or questions:
- GitHub Issues: https://github.com/nprotopapas/LocalizationManager/issues
- Documentation: https://github.com/nprotopapas/LocalizationManager/docs

## License

LocalizationManager is licensed under the MIT License.
