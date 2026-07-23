using WinLedger.ElevatedHelper;

await using var services = ElevatedHelperServices.Create();
return await new ElevatedHelperApplication(services).RunAsync(args).ConfigureAwait(false);
