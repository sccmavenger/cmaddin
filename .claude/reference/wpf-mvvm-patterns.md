# WPF & MVVM Patterns - Cloud Native Assessment

## MVVM Architecture

### ViewModelBase
- All ViewModels inherit from `ViewModelBase` which implements `INotifyPropertyChanged`
- Use `SetProperty<T>(ref field, value, [CallerMemberName] propertyName)` for property change notification
- Use `RelayCommand` / `AsyncRelayCommand` for ICommand implementations

### Data Binding Conventions
- Bind to ViewModel properties in XAML: `{Binding PropertyName}`
- Use `ObservableCollection<T>` for list data that changes at runtime
- Use `IValueConverter` implementations in `Converters/` for display transformations
- Existing converters: `RecommendationPriorityConverter`, `ValueConverters.cs` (multiple converters)

### DI Registration
- All services registered in `Services/ServiceRegistration.cs`
- Constructor injection preferred for services
- Singleton pattern for `FileLogger.Instance` and `AzureTelemetryService`

## WPF UI Patterns

### Tab System
- `DashboardWindow.xaml` uses `TabControl` with runtime visibility toggling
- Tab visibility controlled by `TabVisibilityOptions` model
- Command-line switches: `/showtabs:TabName` and `/hidetabs:TabName`
- Special switches: `/demostall`, `/showdevtools`

### Styles
- Fluent Design color palette defined in `Styles/AppStyles.xaml`
- Consistent color system: Primary, Secondary, Success, Warning, Danger, Info
- Card-based layout pattern with rounded corners and shadows

### Dialog Windows
- Modal dialogs for settings: `GraphAuthSettingsWindow`, `AISettingsWindow`, `ConfigMgrServerDialog`
- Device list drill-through: `DeviceListDialog`, `WorkloadDeviceListDialog`
- Use `Owner = this` and `ShowDialog()` for modal behavior

### Code-Behind Pattern
- Views use code-behind for:
  - Mock data initialization when disconnected
  - UI-specific event handling (not business logic)
  - DataContext assignment from DI container
- Business logic stays in Services and ViewModels

## Data Flow

```
Graph API / ConfigMgr API
    ↓
GraphDataService / ConfigMgrAdminService
    ↓
DashboardViewModel (aggregation, cross-referencing)
    ↓
DashboardWindow.xaml (data binding)
    ↓
Child UserControls (tabs, cards)
```

## Common Patterns

### Adding a New Tab
1. Create UserControl XAML in `Views/`
2. Add code-behind with mock data fallback
3. Add tab to `DashboardWindow.xaml` TabControl
4. Add visibility property to `TabVisibilityOptions`
5. Update `DashboardViewModel` if new data binding needed

### Adding a New Service
1. Create service class in `Services/`
2. Inject `FileLogger.Instance` for logging
3. Register in `ServiceRegistration.cs`
4. Inject via constructor in ViewModel or other services

### Mock-First Development
- Every feature must work with `MockDataService` before requiring auth
- Mock data should use realistic values modeled on enterprise fleets
- Check `IsConnected` / `IsGraphConnected` / `IsConfigMgrConnected` flags
