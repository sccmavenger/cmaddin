<#
.SYNOPSIS
    Creates a Power BI Template (.pbit) file for CloudJourneyAddin Telemetry Dashboard
    
.DESCRIPTION
    This script creates a .pbit template file that can be opened directly in Power BI Desktop.
    Since .pbit files are ZIP archives containing JSON definitions, we construct it programmatically.
    
.NOTES
    Author: CloudJourneyAddin Team
    Date: January 2026
    
.EXAMPLE
    .\Create-PowerBITemplate.ps1
    Creates CloudJourneyAddin-Telemetry.pbit in the current directory
#>

param(
    [string]$OutputPath = "$PSScriptRoot\CloudJourneyAddin-Telemetry.pbit",
    [string]$ApplicationId = "YOUR_APP_ID",
    [string]$ApiKey = "YOUR_API_KEY",
    [string]$LatestVersion = "3.17.3"
)

Write-Host "╔══════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     CloudJourneyAddin - Power BI Template Generator              ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Create temp directory for building the template
$tempDir = Join-Path $env:TEMP "PowerBITemplate_$(Get-Date -Format 'yyyyMMddHHmmss')"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

Write-Host "📁 Creating template structure..." -ForegroundColor Yellow

# DataModelSchema - the core semantic model
$dataModelSchema = @{
    name = "CloudJourneyAddin-Telemetry"
    compatibilityLevel = 1567
    model = @{
        culture = "en-US"
        tables = @(
            @{
                name = "AppLaunches"
                columns = @(
                    @{ name = "Date"; dataType = "dateTime"; sourceColumn = "timestamp" }
                    @{ name = "Launches"; dataType = "int64"; sourceColumn = "Launches" }
                    @{ name = "UniqueUsers"; dataType = "int64"; sourceColumn = "UniqueUsers" }
                )
                partitions = @(@{
                    name = "AppLaunches"
                    source = @{
                        type = "m"
                        expression = @(
                            "let"
                            "    KqlQuery = ""customEvents | where timestamp > ago(90d) | where name == 'AppStarted' | extend Version = tostring(customDimensions.Version) | summarize Launches = count(), UniqueUsers = dcount(user_Id) by bin(timestamp, 1d) | order by timestamp asc"","
                            "    Source = AzureApplicationInsights.Contents(#""ApplicationId"", #""ApiKey"", KqlQuery, []),"
                            "    #""Changed Type"" = Table.TransformColumnTypes(Source,{{""timestamp"", type datetime}, {""Launches"", Int64.Type}, {""UniqueUsers"", Int64.Type}})"
                            "in"
                            "    #""Changed Type"""
                        )
                    }
                })
            }
            @{
                name = "VersionDistribution"
                columns = @(
                    @{ name = "Version"; dataType = "string"; sourceColumn = "Version" }
                    @{ name = "Users"; dataType = "int64"; sourceColumn = "Users" }
                    @{ name = "Launches"; dataType = "int64"; sourceColumn = "Launches" }
                    @{ name = "LastSeen"; dataType = "dateTime"; sourceColumn = "LastSeen" }
                )
                partitions = @(@{
                    name = "VersionDistribution"
                    source = @{
                        type = "m"
                        expression = @(
                            "let"
                            "    KqlQuery = ""customEvents | where timestamp > ago(30d) | where name == 'AppStarted' | extend Version = tostring(customDimensions.Version) | summarize Users = dcount(user_Id), Launches = count(), LastSeen = max(timestamp) by Version | order by LastSeen desc"","
                            "    Source = AzureApplicationInsights.Contents(#""ApplicationId"", #""ApiKey"", KqlQuery, []),"
                            "    #""Changed Type"" = Table.TransformColumnTypes(Source,{{""Version"", type text}, {""Users"", Int64.Type}, {""Launches"", Int64.Type}, {""LastSeen"", type datetime}})"
                            "in"
                            "    #""Changed Type"""
                        )
                    }
                })
            }
            @{
                name = "KPISummary"
                columns = @(
                    @{ name = "TotalLaunches"; dataType = "int64"; sourceColumn = "TotalLaunches" }
                    @{ name = "UniqueUsers"; dataType = "int64"; sourceColumn = "UniqueUsers" }
                    @{ name = "UsersOnLatest"; dataType = "int64"; sourceColumn = "UsersOnLatest" }
                    @{ name = "TotalErrors"; dataType = "int64"; sourceColumn = "TotalErrors" }
                    @{ name = "UsersWithErrors"; dataType = "int64"; sourceColumn = "UsersWithErrors" }
                )
                partitions = @(@{
                    name = "KPISummary"
                    source = @{
                        type = "m"
                        expression = @(
                            "let"
                            "    KqlQuery = ""let appStarts = customEvents | where timestamp > ago(7d) | where name == 'AppStarted'; let errors = exceptions | where timestamp > ago(7d); let latestVersion = '$LatestVersion'; print TotalLaunches = toscalar(appStarts | count), UniqueUsers = toscalar(appStarts | dcount(user_Id)), UsersOnLatest = toscalar(appStarts | extend Version = tostring(customDimensions.Version) | where Version == latestVersion | dcount(user_Id)), TotalErrors = toscalar(errors | count), UsersWithErrors = toscalar(errors | dcount(user_Id))"","
                            "    Source = AzureApplicationInsights.Contents(#""ApplicationId"", #""ApiKey"", KqlQuery, [])"
                            "in"
                            "    Source"
                        )
                    }
                })
            }
            @{
                name = "DailyErrorTrend"
                columns = @(
                    @{ name = "Date"; dataType = "dateTime"; sourceColumn = "timestamp" }
                    @{ name = "Errors"; dataType = "int64"; sourceColumn = "Errors" }
                    @{ name = "AffectedUsers"; dataType = "int64"; sourceColumn = "AffectedUsers" }
                )
                partitions = @(@{
                    name = "DailyErrorTrend"
                    source = @{
                        type = "m"
                        expression = @(
                            "let"
                            "    KqlQuery = ""exceptions | where timestamp > ago(90d) | summarize Errors = count(), AffectedUsers = dcount(user_Id) by bin(timestamp, 1d) | order by timestamp asc"","
                            "    Source = AzureApplicationInsights.Contents(#""ApplicationId"", #""ApiKey"", KqlQuery, []),"
                            "    #""Changed Type"" = Table.TransformColumnTypes(Source,{{""timestamp"", type datetime}, {""Errors"", Int64.Type}, {""AffectedUsers"", Int64.Type}})"
                            "in"
                            "    #""Changed Type"""
                        )
                    }
                })
            }
            @{
                name = "FeatureUsage"
                columns = @(
                    @{ name = "FeatureName"; dataType = "string"; sourceColumn = "name" }
                    @{ name = "UsageCount"; dataType = "int64"; sourceColumn = "UsageCount" }
                    @{ name = "UniqueUsers"; dataType = "int64"; sourceColumn = "UniqueUsers" }
                )
                partitions = @(@{
                    name = "FeatureUsage"
                    source = @{
                        type = "m"
                        expression = @(
                            "let"
                            "    KqlQuery = ""customEvents | where timestamp > ago(30d) | where name !in ('AppStarted', 'AppExited') | summarize UsageCount = count(), UniqueUsers = dcount(user_Id) by name | order by UsageCount desc"","
                            "    Source = AzureApplicationInsights.Contents(#""ApplicationId"", #""ApiKey"", KqlQuery, []),"
                            "    #""Changed Type"" = Table.TransformColumnTypes(Source,{{""name"", type text}, {""UsageCount"", Int64.Type}, {""UniqueUsers"", Int64.Type}})"
                            "in"
                            "    #""Changed Type"""
                        )
                    }
                })
            }
            @{
                name = "WeeklyActiveUsers"
                columns = @(
                    @{ name = "WeekStart"; dataType = "dateTime"; sourceColumn = "timestamp" }
                    @{ name = "WAU"; dataType = "int64"; sourceColumn = "WAU" }
                )
                partitions = @(@{
                    name = "WeeklyActiveUsers"
                    source = @{
                        type = "m"
                        expression = @(
                            "let"
                            "    KqlQuery = ""customEvents | where timestamp > ago(90d) | where name == 'AppStarted' | summarize WAU = dcount(user_Id) by bin(timestamp, 7d) | order by timestamp asc"","
                            "    Source = AzureApplicationInsights.Contents(#""ApplicationId"", #""ApiKey"", KqlQuery, []),"
                            "    #""Changed Type"" = Table.TransformColumnTypes(Source,{{""timestamp"", type datetime}, {""WAU"", Int64.Type}})"
                            "in"
                            "    #""Changed Type"""
                        )
                    }
                })
            }
        )
        expressions = @(
            @{
                name = "ApplicationId"
                kind = "m"
                expression = """$ApplicationId"" meta [IsParameterQuery=true, Type=""Text"", IsParameterQueryRequired=true]"
            }
            @{
                name = "ApiKey"
                kind = "m"
                expression = """$ApiKey"" meta [IsParameterQuery=true, Type=""Text"", IsParameterQueryRequired=true]"
            }
        )
    }
}

