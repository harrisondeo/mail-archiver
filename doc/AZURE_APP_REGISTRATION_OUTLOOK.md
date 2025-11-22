# ☁️ Azure App Registration for Outlook Personal Accounts

[← Back to Documentation Index](Index.md)

## 📋 Overview

This guide provides step-by-step instructions for setting up Azure App Registration to enable OAuth2 authentication for Outlook.com personal email accounts in the Mail Archiver application.

## 📚 Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Azure App Registration Setup](#azure-app-registration-setup)
4. [Configure Mail Archiver](#configure-mail-archiver)
5. [Adding an Outlook Account](#adding-an-outlook-account)
6. [Troubleshooting](#troubleshooting)

## 🎯 Prerequisites

- Access to [Azure Portal](https://portal.azure.com)
- A Microsoft Account (Outlook.com, Hotmail.com, Live.com, or MSN.com)
- Mail Archiver application running with HTTPS (required for OAuth2)

## ☁️ Azure App Registration Setup

### Step 1: Create App Registration

1. Navigate to the [Azure Portal](https://portal.azure.com)
2. Sign in with your Microsoft account
3. Search for **Azure Active Directory** (or **Microsoft Entra ID**) in the search bar
4. In the left navigation pane, select **App registrations**
5. Click **+ New registration** at the top

### Step 2: Configure Registration Settings

Fill in the registration form:

- **Name**: Enter a descriptive name (e.g., "Mail Archiver - Outlook Personal")
- **Supported account types**: Select **Accounts in any organizational directory (Any Azure AD directory - Multitenant) and personal Microsoft accounts (e.g., Skype, Xbox)**
  - This option is required for Outlook.com personal accounts
- **Redirect URI**: 
  - Select **Web** from the dropdown
  - Enter your redirect URI: `https://your-mail-archiver-domain.com/MailAccounts/OutlookOAuthCallback`
  - Replace `your-mail-archiver-domain.com` with your actual domain
  - **Important**: Must use HTTPS (not HTTP)

Click **Register**

### Step 3: Note Important Values

After registration, you'll see the **Overview** page. Note down these values:

- **Application (client) ID** - You'll need this for configuration
- **Directory (tenant) ID** - Not needed for personal accounts, but displayed for reference

### Step 4: Create Client Secret

1. In the left navigation, select **Certificates & secrets**
2. Under **Client secrets**, click **+ New client secret**
3. Provide a description (e.g., "Mail Archiver Secret")
4. Select an expiration period:
   - **6 months** (recommended for testing)
   - **12 months**
   - **24 months** (maximum)
5. Click **Add**
6. **CRITICAL**: Copy the **Value** immediately and store it securely
   - This secret will NOT be shown again
   - You'll need this for configuration

### Step 5: Configure API Permissions

1. In the left navigation, select **API permissions**
2. Click **+ Add a permission**
3. Select **Microsoft Graph**
4. Choose **Delegated permissions** (NOT Application permissions)
5. Search for and add the following permissions:
   - **IMAP.AccessAsUser.All** - Allows the app to read and write access to mailboxes via IMAP
   - **offline_access** - Allows the app to maintain access to data it has been given access to
6. Click **Add permissions**

**Note**: Admin consent is not required for delegated permissions on personal accounts

### Step 6: Configure Authentication (Optional but Recommended)

1. In the left navigation, select **Authentication**
2. Under **Implicit grant and hybrid flows**, ensure nothing is checked (not needed for our implementation)
3. Under **Allow public client flows**, set to **No**
4. Click **Save**

## 🔧 Configure Mail Archiver

### Add Configuration to appsettings.json

Add the following configuration to your `appsettings.json` file or use environment variables:

```json
{
  "OAuth2": {
    "Outlook": {
      "ClientId": "your-application-client-id",
      "ClientSecret": "your-client-secret-value"
    }
  }
}
```

### Using Environment Variables (Recommended for Docker)

For Docker deployments, add these environment variables to your `docker-compose.yml`:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Existing environment variables...
      
      # Outlook OAuth2 Configuration
      - OAuth2__Outlook__ClientId=your-application-client-id
      - OAuth2__Outlook__ClientSecret=your-client-secret-value
```

Replace the placeholder values with:
- `your-application-client-id`: The Application (client) ID from Azure
- `your-client-secret-value`: The client secret value you saved earlier

## 📧 Adding an Outlook Account

### Step 1: Initiate OAuth2 Flow

1. Log into your Mail Archiver application
2. Navigate to **Mail Accounts** > **Create**
3. Select **OUTLOOK** as the Provider type
4. Click the **Authorize with Outlook** button
5. You will be redirected to Microsoft's login page

### Step 2: Microsoft Authorization

1. Sign in with your Outlook.com personal account
2. Review the permissions being requested:
   - Read and write access to your mailbox via IMAP
   - Maintain access to data you have given it access to
3. Click **Accept** to authorize the application

### Step 3: Complete Account Setup

After authorization, you'll be redirected back to Mail Archiver:

1. Fill in the remaining account details:
   - **Name**: A descriptive name for the account (e.g., "My Personal Outlook")
   - **Email Address**: Your Outlook.com email address
2. The IMAP server settings are pre-configured:
   - Server: outlook.office365.com
   - Port: 993
   - SSL: Enabled
3. (Optional) Configure retention policies:
   - **Delete After Days**: Automatically delete emails from server after archiving
   - **Local Retention Days**: Delete emails from local archive after specified days
4. Click **Create** to save the account

### Step 4: Verify Connection

The system will automatically test the connection using the OAuth2 token. If successful, the account will be created and email synchronization will begin automatically.

## 🔄 Token Management

### Automatic Token Refresh

- Access tokens expire after 1 hour
- Mail Archiver automatically refreshes tokens before they expire using the refresh token
- Refresh tokens are valid for 90 days with rolling expiration (extended with each use)

### Manual Token Refresh

If you encounter authentication issues:

1. Navigate to the account details page
2. Click **Refresh Token** to manually refresh the access token

### Re-authorization

If the refresh token expires or becomes invalid:

1. Navigate to **Mail Accounts**
2. Click on the Outlook account
3. Click **Re-authorize**
4. Complete the OAuth2 flow again

## 🔍 Troubleshooting

### "Redirect URI mismatch" Error

**Problem**: The redirect URI in Azure doesn't match your Mail Archiver URL

**Solution**:
1. Check your Mail Archiver URL (must use HTTPS)
2. Update the Redirect URI in Azure App Registration to match exactly
3. Format: `https://your-domain.com/MailAccounts/OutlookOAuthCallback`

### "Invalid Client" Error

**Problem**: Client ID or Client Secret is incorrect

**Solution**:
1. Verify the Client ID in your configuration matches the Application (client) ID in Azure
2. If the secret expired or was changed, create a new client secret in Azure
3. Update your configuration with the new secret

### "Insufficient Permissions" Error

**Problem**: Required API permissions are not configured

**Solution**:
1. Go to Azure App Registration > API permissions
2. Ensure you have added:
   - IMAP.AccessAsUser.All (Delegated)
   - offline_access (Delegated)
3. Remove any Application permissions if present (not supported for personal accounts)

### Connection Test Fails

**Problem**: Cannot connect to Outlook IMAP server

**Solution**:
1. Verify IMAP is enabled in your Outlook.com account settings
2. Check that the access token hasn't expired
3. Try refreshing the token manually
4. If issues persist, re-authorize the account

### "XOAUTH2 not supported" Error

**Problem**: IMAP server doesn't support OAuth2 authentication

**Solution**:
- This should not occur with outlook.office365.com
- Verify you're using the correct IMAP server (outlook.office365.com)
- Check for any firewall or proxy issues

## 📝 Important Notes

### Security Best Practices

1. **Use HTTPS**: OAuth2 requires HTTPS for security. Never use HTTP in production.
2. **Secure Storage**: Client secrets and access tokens are sensitive. Store them securely.
3. **Secret Rotation**: Regularly rotate client secrets (before they expire).
4. **Access Logs**: Monitor access logs for suspicious activity.

### Limitations

1. **Personal Accounts Only**: This configuration is for Outlook.com personal accounts (Hotmail, Live, MSN).
2. **For Microsoft 365 Work/School accounts**, use the M365 provider instead (see [AZURE_APP_REGISTRATION_M365.md](AZURE_APP_REGISTRATION_M365.md)).
3. **IMAP Access**: Ensure IMAP is enabled in your Outlook.com account settings.

### Token Expiration

- **Access Token**: Expires after 1 hour (auto-refreshed)
- **Refresh Token**: Expires after 90 days of inactivity (rolling window)
- **Client Secret**: Expires based on your selection (6-24 months)

## 🆘 Support

If you continue to experience issues:

1. Check the Mail Archiver logs for detailed error messages
2. Verify all configuration values are correct
3. Ensure you're using the latest version of Mail Archiver
4. Open an issue on the GitHub repository with relevant log excerpts

---

**Note**: Microsoft regularly updates their services and UI. If you notice discrepancies in this guide, please report them so we can keep the documentation up to date.
