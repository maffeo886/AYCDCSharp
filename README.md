# AYCDCSharp
Async Wrapper of the AYCD API 

# Creating a Session
```
session = Session.NewSession(<your api key>);
```

# Sending a Task Request to your session
```
async Task<TaskResponseDto> solve(string url, string siteKey, CaptchaVersion version, string proxy) {
  var GUID = Guid.NewGuid().ToString();

  var taskRequest = new CreateTaskDto(
    TaskId: GUID,
    Url: url,
    SiteKey: siteKey,
    Version: version,
    Proxy: proxy
  );

  LOG taskRequest

  var taskResponse = await globals.session.Solve(taskRequest, TimeSpan.FromMinutes(3), data.CancellationToken);
  if (taskResponse is null || taskResponse.Status.Equals("cancelled") ) {
      LOG "ERROR AYCD"
      return null;
  }

  return taskResponse;
}

var taskResponse = await solve(url, sitekey, version, proxy);
```

You can find an example in AYCDTest

# Note
Not every task response type has their corresponding dto/model.
You can find example responses and therefore their json scheme by looking at their [documentation](https://aycd.io/account/developer/public/docs#/autosolve-http/create-task)

