// ============================================================
// Cloud Journey Add-in - Power BI Import Queries
// ============================================================
// 
// HOW TO IMPORT INTO POWER BI:
// 1. Open Power BI Desktop
// 2. Home → Transform data → Transform data (opens Power Query Editor)
// 3. Home → New Source → Blank Query
// 4. View → Advanced Editor
// 5. Replace the content with ONE section below
// 6. Click "Done"
// 7. Rename the query appropriately
// 8. Repeat for each query you want
//
// IMPORTANT: Before importing queries, create these PARAMETERS first:
// 1. Home → Manage Parameters → New Parameter
//    - Name: ApplicationId
//    - Type: Text
//    - Current Value: (your App Insights Application ID)
// 2. Create another parameter:
//    - Name: ApiKey  
//    - Type: Text
//    - Current Value: (your App Insights API Key)
// ============================================================

// ============================================================
// PARAMETER: ApplicationId (Create this FIRST)
// ============================================================
// In Power Query: Home → Manage Parameters → New Parameter
// Name: ApplicationId
// Type: Text
// Current Value: your-application-id-from-azure-portal

// ============================================================
// PARAMETER: ApiKey (Create this SECOND)
// ============================================================
// In Power Query: Home → Manage Parameters → New Parameter
// Name: ApiKey
// Type: Text
// Current Value: your-api-key-from-azure-portal


// ============================================================
// QUERY: AppLaunches
// Daily app launches and unique users (90 days)
// ============================================================
let
    KqlQuery = "customEvents | where timestamp > ago(90d) | where name == 'AppStarted' | summarize Launches = count(), UniqueUsers = dcount(user_Id) by bin(timestamp, 1d) | order by timestamp asc",
    Source = AzureApplicationInsights.Contents(ApplicationId, ApiKey, KqlQuery, []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"timestamp", type datetime}, {"Launches", Int64.Type}, {"UniqueUsers", Int64.Type}}),
    #"Renamed Columns" = Table.RenameColumns(#"Changed Type",{{"timestamp", "Date"}})
in
    #"Renamed Columns"


// ============================================================
// QUERY: VersionDistribution
// User counts by application version (30 days)
// ============================================================
let
    KqlQuery = "customEvents | where timestamp > ago(30d) | where name == 'AppStarted' | extend Version = tostring(customDimensions.Version) | summarize Users = dcount(user_Id), Launches = count(), LastSeen = max(timestamp) by Version | order by LastSeen desc",
    Source = AzureApplicationInsights.Contents(ApplicationId, ApiKey, KqlQuery, []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"Version", type text}, {"Users", Int64.Type}, {"Launches", Int64.Type}, {"LastSeen", type datetime}})
in
    #"Changed Type"


// ============================================================
// QUERY: UserActivity
// Individual user activity details (30 days)
// ============================================================
let
    KqlQuery = "customEvents | where timestamp > ago(30d) | where name == 'AppStarted' | extend Version = tostring(customDimensions.Version) | summarize FirstSeen = min(timestamp), LastSeen = max(timestamp), Sessions = count(), LatestVersion = take_any(Version) by user_Id | order by LastSeen desc",
    Source = AzureApplicationInsights.Contents(ApplicationId, ApiKey, KqlQuery, []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"user_Id", type text}, {"FirstSeen", type datetime}, {"LastSeen", type datetime}, {"Sessions", Int64.Type}, {"LatestVersion", type text}}),
    #"Renamed Columns" = Table.RenameColumns(#"Changed Type",{{"user_Id", "UserId"}})
in
    #"Renamed Columns"


// ============================================================
// QUERY: ErrorsByType
// Error breakdown by type and message (30 days)
// ============================================================
let
    KqlQuery = "exceptions | where timestamp > ago(30d) | extend Version = tostring(customDimensions.Version) | summarize ErrorCount = count(), AffectedUsers = dcount(user_Id), FirstOccurrence = min(timestamp), LastOccurrence = max(timestamp) by type, outerMessage, Version | order by ErrorCount desc | take 50",
    Source = AzureApplicationInsights.Contents(ApplicationId, ApiKey, KqlQuery, []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"type", type text}, {"outerMessage", type text}, {"Version", type text}, {"ErrorCount", Int64.Type}, {"AffectedUsers", Int64.Type}, {"FirstOccurrence", type datetime}, {"LastOccurrence", type datetime}}),
    #"Renamed Columns" = Table.RenameColumns(#"Changed Type",{{"type", "ErrorType"}, {"outerMessage", "Message"}})
in
    #"Renamed Columns"


