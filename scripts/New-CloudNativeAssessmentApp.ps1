<#
.SYNOPSIS
    Creates an Entra ID (Azure AD) app registration for Cloud Native Assessment.

.DESCRIPTION
    This script creates an app registration with the required Microsoft Graph API 
    permissions for Cloud Native Assessment. Use this when your organization cannot
    use the default Microsoft public app registration.

.PARAMETER AppName
    The display name for the app registration. Default: "Cloud Native Assessment"

.PARAMETER GrantAdminConsent
    If specified, grants admin consent for the permissions (requires Global Admin or 
    Privileged Role Administrator).

.EXAMPLE
    .\New-CloudNativeAssessmentApp.ps1
    Creates an app named "Cloud Native Assessment" with required permissions.

.EXAMPLE
    .\New-CloudNativeAssessmentApp.ps1 -AppName "Cloud Native Assessment - Contoso" -GrantAdminConsent
    Creates a custom-named app and grants admin consent.

.NOTES
    Author: Cloud Native Assessment Team
    Requires: Microsoft.Graph PowerShell module
    
    After running this script:
    1. Copy the Client ID and Tenant ID from the output
    2. Open Cloud Native Assessment in ConfigMgr Console
    3. Click the Auth button in the toolbar
    4. Enter your Client ID and Tenant ID
    5. Click Save and reconnect
#>

param(
    [Parameter()]
    [string]$AppName = "Cloud Native Assessment",
    
    [Parameter()]
    [switch]$GrantAdminConsent
)

$ErrorActionPreference = "Stop"

# Required Graph API permissions (delegated)
$RequiredPermissions = @(
    @{ Name = "DeviceManagementManagedDevices.Read.All"; Id = "f51be20a-obd6-4acc-8f9f-25ebb3e25a11" }
    @{ Name = "DeviceManagementConfiguration.Read.All"; Id = "dc377aa6-52d8-4e23-b271-2a7ae04cedf3" }
    @{ Name = "DeviceManagementApps.Read.All"; Id = "4edf5f54-4666-44af-9de9-0144fb4b6e9c" }
    @{ Name = "Device.Read.All"; Id = "11d4cd79-5ba5-460f-803f-e22c8ab85ccd" }
    @{ Name = "Directory.Read.All"; Id = "06da0dbc-49e2-44d2-8312-53f166ab848a" }
    @{ Name = "User.Read"; Id = "e1fe6dd8-ba31-4d61-89e7-88639da4683d" }
    @{ Name = "Organization.Read.All"; Id = "4908d5b9-3fb2-4b1e-9571-1b3dffacd109" }
    @{ Name = "Policy.Read.All"; Id = "572fea84-0151-49b2-9301-11cb16974376" }
)

# Microsoft Graph App ID (constant)
$MicrosoftGraphAppId = "00000003-0000-0000-c000-000000000000"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Cloud Native Assessment App Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check for Microsoft.Graph module
Write-Host "Checking for Microsoft.Graph module..." -ForegroundColor Yellow
if (-not (Get-Module -ListAvailable -Name Microsoft.Graph.Applications)) {
    Write-Host "Microsoft.Graph module not found. Installing..." -ForegroundColor Yellow
    Install-Module Microsoft.Graph -Scope CurrentUser -Force -AllowClobber
}

# Import required modules
Import-Module Microsoft.Graph.Applications -ErrorAction Stop

# Connect to Microsoft Graph
Write-Host "Connecting to Microsoft Graph..." -ForegroundColor Yellow
Write-Host "A browser window will open for authentication." -ForegroundColor Gray
Write-Host ""

$Scopes = @(
    "Application.ReadWrite.All"
    "AppRoleAssignment.ReadWrite.All"
)

if ($GrantAdminConsent) {
    $Scopes += "DelegatedPermissionGrant.ReadWrite.All"
}

Connect-MgGraph -Scopes $Scopes -NoWelcome

# Get tenant info
$Organization = Get-MgOrganization
$TenantId = $Organization.Id
$TenantName = $Organization.DisplayName

Write-Host ""
Write-Host "Connected to tenant: $TenantName ($TenantId)" -ForegroundColor Green
Write-Host ""

# Check if app already exists
Write-Host "Checking for existing app registration..." -ForegroundColor Yellow
$ExistingApp = Get-MgApplication -Filter "displayName eq '$AppName'" -ErrorAction SilentlyContinue

