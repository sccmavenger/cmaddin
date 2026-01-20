// ============================================================
// Cloud Journey Add-in - Power BI Telemetry Dashboard Queries
// ============================================================
// 
// HOW TO USE:
// 1. Open Power BI Desktop
// 2. Get Data → Azure → Azure Application Insights
// 3. Enter your Application ID and API Key
// 4. Use these queries in the Advanced Editor
//
// Application Insights Resource: appi-cloudjourney-addin
// ============================================================

// ============================================================
// QUERY 1: App Launches (Daily)
// ============================================================
let
    Source = AzureApplicationInsights.Contents("YOUR_APP_ID", "YOUR_API_KEY", "customEvents | where timestamp > ago(90d) | where name == 'AppStarted' | extend Version = tostring(customDimensions.Version), OS = tostring(customDimensions.OS) | summarize Launches = count(), UniqueUsers = dcount(user_Id) by bin(timestamp, 1d) | order by timestamp asc", []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"timestamp", type datetime}, {"Launches", Int64.Type}, {"UniqueUsers", Int64.Type}})
in
    #"Changed Type"

// ============================================================
// QUERY 2: Version Distribution
// ============================================================
let
    Source = AzureApplicationInsights.Contents("YOUR_APP_ID", "YOUR_API_KEY", "customEvents | where timestamp > ago(30d) | where name == 'AppStarted' | extend Version = tostring(customDimensions.Version) | summarize Users = dcount(user_Id), Launches = count(), LastSeen = max(timestamp) by Version | order by LastSeen desc", []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"Version", type text}, {"Users", Int64.Type}, {"Launches", Int64.Type}, {"LastSeen", type datetime}})
in
    #"Changed Type"

// ============================================================
// QUERY 3: User Activity
// ============================================================
let
    Source = AzureApplicationInsights.Contents("YOUR_APP_ID", "YOUR_API_KEY", "customEvents | where timestamp > ago(30d) | where name == 'AppStarted' | extend Version = tostring(customDimensions.Version) | summarize FirstSeen = min(timestamp), LastSeen = max(timestamp), Sessions = count(), LatestVersion = arg_max(timestamp, Version) by user_Id | project user_Id, FirstSeen, LastSeen, Sessions, LatestVersion = LatestVersion1 | order by LastSeen desc", []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"user_Id", type text}, {"FirstSeen", type datetime}, {"LastSeen", type datetime}, {"Sessions", Int64.Type}, {"LatestVersion", type text}})
in
    #"Changed Type"

// ============================================================
// QUERY 4: Errors by Type
// ============================================================
let
    Source = AzureApplicationInsights.Contents("YOUR_APP_ID", "YOUR_API_KEY", "exceptions | where timestamp > ago(30d) | extend Version = tostring(customDimensions.Version) | summarize ErrorCount = count(), AffectedUsers = dcount(user_Id), FirstOccurrence = min(timestamp), LastOccurrence = max(timestamp) by type, outerMessage, Version | order by ErrorCount desc", []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"type", type text}, {"outerMessage", type text}, {"Version", type text}, {"ErrorCount", Int64.Type}, {"AffectedUsers", Int64.Type}, {"FirstOccurrence", type datetime}, {"LastOccurrence", type datetime}})
in
    #"Changed Type"

// ============================================================
// QUERY 5: Daily Error Trend
// ============================================================
let
    Source = AzureApplicationInsights.Contents("YOUR_APP_ID", "YOUR_API_KEY", "exceptions | where timestamp > ago(90d) | summarize Errors = count(), AffectedUsers = dcount(user_Id) by bin(timestamp, 1d) | order by timestamp asc", []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"timestamp", type datetime}, {"Errors", Int64.Type}, {"AffectedUsers", Int64.Type}})
in
    #"Changed Type"

// ============================================================
// QUERY 6: Feature Usage
// ============================================================
let
    Source = AzureApplicationInsights.Contents("YOUR_APP_ID", "YOUR_API_KEY", "customEvents | where timestamp > ago(30d) | where name !in ('AppStarted', 'AppExited') | summarize UsageCount = count(), UniqueUsers = dcount(user_Id) by name | order by UsageCount desc", []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"name", type text}, {"UsageCount", Int64.Type}, {"UniqueUsers", Int64.Type}})
in
    #"Changed Type"

// ============================================================
// QUERY 7: Update Check Results
// ============================================================
let
    Source = AzureApplicationInsights.Contents("YOUR_APP_ID", "YOUR_API_KEY", "customEvents | where timestamp > ago(30d) | where name in ('UpdateCheckStarted', 'UpdateCheckSuccess', 'UpdateCheckFailed', 'UpdateDetected', 'UpdateApplied') | summarize Count = count() by name, bin(timestamp, 1d) | order by timestamp asc", []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"name", type text}, {"timestamp", type datetime}, {"Count", Int64.Type}})
in
    #"Changed Type"

// ============================================================
// QUERY 8: Update Failures Analysis
// ============================================================
let
    Source = AzureApplicationInsights.Contents("YOUR_APP_ID", "YOUR_API_KEY", "customEvents | where timestamp > ago(30d) | where name == 'UpdateCheckFailed' | extend ErrorType = tostring(customDimensions.ErrorType), Message = tostring(customDimensions.Message), IsAuthenticated = tostring(customDimensions.IsAuthenticated) | summarize Count = count(), Users = dcount(user_Id) by ErrorType, Message, IsAuthenticated | order by Count desc", []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"ErrorType", type text}, {"Message", type text}, {"IsAuthenticated", type text}, {"Count", Int64.Type}, {"Users", Int64.Type}})
