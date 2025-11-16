# Placeholder Validation

LocalizationManager automatically validates that placeholders in translations match the source text. This ensures that format strings, variables, and dynamic content are correctly preserved across all languages.

## Table of Contents
- [Overview](#overview)
- [Supported Placeholder Formats](#supported-placeholder-formats)
- [How It Works](#how-it-works)
- [Validation Rules](#validation-rules)
- [Configuration](#configuration)
- [CLI Usage](#cli-usage)
- [TUI Usage](#tui-usage)
- [Common Scenarios](#common-scenarios)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)

---

## Overview

Placeholder validation is **automatically enabled** in the `lrm validate` command and TUI validation panel (F6). It detects and validates four major placeholder formats commonly used in localization:

1. **.NET Format Strings** - `{0}`, `{name}`, `{0:C2}`
2. **Printf-Style** - `%s`, `%d`, `%1$s`
3. **ICU MessageFormat** - `{count, plural, one {# item} other {# items}}`
4. **Template Literals** - `${var}`, `${user.name}`

---

## Supported Placeholder Formats

### .NET Format Strings

Common in C#, .NET applications, and resource files (.resx).

**Syntax:**
- Indexed: `{0}`, `{1}`, `{2}`
- Named: `{name}`, `{userName}`, `{count}`
- With format specifiers: `{0:N2}`, `{price:C2}`, `{date:yyyy-MM-dd}`

**Examples:**
```csharp
// Source (English)
"Hello {0}!"
"Your balance is {amount:C2}"
"Welcome back, {userName}!"

// Valid Translation (Greek)
"Γεια σου {0}!"
"Το υπόλοιπό σας είναι {amount:C2}"
"Καλώς ήρθες, {userName}!"
```

### Printf-Style Placeholders

Common in C, Java, Android (strings.xml), and iOS localization.

**Syntax:**
- Type specifiers: `%s` (string), `%d` (integer), `%f` (float)
- Positional: `%1$s`, `%2$d`, `%3$f`
- With width/precision: `%10.2f`, `%.2f`

**Examples:**
```c
// Source (English)
"You have %d items"
"Hello %s, you have %d new messages"
"Price: %1$s, Quantity: %2$d"

// Valid Translation (Spanish)
"Tienes %d artículos"
"Hola %s, tienes %d mensajes nuevos"
"Precio: %1$s, Cantidad: %2$d"
```

### ICU MessageFormat

Common in complex pluralization and selection scenarios.

**Syntax:**
- Plurals: `{count, plural, one {# item} other {# items}}`
- Select: `{gender, select, male {He} female {She} other {They}}`
- Select ordinal: `{place, selectordinal, one {#st} two {#nd} few {#rd} other {#th}}`

**Examples:**
```icu
// Source (English)
"{count, plural, one {# item} other {# items}}"
"{gender, select, male {He} female {She} other {They}}"

// Valid Translation (French)
"{count, plural, one {# article} other {# articles}}"
"{gender, select, male {Il} female {Elle} other {Ils}}"
```

### Template Literals

Common in JavaScript, TypeScript, and modern frameworks.

**Syntax:**
- Simple: `${name}`, `${count}`
- With property access: `${user.name}`, `${cart.total}`

**Examples:**
```javascript
// Source (English)
"Hello ${name}!"
"User: ${user.firstName} ${user.lastName}"
"Total: ${cart.count} items"

// Valid Translation (Japanese)
"こんにちは ${name}！"
"ユーザー: ${user.firstName} ${user.lastName}"
"合計: ${cart.count} アイテム"
```

---

## How It Works

The placeholder validator performs these steps:

1. **Detection** - Scans source and translation text for placeholders using regex patterns
2. **Normalization** - Converts placeholders to normalized identifiers for comparison
3. **Comparison** - Validates that translation placeholders match source placeholders
4. **Error Reporting** - Generates detailed error messages for any mismatches

**Normalization Examples:**
- `{0}` → `"0"`
- `{name}` → `"name"`
- `%1$s` → `"1"`
- `%s` → `"s"`
- `${user.name}` → `"user.name"`

---

## Validation Rules

### ✅ Valid Scenarios

**1. Exact Match**
```
Source:      "Hello {0}!"
Translation: "Bonjour {0}!"  ✓
```

**2. Different Order (same placeholders)**
```
Source:      "First {0}, Second {1}"
Translation: "Second {1}, First {0}"  ✓
```

**3. Named Placeholders**
```
Source:      "User {name} has {count} items"
Translation: "L'utilisateur {name} a {count} articles"  ✓
```

**4. Format Specifiers (optional in translation)**
```
Source:      "Price: {0:C2}"
Translation: "Prix: {0}"  ✓
```

### ❌ Invalid Scenarios

**1. Missing Placeholder**
```
Source:      "Hello {0}!"
Translation: "Bonjour!"  ✗
Error: Missing placeholder: {0}
```

**2. Extra Placeholder**
```
Source:      "Hello!"
Translation: "Bonjour {0}!"  ✗
Error: Extra placeholder not in source: {0}
```

**3. Wrong Placeholder Index/Name**
```
Source:      "Hello {0}!"
Translation: "Bonjour {1}!"  ✗
Error: Missing placeholder: {0}; Extra placeholder not in source: {1}
```

**4. Placeholder Count Mismatch**
```
Source:      "Value: {0}"
Translation: "Value: {0} {0}"  ✗
Error: Placeholder count mismatch: source has 1, translation has 2
```

**5. Different Placeholder Systems**
```
Source:      "Count: {0}"
Translation: "Contagem: %d"  ✗
Error: Missing placeholder: {0}; Extra placeholder not in source: %d
```

---

## Configuration

### Default Behavior

By default, LRM validates only **.NET format placeholders** (`{0}`, `{1}`, `{name}`). This is because:
- LRM is designed for .NET `.resx` files
- .NET placeholders are used in 99% of .resx projects
- This prevents false positives from other placeholder types

### Changing Placeholder Types

You can customize which placeholder types to validate using configuration or CLI options.

#### Option 1: Configuration File (`lrm.json`)

Create or update `lrm.json` in your Resources directory:

```json
{
  "Validation": {
    "PlaceholderTypes": ["dotnet"],
    "EnablePlaceholderValidation": true
  }
}
```

**Available Types:**
- `"dotnet"` - .NET format strings (default)
- `"printf"` - Printf-style placeholders
- `"icu"` - ICU MessageFormat
- `"template"` - Template literals
- `"all"` - All types

**Examples:**

**.NET Only (Default):**
```json
{
  "Validation": {
    "PlaceholderTypes": ["dotnet"]
  }
}
```

**Multiple Types (e.g., Blazor with JavaScript):**
```json
{
  "Validation": {
    "PlaceholderTypes": ["dotnet", "template"]
  }
}
```

**Disable Placeholder Validation:**
```json
{
  "Validation": {
    "EnablePlaceholderValidation": false
  }
}
```

#### Option 2: CLI Override

Override configuration for a single command:

```bash
# Validate specific placeholder types
lrm validate --placeholder-types dotnet,printf

# Validate all placeholder types
lrm validate --placeholder-types all

# Disable placeholder validation entirely
lrm validate --no-placeholder-validation
```

**Priority:** CLI arguments override configuration file settings.

**See Also:** [Configuration Guide](CONFIGURATION.md#validation) for more details on configuration options.

---

## CLI Usage

Placeholder validation is automatically enabled in the `validate` command:

```bash
# Validate all resource files
lrm validate

# Validate with specific output format
lrm validate --format json
lrm validate --format simple
```

**Example Output (Table Format):**
```
Scanning: /path/to/resources

✓ Found 3 language(s):
  • en (default)
  • fr
  • de

⚠ Validation found 2 issue(s)

┌─────────────────────────────────────────────┐
│        Placeholder Mismatches (red)         │
├──────────┬───────────────┬──────────────────┤
│ Language │ Key           │ Error            │
├──────────┼───────────────┼──────────────────┤
│ fr       │ Welcome       │ Missing          │
│          │               │ placeholder: {0} │
│ de       │ ItemCount     │ Extra            │
│          │               │ placeholder not  │
│          │               │ in source: {1}   │
└──────────┴───────────────┴──────────────────┘
```

**Example Output (JSON Format):**
```json
{
  "isValid": false,
  "totalIssues": 2,
  "placeholderMismatches": {
    "fr": {
      "Welcome": "Missing placeholder: {0}"
    },
    "de": {
      "ItemCount": "Extra placeholder not in source: {1}"
    }
  }
}
```

---

## TUI Usage

Press **F6** in the TUI to validate all resource files. Placeholder errors are displayed in the validation summary:

```
┌─────────────────────────────────────────┐
│           Validation Results            │
├─────────────────────────────────────────┤
│ Found 3 issue(s):                       │
│                                         │
│ Missing: 0                              │
│ Extra: 0                                │
│ Duplicates: 0                           │
│ Empty: 1                                │
│ Placeholder Mismatches: 2               │
│                                         │
│ [OK]                                    │
└─────────────────────────────────────────┘
```

---

## Common Scenarios

### Mixed Placeholder Types

You can use multiple placeholder formats in the same string:

```csharp
// Source
"Hello {0}, you have %d items in ${cart.name}"

// Valid Translation
"Bonjour {0}, vous avez %d articles dans ${cart.name}"
```

### Complex .NET Format Specifiers

Format specifiers are optional in translations:

```csharp
// Source
"Date: {date:yyyy-MM-dd}, Amount: {amount:N2}"

// Both valid:
"日付: {date:yyyy-MM-dd}, 金額: {amount:N2}"
"日付: {date}, 金額: {amount}"
```

### Printf Positional Arguments

Positional arguments allow reordering:

```c
// Source (English)
"Hello %1$s, you have %2$d items"

// Valid Translation (can reorder)
"Sie haben %2$d Artikel, Hallo %1$s"
```

### ICU Plurals

ICU plural rules vary by language:

```icu
// English (two forms)
"{count, plural, one {# item} other {# items}}"

// Polish (three forms)
"{count, plural, one {# przedmiot} few {# przedmioty} other {# przedmiotów}}"
```

---

## Best Practices

### 1. Use Consistent Placeholder Systems

Stick to one placeholder format per project:
- .NET projects → .NET format strings
- Android → Printf-style
- JavaScript/React → Template literals

### 2. Meaningful Named Placeholders

Prefer named placeholders over indexed:
```csharp
// Good
"Welcome {userName}, you have {messageCount} messages"

// Less clear
"Welcome {0}, you have {1} messages"
```

### 3. Document Placeholder Meaning

Add comments in resource files:
```xml
<!-- {0} = user name, {1} = item count -->
<data name="UserItems">
  <value>User {0} has {1} items</value>
</data>
```

### 4. Test Placeholder Validation Early

Run validation frequently during development:
```bash
# Add to CI/CD pipeline
lrm validate || exit 1
```

### 5. Handle Empty Translations

Empty translations skip placeholder validation:
- Placeholder mismatches are not reported for empty values
- Empty values are tracked separately in validation results

---

## Troubleshooting

### Problem: False Positive - "Missing placeholder" for optional format specifiers

**Example:**
```
Source:      "Price: {amount:C2}"
Translation: "Prix: {amount}"
Status:      ✓ Valid (format specifiers are optional)
```

**Solution:** This is expected behavior. Format specifiers are optional in translations.

---

### Problem: "Type mismatch" error for similar placeholders

**Example:**
```
Source:      "Count: {0}"
Translation: "Contagem: %d"
Error:       Missing placeholder: {0}; Extra placeholder: %d
```

**Solution:** Different placeholder systems (`{0}` vs `%d`) are treated as different placeholders. Keep placeholder systems consistent across translations.

---

### Problem: Template literal detected inside .NET string

**Example:**
```
Source:      "var js = \"${user.name}\";"
Detected:    ${user.name} as TemplateLiteral ✓ Correct
```

**Solution:** This is correct! Template literals (`${...}`) are detected separately from .NET format strings (`{...}`). Both can coexist in the same string.

---

### Problem: Escaped characters not handled

**Example:**
```
Source:      "Use %% to escape"
Detected:    (no placeholders) ✓ Correct
```

**Solution:** Escaped printf placeholders (`%%`) are correctly ignored.

---

## Technical Details

### Regex Patterns

**. NET Format:**
```regex
(?<!\$)\{(?<index>\d+)(?::(?<format>[^}]+))?\}
(?<!\$)\{(?<name>[a-zA-Z_]\w*)(?::(?<format>[^}]+))?\}
```

**Printf-Style:**
```regex
%(?:(?<position>\d+)\$)?(?<flags>[-+0 #]*)(?<width>\d+)?(?:\.(?<precision>\d+))?(?<type>[sdifFeEgGxXocpn%])
```

**ICU MessageFormat:**
```regex
\{(?<name>\w+),\s*(?<type>plural|select|selectordinal)(?:,\s*(?<format>[^}]+))?\}
```

**Template Literals:**
```regex
\$\{(?<name>[a-zA-Z_][\w.]*)\}
```

### Performance

- Detection uses compiled regex patterns for optimal performance
- Validation is O(n) where n is the number of placeholders
- Typical validation time: < 1ms per key
- Memory efficient: placeholders are normalized to simple strings

---

## See Also

- [Validation Guide](VALIDATION.md) - Complete validation features
- [Commands Reference](COMMANDS.md) - CLI command documentation
- [README](../README.md) - Getting started guide

---

**Last Updated:** 2025-01-16
**Version:** 0.7.0 (Phase 2: Variable Validation)