# Convert to JSON and save
$dataModelJson = $dataModelSchema | ConvertTo-Json -Depth 20
$dataModelJson | Out-File -FilePath (Join-Path $tempDir "DataModelSchema") -Encoding UTF8 -NoNewline

Write-Host "✅ DataModelSchema created" -ForegroundColor Green

# Version file
"1.23" | Out-File -FilePath (Join-Path $tempDir "Version") -Encoding ASCII -NoNewline
Write-Host "✅ Version file created" -ForegroundColor Green

# [Content_Types].xml
$contentTypes = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="json" ContentType="application/json"/>
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Override PartName="/DataModelSchema" ContentType=""/>
  <Override PartName="/Version" ContentType=""/>
  <Override PartName="/Settings" ContentType="application/json"/>
  <Override PartName="/Metadata" ContentType="application/json"/>
  <Override PartName="/DiagramState" ContentType="application/json"/>
</Types>
"@
$contentTypes | Out-File -FilePath (Join-Path $tempDir "[Content_Types].xml") -Encoding UTF8 -NoNewline
Write-Host "✅ Content_Types.xml created" -ForegroundColor Green

# Settings
$settings = @{
    Version = "1.0"
    Settings = @{
        ParameterQueryTargetLocales = @("en-US")
        UseEnhancedTooltips = $true
    }
}
$settings | ConvertTo-Json | Out-File -FilePath (Join-Path $tempDir "Settings") -Encoding UTF8 -NoNewline
Write-Host "✅ Settings created" -ForegroundColor Green

