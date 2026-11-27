import { dotnet } from './_framework/dotnet.js'

try {
    const is_browser = typeof window != "undefined";
    if (!is_browser) throw new Error("Expected to be running in a browser");

    const dotnetRuntime = await dotnet
        .withDiagnosticTracing(false)
        .withApplicationArgumentsFromQuery()
        .create();

    const config = dotnetRuntime.getConfig();
    await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
} catch (err) {
    document.body.innerHTML = '<div style="background:#b91c1c;color:#fff;padding:20px;font-family:monospace;font-size:13px;white-space:pre-wrap;word-break:break-word;">' + (err?.stack || err) + '</div>';
}
