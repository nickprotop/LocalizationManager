# Test Source Files

This directory contains realistic source code files used for testing the LocalizationManager code scanning functionality.

## Purpose

These files are used by integration tests to verify that the code scanners correctly identify localization key references across different file types and patterns.

## File Structure

### C# Files

- **UserController.cs** - ASP.NET controller with various localization patterns
  - Property access: `Resources.WelcomeMessage`
  - Method calls: `GetString("ConfirmEmail")`
  - Indexer access: `_localizer["Error_InvalidEmail"]`
  - Dynamic keys: `GetString($"Status_{status}")`
  - Missing keys for detection testing

- **ProductService.cs** - Service class with edge cases and advanced patterns
  - Multiple localizer instances
  - Conditional key access
  - Dynamic patterns
  - String concatenation patterns
  - Comments and string literals (should NOT be detected)

### Razor Files

- **LoginPage.razor** - Blazor component with Razor-specific patterns
  - Inline property access: `@Resources.Login_Title`
  - Inline indexer: `@Localizer["Login_Username"]`
  - Code block patterns
  - Dynamic keys in code blocks

- **HomeView.cshtml** - MVC Razor view with mixed patterns
  - HTML localizer: `@HtmlLocalizer["Home_WelcomeMessage"]`
  - Standard patterns in HTML context
  - Loop-based rendering with localization
  - @functions block

### XAML Files

- **MainWindow.xaml** - WPF window with XAML binding patterns
  - Static resource binding: `{x:Static res:Resources.Window_Title}`
  - Multiple controls and layouts
  - Missing keys for detection testing

## Key Categories

Each file contains keys in these categories:

1. **Existing Keys** - Keys that should exist in TestResource.resx and be detected
2. **Missing Keys** - Keys that intentionally don't exist in TestResource.resx (for testing missing key detection)
3. **Dynamic Keys** - Keys using string interpolation (low confidence detection)

## Integration Test Usage

These files should be used by `ScanCommandTests` integration tests to verify:

1. **Detection Accuracy** - All static keys are correctly identified
2. **Missing Key Detection** - Missing keys are reported with correct file locations and line numbers
3. **Dynamic Key Handling** - Dynamic patterns are flagged with low confidence
4. **Multi-File Scanning** - Scanning directory trees works correctly
5. **Pattern Coverage** - All supported patterns are detected across file types

## Expected Key References

### UserController.cs
- High confidence: WelcomeMessage, Success_UserCreated, ConfirmEmail, PageTitle, Error_InvalidEmail, Warning_DuplicateUser, Button_Save, Label_UserName
- Missing: MissingKeyFromController, MissingIndexerKey, MissingMethodKey
- Dynamic: Status_{status}

### ProductService.cs
- High confidence: Product_CreateTitle, Product_NameLabel, Product_CreateSuccess, Product_PriceLabel, Common_SaveButton, Product_ValidateName, Product_ConfirmCreate, Product_CreatedFormat, Error_InvalidPrice, Error_PriceRange, Error_DuplicateName
- Missing: MissingProductKey1, MissingProductKey2, MissingProductKey3
- Dynamic: Error_Code_{errorCode}, Status_{status}, Action_{status}

### LoginPage.razor
- High confidence: Login_Title, Login_WelcomeMessage, Login_Username, Login_UsernamePlaceholder, Login_Password, Button_Login, Login_ForgotPassword, Login_ErrorMessage, Login_Validating, Login_Success, Login_Failed
- Missing: MissingRazorKey, MissingRazorIndexer
- Dynamic: Login_{status}

### HomeView.cshtml
- High confidence: Home_Title, Site_Name, Nav_Home, Nav_Products, Nav_About, Nav_Contact, Home_WelcomeTitle, Home_WelcomeMessage, Home_FeaturesTitle, Feature_Fast, Feature_FastDescription, Feature_Secure, Feature_SecureDescription, Notification_Message, Home_PageTitle, Home_MetaDescription, Footer_Copyright, Button_ReadMore, Footer_CompanyName, Footer_AllRights, Footer_Privacy, Footer_Terms
- Missing: MissingViewKey, MissingViewIndexer
- Dynamic: Content_{section}

### MainWindow.xaml
- High confidence: Window_Title, App_Name, App_Version, Tab_Dashboard, Dashboard_Welcome, Button_Refresh, Tab_Settings, Settings_Language, Settings_Theme, Status_Ready
- Missing: MissingXamlKey1, MissingXamlKey2

## Maintenance

When adding new scanner patterns or test scenarios:

1. Add the pattern to the appropriate file
2. Document it in this README
3. Update TestResource.resx if the key should exist
4. Create corresponding integration tests