# Metadata
$metadata = @{
    Version = 3
    AutoCreatedRelationships = @()
    Culture = "en-US"
}
$metadata | ConvertTo-Json | Out-File -FilePath (Join-Path $tempDir "Metadata") -Encoding UTF8 -NoNewline
Write-Host "✅ Metadata created" -ForegroundColor Green

# DiagramState
$diagramState = @{
    Version = 0
    Diagrams = @()
}
$diagramState | ConvertTo-Json | Out-File -FilePath (Join-Path $tempDir "DiagramState") -Encoding UTF8 -NoNewline
Write-Host "✅ DiagramState created" -ForegroundColor Green

# SecurityBindings (empty)
"" | Out-File -FilePath (Join-Path $tempDir "SecurityBindings") -Encoding UTF8 -NoNewline
Write-Host "✅ SecurityBindings created" -ForegroundColor Green

# Create _rels folder and .rels file
$relsDir = Join-Path $tempDir "_rels"
New-Item -ItemType Directory -Path $relsDir -Force | Out-Null

$rels = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Type="http://schemas.microsoft.com/powerbi/2015/relationships/dataModelSchema" Target="/DataModelSchema" Id="R1"/>
  <Relationship Type="http://schemas.microsoft.com/powerbi/2015/relationships/version" Target="/Version" Id="R2"/>
  <Relationship Type="http://schemas.microsoft.com/powerbi/2015/relationships/settings" Target="/Settings" Id="R3"/>
  <Relationship Type="http://schemas.microsoft.com/powerbi/2015/relationships/metadata" Target="/Metadata" Id="R4"/>
  <Relationship Type="http://schemas.microsoft.com/powerbi/2015/relationships/diagramState" Target="/DiagramState" Id="R5"/>
</Relationships>
"@
$rels | Out-File -FilePath (Join-Path $relsDir ".rels") -Encoding UTF8 -NoNewline
Write-Host "✅ Relationships created" -ForegroundColor Green

# Create the .pbit file (ZIP archive)
Write-Host ""
Write-Host "📦 Packaging .pbit file..." -ForegroundColor Yellow

# Remove existing file if it exists
if (Test-Path $OutputPath) {
    Remove-Item $OutputPath -Force
}

# Create ZIP
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($tempDir, $OutputPath)

# Cleanup
Remove-Item $tempDir -Recurse -Force

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║  ✅ Power BI Template Created Successfully!                      ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "📄 File: $OutputPath" -ForegroundColor Cyan
Write-Host "📏 Size: $([math]::Round((Get-Item $OutputPath).Length / 1KB, 2)) KB" -ForegroundColor Cyan
Write-Host ""
Write-Host "📋 Next Steps:" -ForegroundColor Yellow
Write-Host "   1. Double-click the .pbit file to open in Power BI Desktop"
Write-Host "   2. Enter your Application Insights credentials when prompted:"
Write-Host "      • ApplicationId: Found in Azure Portal → App Insights → API Access"
Write-Host "      • ApiKey: Create in Azure Portal → App Insights → API Access → Create API Key"
Write-Host "   3. Click 'Load' to connect to your telemetry data"
Write-Host "   4. Build visuals using the pre-configured data model"
Write-Host ""
Write-Host "📊 Included Tables:" -ForegroundColor Yellow
Write-Host "   • AppLaunches - Daily active users and launch counts"
Write-Host "   • VersionDistribution - Users per application version"
Write-Host "   • KPISummary - 7-day key performance indicators"
Write-Host "   • DailyErrorTrend - Error counts over 90 days"
Write-Host "   • FeatureUsage - Feature/event usage statistics"
Write-Host "   • WeeklyActiveUsers - WAU trend over 90 days"
Write-Host ""

return $OutputPath
