// Fixture for issue #6 Bug 2: a deeply nested namespace whose segments
// (Resources, Components, Account, Pages) must NOT be treated as localization keys.
using Microsoft.Extensions.Localization;

namespace Vitrum.Resources.Components.Account.Pages;

public partial class Login
{
    private void Demo()
    {
        // A real localization usage that SHOULD be detected.
        var title = Resources.Login_Title;
    }
}