if ($ExistingApp) {
    Write-Host ""
    Write-Host "WARNING: An app named '$AppName' already exists!" -ForegroundColor Yellow
    Write-Host "  Client ID: $($ExistingApp.AppId)" -ForegroundColor Gray
    Write-Host ""
    $Response = Read-Host "Do you want to delete and recreate it? (y/N)"
    
    if ($Response -eq 'y' -or $Response -eq 'Y') {
        Write-Host "Deleting existing app..." -ForegroundColor Yellow
        Remove-MgApplication -ApplicationId $ExistingApp.Id
        Start-Sleep -Seconds 2
    } else {
        Write-Host "Aborted. Use a different -AppName or delete the existing app manually." -ForegroundColor Red
        Disconnect-MgGraph | Out-Null
        exit 1
    }
}

# Build required resource access
Write-Host "Building permission requirements..." -ForegroundColor Yellow

$ResourceAccess = $RequiredPermissions | ForEach-Object {
    @{
        Id = $_.Id
        Type = "Scope"  # Delegated permission
    }
}

$RequiredResourceAccess = @(
    @{
        ResourceAppId = $MicrosoftGraphAppId
        ResourceAccess = $ResourceAccess
    }
)

# Create the app registration
Write-Host "Creating app registration '$AppName'..." -ForegroundColor Yellow

$AppParams = @{
    DisplayName = $AppName
    SignInAudience = "AzureADMyOrg"  # Single tenant
    RequiredResourceAccess = $RequiredResourceAccess
    PublicClient = @{
        RedirectUris = @(
            "http://localhost"
            "https://login.microsoftonline.com/common/oauth2/nativeclient"
            "urn:ietf:wg:oauth:2.0:oob"
        )
    }
    IsFallbackPublicClient = $true
}

$NewApp = New-MgApplication -BodyParameter $AppParams

Write-Host ""
Write-Host "App registration created successfully!" -ForegroundColor Green
Write-Host ""

# Grant admin consent if requested
if ($GrantAdminConsent) {
    Write-Host "Granting admin consent for permissions..." -ForegroundColor Yellow
    
    # Create service principal first
    $ServicePrincipal = New-MgServicePrincipal -AppId $NewApp.AppId
    
    # Get Microsoft Graph service principal
    $GraphSP = Get-MgServicePrincipal -Filter "appId eq '$MicrosoftGraphAppId'"
    
    # Grant delegated permissions
    $PermissionScopes = ($RequiredPermissions | ForEach-Object { $_.Name }) -join " "
    
    $GrantParams = @{
        ClientId = $ServicePrincipal.Id
        ConsentType = "AllPrincipals"
        ResourceId = $GraphSP.Id
        Scope = $PermissionScopes
    }
    
    New-MgOauth2PermissionGrant -BodyParameter $GrantParams | Out-Null
    
    Write-Host "Admin consent granted!" -ForegroundColor Green
    Write-Host ""
}

# Output results
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Configuration Complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Use these values in Cloud Native Assessment:" -ForegroundColor White
Write-Host ""
Write-Host "  App Name:   $AppName" -ForegroundColor Yellow
Write-Host "  Client ID:  $($NewApp.AppId)" -ForegroundColor Green
Write-Host "  Tenant ID:  $TenantId" -ForegroundColor Green
Write-Host ""
Write-Host "Required Permissions:" -ForegroundColor White
foreach ($Perm in $RequiredPermissions) {
    $Status = if ($GrantAdminConsent) { "[Granted]" } else { "[Requires Consent]" }
    Write-Host "  - $($Perm.Name) $Status" -ForegroundColor Gray
}
Write-Host ""

if (-not $GrantAdminConsent) {
    Write-Host "NOTE: Admin consent was not granted." -ForegroundColor Yellow
    Write-Host "Either:" -ForegroundColor Yellow
    Write-Host "  1. Run this script again with -GrantAdminConsent" -ForegroundColor Gray
    Write-Host "  2. Have a Global Admin consent in Azure Portal" -ForegroundColor Gray
    Write-Host "  3. Users will be prompted for consent on first login" -ForegroundColor Gray
    Write-Host ""
}

Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Open Cloud Native Assessment in ConfigMgr Console" -ForegroundColor White
Write-Host "  2. Click the '🔐 Auth' button in the toolbar" -ForegroundColor White
Write-Host "  3. Enter the Client ID and Tenant ID above" -ForegroundColor White
Write-Host "  4. Click Save and reconnect to Microsoft Graph" -ForegroundColor White
Write-Host ""

# Disconnect
Disconnect-MgGraph | Out-Null

Write-Host "Done! Graph connection closed." -ForegroundColor Green
Write-Host ""
