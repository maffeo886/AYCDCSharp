using System.Text.Json;
using AYCD;
using AYCD.Dtos;
using AYCD.Models;

namespace AYCDTest;

public abstract class AycdTest {
    private static string? _apiKey;
    private static Session? _session;
    
    public static void Main(string[] args)  {
        _apiKey = Environment.GetEnvironmentVariable("AYCD_API_KEY");
        if ( _apiKey is null ) {
            Console.WriteLine("Please set the AYCD_API_KEY environment variable");
            return;
        } 
        
        _session = Session.NewSession(_apiKey);
        _session.EnableDebug();
        
        _ = MainAsync(args);
        Console.ReadLine();
    }
    public static async Task MainAsync(string[] args ) {
        //await SendSolve();
        //await TestSolveAndCancel();
        await TestMutliTask();
    }
    public static async Task TestSolveAndCancel() {
        // create solve with task id 1 in a new task that will get canceled in 1 second
        var source = new CancellationTokenSource();
        source.CancelAfter(TimeSpan.FromSeconds(1));

        await SendSolve("1", source.Token);
    }

    public static Task TestMutliTask() {
        for(int i = 1; i <= 10; i++) {
            _ = SendSolve(i.ToString());
        }
        
        return Task.CompletedTask;
    }
    
    public static async Task SendSolve(string taskId, CancellationToken sourceToken = default) {
        Console.WriteLine("Sending solve for task id: " + taskId);
        
        var taskRequest = new CreateTaskDto(
            TaskId: taskId,
            Url: "https://recaptcha.autosolve.io/version/1",
            SiteKey: "6Ld_LMAUAAAAAOIqLSy5XY9-DUKLkAgiDpqtTJ9b",
            Version: CaptchaVersion.ReCaptchaV2Checkbox
        );
        
        var taskResponse = await _session?.Solve(taskRequest, TimeSpan.FromMinutes(5), sourceToken)!;
        if (taskResponse is null) {
            Console.WriteLine("TaskResponse is null");
            return;
        }
        
        var responseToken = taskResponse.Token;
        if (responseToken != null) {
            var splashData = JsonSerializer.Deserialize<CfSplashDataDto>(responseToken);
            Console.WriteLine("Deserialized token into splash data : " + splashData);
        }

        Console.WriteLine("Task solved: " + taskResponse.Token);
    }
}