using WinLedger.Cli;

await using var services = CliServices.Create();
return await new CliApplication(services).RunAsync(args).ConfigureAwait(false);
