// Copyright (c) 2025 Test File
// This file contains realistic C# code patterns for testing the code scanner

using System;
using Microsoft.Extensions.Localization;

namespace TestApp.Controllers
{
    public class UserController
    {
        private readonly IStringLocalizer<UserController> _localizer;

        public UserController(IStringLocalizer<UserController> localizer)
        {
            _localizer = localizer;
        }

        public void CreateUser(string username, string email)
        {
            // Property access pattern - should be detected
            var welcomeMsg = Resources.WelcomeMessage;
            var successMsg = Resources.Success_UserCreated;

            // GetString call pattern - should be detected
            var confirmMsg = GetString("ConfirmEmail");
            var titleMsg = GetLocalizedString("PageTitle");

            // Indexer pattern - should be detected
            var errorMsg = _localizer["Error_InvalidEmail"];
            var warningMsg = _localizer["Warning_DuplicateUser"];

            // Missing keys - these don't exist in TestResource.resx
            var missingKey1 = Resources.MissingKeyFromController;
            var missingKey2 = _localizer["MissingIndexerKey"];
            var missingKey3 = GetString("MissingMethodKey");
        }

        public void UpdateUser(int userId)
        {
            // More realistic patterns
            var button = Resources.Button_Save;
            var label = Resources.Label_UserName;
        }

        public string GetUserStatus(string status)
        {
            // Dynamic key - low confidence
            return GetString($"Status_{status}");
        }

        // Helper methods
        private string GetString(string key) => _localizer[key];
        private string GetLocalizedString(string key) => _localizer[key];
    }
}
