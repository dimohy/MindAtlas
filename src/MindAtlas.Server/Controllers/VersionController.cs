using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace MindAtlas.Server.Controllers;

/// <summary>
/// Returns MindAtlas app version metadata so the Web UI can show
/// a single source of truth (read from assembly attributes, set in
/// Directory.Build.props alongside installer/MindAtlas.iss).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class VersionController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var asm = Assembly.GetEntryAssembly() ?? typeof(VersionController).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString()
            ?? "0.0.0";

        // Strip any build metadata suffix (e.g. "0.1.0+git-sha") for display.
        var plus = info.IndexOf('+');
        if (plus > 0) info = info[..plus];

        return Ok(new
        {
            version = info,
            product = "MindAtlas"
        });
    }
}