// ============================================================
// QUERY: DailyErrorTrend
// Daily error counts (90 days)
// ============================================================
let
    KqlQuery = "exceptions | where timestamp > ago(90d) | summarize Errors = count(), AffectedUsers = dcount(user_Id) by bin(timestamp, 1d) | order by timestamp asc",
    Source = AzureApplicationInsights.Contents(ApplicationId, ApiKey, KqlQuery, []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"timestamp", type datetime}, {"Errors", Int64.Type}, {"AffectedUsers", Int64.Type}}),
    #"Renamed Columns" = Table.RenameColumns(#"Changed Type",{{"timestamp", "Date"}})
in
    #"Renamed Columns"


// ============================================================
// QUERY: FeatureUsage
// Feature/event usage ranking (30 days)
// ============================================================
let
    KqlQuery = "customEvents | where timestamp > ago(30d) | where name !in ('AppStarted', 'AppExited') | summarize UsageCount = count(), UniqueUsers = dcount(user_Id) by name | order by UsageCount desc",
    Source = AzureApplicationInsights.Contents(ApplicationId, ApiKey, KqlQuery, []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"name", type text}, {"UsageCount", Int64.Type}, {"UniqueUsers", Int64.Type}}),
    #"Renamed Columns" = Table.RenameColumns(#"Changed Type",{{"name", "FeatureName"}})
in
    #"Renamed Columns"


// ============================================================
// QUERY: UpdateEvents
// Auto-update event tracking (30 days)
// ============================================================
let
    KqlQuery = "customEvents | where timestamp > ago(30d) | where name in ('UpdateCheckStarted', 'UpdateCheckSuccess', 'UpdateCheckFailed', 'UpdateDetected', 'UpdateApplied') | summarize Count = count() by name, bin(timestamp, 1d) | order by timestamp asc",
    Source = AzureApplicationInsights.Contents(ApplicationId, ApiKey, KqlQuery, []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"name", type text}, {"timestamp", type datetime}, {"Count", Int64.Type}}),
    #"Renamed Columns" = Table.RenameColumns(#"Changed Type",{{"name", "EventName"}, {"timestamp", "Date"}})
in
    #"Renamed Columns"


// ============================================================
// QUERY: WeeklyActiveUsers
// WAU trend (90 days)
// ============================================================
let
    KqlQuery = "customEvents | where timestamp > ago(90d) | where name == 'AppStarted' | summarize WAU = dcount(user_Id) by bin(timestamp, 7d) | order by timestamp asc",
    Source = AzureApplicationInsights.Contents(ApplicationId, ApiKey, KqlQuery, []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"timestamp", type datetime}, {"WAU", Int64.Type}}),
    #"Renamed Columns" = Table.RenameColumns(#"Changed Type",{{"timestamp", "WeekStart"}})
in
    #"Renamed Columns"


// ============================================================
// QUERY: KPISummary
// 7-day KPI snapshot
// ============================================================
let
    KqlQuery = "let appStarts = customEvents | where timestamp > ago(7d) | where name == 'AppStarted'; let errors = exceptions | where timestamp > ago(7d); print TotalLaunches = toscalar(appStarts | count), UniqueUsers = toscalar(appStarts | summarize dcount(user_Id)), TotalErrors = toscalar(errors | count), AffectedUsers = toscalar(errors | summarize dcount(user_Id))",
    Source = AzureApplicationInsights.Contents(ApplicationId, ApiKey, KqlQuery, [])
in
    Source


// ============================================================
// QUERY: TabNavigation
// Tab/feature navigation patterns (30 days)
// ============================================================
let
    KqlQuery = "customEvents | where timestamp > ago(30d) | where name == 'TabNavigated' | extend TabName = tostring(customDimensions.TabName) | where isnotempty(TabName) | summarize Views = count(), UniqueUsers = dcount(user_Id) by TabName | order by Views desc",
    Source = AzureApplicationInsights.Contents(ApplicationId, ApiKey, KqlQuery, []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"TabName", type text}, {"Views", Int64.Type}, {"UniqueUsers", Int64.Type}})
in
    #"Changed Type"


// ============================================================
// QUERY: SessionDuration
// Average session duration by version (30 days)
// ============================================================
let
    KqlQuery = "customEvents | where timestamp > ago(30d) | where name in ('AppStarted', 'AppExited') | extend Version = tostring(customDimensions.Version) | summarize StartTime = minif(timestamp, name == 'AppStarted'), EndTime = maxif(timestamp, name == 'AppExited') by session_Id, Version | where isnotempty(StartTime) and isnotempty(EndTime) | extend DurationMinutes = datetime_diff('minute', EndTime, StartTime) | where DurationMinutes > 0 and DurationMinutes < 480 | summarize AvgDuration = avg(DurationMinutes), Sessions = count() by Version | order by Sessions desc",
    Source = AzureApplicationInsights.Contents(ApplicationId, ApiKey, KqlQuery, []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"Version", type text}, {"AvgDuration", type number}, {"Sessions", Int64.Type}})
in
    #"Changed Type"
