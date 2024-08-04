namespace AYCD.Util;

public static class Constants  {
    public static readonly string AuthUrl = "https://autosolve-dashboard-api.aycd.io/api/v1/auth/generate-token";
    public static readonly string ApiUrl = "https://autosolve-api.aycd.io/api/v1";
    public static readonly string TasksUrl = ApiUrl + "/tasks";
    public static readonly string TasksCreateUrl = ApiUrl + "/tasks/create";
    public static readonly string TasksCancelUrl = ApiUrl + "/tasks/cancel";

}