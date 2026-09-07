using L5R.Server;

var app = await ServerApplication.BuildAsync(args);
await app.RunAsync();
