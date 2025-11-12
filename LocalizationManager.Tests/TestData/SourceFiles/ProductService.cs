// Copyright (c) 2025 Test File
// This file contains additional C# patterns including edge cases for testing

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Localization;

namespace TestApp.Services
{
    public class ProductService
    {
        private readonly IStringLocalizer<ProductService> _localizer;
        private readonly IStringLocalizer _sharedLocalizer;

        public ProductService(
            IStringLocalizer<ProductService> localizer,
            IStringLocalizer sharedLocalizer)
        {
            _localizer = localizer;
            _sharedLocalizer = sharedLocalizer;
        }

        public string CreateProduct(string name, decimal price)
        {
            // Standard property access
            var titleMsg = Resources.Product_CreateTitle;
            var labelMsg = Resources.Product_NameLabel;

            // Standard indexer access
            var successMsg = _localizer["Product_CreateSuccess"];
            var priceMsg = _localizer["Product_PriceLabel"];

            // Shared localizer pattern
            var commonMsg = _sharedLocalizer["Common_SaveButton"];

            // Method calls with different patterns
            var validateMsg = GetString("Product_ValidateName");
            var confirmMsg = GetLocalizedString("Product_ConfirmCreate");

            // Nested/chained patterns
            var result = string.Format(_localizer["Product_CreatedFormat"], name, price);

            return successMsg;
        }

        public List<string> GetProductErrors(int errorCode)
        {
            var errors = new List<string>();

            // Conditional key access
            if (errorCode == 1)
            {
                errors.Add(Resources.Error_InvalidPrice);
                errors.Add(_localizer["Error_PriceRange"]);
            }
            else if (errorCode == 2)
            {
                errors.Add(Resources.Error_DuplicateName);
            }

            // Dynamic key - low confidence pattern
            var dynamicError = GetString($"Error_Code_{errorCode}");
            errors.Add(dynamicError);

            return errors;
        }

        public void UpdateProductStatus(string productId, string status)
        {
            // Multiple dynamic patterns
            var statusMsg = Resources.GetString($"Status_{status}");
            var actionMsg = _localizer[$"Action_{status}"];

            // String concatenation (should NOT be detected as dynamic)
            var key = "Product_" + "Update";
            var msg = GetString(key);
        }

        // Missing keys - for testing detection
        public void TestMissingKeys()
        {
            var missing1 = Resources.MissingProductKey1;
            var missing2 = _localizer["MissingProductKey2"];
            var missing3 = GetString("MissingProductKey3");
        }

        // Edge case: Comments and strings that look like keys
        public void EdgeCases()
        {
            // This comment mentions Resources.NotAKey but shouldn't be detected
            var text = "This string mentions _localizer[\"NotAKey\"] but shouldn't match";

            /* Multi-line comment
             * Resources.AlsoNotAKey
             * GetString("StillNotAKey")
             */

            // String literals that aren't localization
            var sql = "SELECT * FROM Resources WHERE Key = 'Data'";
            var code = "_localizer[\"value\"]"; // This is a string, not actual code
        }

        // Helper methods
        private string GetString(string key) => _localizer[key];
        private string GetLocalizedString(string key) => _localizer[key];
    }
}
