using System;
using System.Security.Cryptography;
using System.Text;
using static ZeroTrustMigrationAddin.Services.FileLogger;

namespace ZeroTrustMigrationAddin.Services
{
    /// <summary>
    /// Centralized credential encryption/decryption service using Windows DPAPI.
    /// Credentials can only be decrypted by the same Windows user on the same machine.
    /// 
    /// SECURITY: DPAPI provides user-scoped encryption tied to Windows login credentials.
    /// This protects against:
    /// - Other users on the same machine
    /// - Offline attacks on stolen disk/files
    /// - Backup exposure (encrypted data is useless without Windows credentials)
    /// </summary>
    public static class SecureCredentialManager
    {
        /// <summary>
        /// Encrypts a string using Windows DPAPI (Data Protection API).
        /// </summary>
        /// <param name="plainText">The text to encrypt.</param>
        /// <returns>Base64-encoded encrypted string, or empty string on failure.</returns>
        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                var data = Encoding.UTF8.GetBytes(plainText);
                var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            catch (Exception ex)
            {
                Instance.Error($"[SECURITY] Failed to encrypt credential: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Decrypts a DPAPI-encrypted string.
        /// </summary>
        /// <param name="encryptedText">Base64-encoded encrypted string.</param>
        /// <returns>Decrypted plain text, or empty string if decryption fails.</returns>
        public static string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return string.Empty;

            try
            {
                var encrypted = Convert.FromBase64String(encryptedText);
                var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (CryptographicException)
            {
                // This happens when the data was encrypted by a different user or on a different machine
                // It can also happen if trying to decrypt plaintext (legacy migration)
                Instance.Warning("[SECURITY] Cannot decrypt credential - may be from different user/machine or unencrypted legacy data");
                return string.Empty;
            }
            catch (FormatException)
            {
                // Not valid Base64 - might be legacy plaintext
                Instance.Warning("[SECURITY] Credential is not encrypted (legacy format) - will be encrypted on next save");
                return encryptedText; // Return as-is for backward compatibility
            }
            catch (Exception ex)
            {
                Instance.Warning($"[SECURITY] Failed to decrypt credential: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Checks if a string appears to be DPAPI-encrypted (valid Base64 of sufficient length).
        /// Used for detecting legacy plaintext credentials that need migration.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>True if the value appears to be encrypted.</returns>
        public static bool IsEncrypted(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            try
            {
                // DPAPI-encrypted data is Base64 and typically longer than the original
                // A minimum encrypted length would be ~40+ characters for even a short password
                if (value.Length < 40)
                    return false;

                var decoded = Convert.FromBase64String(value);
                // DPAPI adds a header, so even a 1-byte plaintext produces 50+ bytes encrypted
                return decoded.Length >= 50;
            }
            catch
            {
                return false;
            }
        }
    }
}