in
    #"Changed Type"

// ============================================================
// QUERY 9: Session Duration
// ============================================================
let
    Source = AzureApplicationInsights.Contents("YOUR_APP_ID", "YOUR_API_KEY", "customEvents | where timestamp > ago(30d) | where name in ('AppStarted', 'AppExited') | extend Version = tostring(customDimensions.Version) | order by session_Id, timestamp asc | serialize | extend NextEvent = next(name), NextTimestamp = next(timestamp), NextSession = next(session_Id) | where name == 'AppStarted' and NextEvent == 'AppExited' and session_Id == NextSession | extend DurationMinutes = datetime_diff('minute', NextTimestamp, timestamp) | where DurationMinutes > 0 and DurationMinutes < 480 | summarize AvgDuration = avg(DurationMinutes), MedianDuration = percentile(DurationMinutes, 50), Sessions = count() by Version | order by Sessions desc", []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"Version", type text}, {"AvgDuration", type number}, {"MedianDuration", type number}, {"Sessions", Int64.Type}})
in
    #"Changed Type"

// ============================================================
// QUERY 10: Peak Usage Hours
// ============================================================
let
    Source = AzureApplicationInsights.Contents("YOUR_APP_ID", "YOUR_API_KEY", "customEvents | where timestamp > ago(30d) | where name == 'AppStarted' | extend Hour = hourofday(timestamp), DayOfWeek = dayofweek(timestamp) | summarize Launches = count() by Hour, DayOfWeek | order by DayOfWeek asc, Hour asc", []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"Hour", Int64.Type}, {"DayOfWeek", type duration}, {"Launches", Int64.Type}})
in
    #"Changed Type"

// ============================================================
// QUERY 11: Crash Detection (Incomplete Sessions)
// ============================================================
let
    Source = AzureApplicationInsights.Contents("YOUR_APP_ID", "YOUR_API_KEY", "let Started = customEvents | where timestamp > ago(7d) | where name == 'AppStarted' | project session_Id, user_Id, StartTime = timestamp, Version = tostring(customDimensions.Version); let Exited = customEvents | where timestamp > ago(7d) | where name == 'AppExited' | project session_Id; Started | join kind=leftanti Exited on session_Id | summarize CrashCount = count(), AffectedUsers = dcount(user_Id) by bin(StartTime, 1d), Version | order by StartTime desc", []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"StartTime", type datetime}, {"Version", type text}, {"CrashCount", Int64.Type}, {"AffectedUsers", Int64.Type}})
in
    #"Changed Type"

// ============================================================
// QUERY 12: Tab Navigation (Feature Engagement)
// ============================================================
let
    Source = AzureApplicationInsights.Contents("YOUR_APP_ID", "YOUR_API_KEY", "customEvents | where timestamp > ago(30d) | where name == 'TabNavigated' or name contains 'Tab' | extend TabName = tostring(customDimensions.TabName) | where isnotempty(TabName) | summarize Views = count(), UniqueUsers = dcount(user_Id) by TabName | order by Views desc", []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"TabName", type text}, {"Views", Int64.Type}, {"UniqueUsers", Int64.Type}})
in
    #"Changed Type"

// ============================================================
// QUERY 13: Data Source Usage
// ============================================================
let
    Source = AzureApplicationInsights.Contents("YOUR_APP_ID", "YOUR_API_KEY", "customEvents | where timestamp > ago(30d) | where name == 'DataRefreshed' | extend DataSource = tostring(customDimensions.DataSource) | summarize Refreshes = count(), UniqueUsers = dcount(user_Id) by DataSource | order by Refreshes desc", []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"DataSource", type text}, {"Refreshes", Int64.Type}, {"UniqueUsers", Int64.Type}})
in
    #"Changed Type"

// ============================================================
// QUERY 14: Weekly Active Users (WAU)
// ============================================================
let
    Source = AzureApplicationInsights.Contents("YOUR_APP_ID", "YOUR_API_KEY", "customEvents | where timestamp > ago(90d) | where name == 'AppStarted' | summarize WAU = dcount(user_Id) by bin(timestamp, 7d) | order by timestamp asc", []),
    #"Changed Type" = Table.TransformColumnTypes(Source,{{"timestamp", type datetime}, {"WAU", Int64.Type}})
in
    #"Changed Type"

// ============================================================
// QUERY 15: KPI Summary (Current State)
// ============================================================
let
    Source = AzureApplicationInsights.Contents("YOUR_APP_ID", "YOUR_API_KEY", "let appStarts = customEvents | where timestamp > ago(7d) | where name == 'AppStarted'; let errors = exceptions | where timestamp > ago(7d); let latestVersion = '3.16.49.0'; print TotalLaunches = toscalar(appStarts | count), UniqueUsers = toscalar(appStarts | dcount(user_Id)), UsersOnLatest = toscalar(appStarts | extend Version = tostring(customDimensions.Version) | where Version == latestVersion | dcount(user_Id)), TotalErrors = toscalar(errors | count), UsersWithErrors = toscalar(errors | dcount(user_Id)), Period = '7 days'", [])
in
    Source
