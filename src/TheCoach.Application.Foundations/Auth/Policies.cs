namespace TheCoach.Application.Foundations.Auth;

public static class Policies
{
    public const string ProgramsCreate = "Programs.Create";
    public const string ProgramsAssign = "Programs.Assign";
    public const string ProgramsViewAll = "Programs.ViewAll";
    public const string ProgramsBrowseLibrary = "Programs.BrowseLibrary";
    public const string ComplianceViewAll = "Compliance.ViewAll";
    public const string ComplianceViewOwn = "Compliance.ViewOwn";
    public const string WorkoutLogsCreate = "WorkoutLogs.Create";
    public const string HealthTrackingViewAll = "HealthTracking.ViewAll";
    public const string HealthTrackingViewOwn = "HealthTracking.ViewOwn";
    public const string AIGenerate = "AI.Generate";
    public const string BillingManage = "Billing.Manage";
    public const string UsersInvite = "Users.Invite";
    public const string TenantsManage = "Tenants.Manage";
    public const string CheckInsManage = "CheckIns.Manage";
    public const string CheckInsViewOwn = "CheckIns.ViewOwn";
    public const string CheckInsViewAll = "CheckIns.ViewAll";
}

public static class Roles
{
    public const string SystemAdmin = "SystemAdmin";
    public const string Operator = "Operator";
    public const string HeadCoach = "HeadCoach";
    public const string Coach = "Coach";
    public const string Client = "Client";
    public const string Athlete = "Athlete";
    public const string Member = "Member";
}
