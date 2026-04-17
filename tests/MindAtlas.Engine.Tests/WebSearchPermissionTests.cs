using GitHub.Copilot.SDK;
using MindAtlas.Engine.Agent;

namespace MindAtlas.Engine.Tests;

public class WebSearchPermissionTests
{
    [Fact]
    public async Task UrlRequest_Allowed_WhenFlagTrue()
    {
        var handler = CopilotAgentService.CreateWebSearchAwareHandler(() => true);
        var request = (PermissionRequestUrl)CreateUrlRequest();

        var result = await handler(request, null!);

        Assert.Equal(PermissionRequestResultKind.Approved, result.Kind);
    }

    [Fact]
    public async Task UrlRequest_DeniedByRules_WhenFlagFalse()
    {
        var handler = CopilotAgentService.CreateWebSearchAwareHandler(() => false);
        var request = CreateUrlRequest();

        var result = await handler(request, null!);

        Assert.Equal(PermissionRequestResultKind.DeniedByRules, result.Kind);
    }

    [Fact]
    public async Task NonUrlRequest_Approved_RegardlessOfFlag()
    {
        var handler = CopilotAgentService.CreateWebSearchAwareHandler(() => false);
        var request = CreateShellRequest();

        var result = await handler(request, null!);

        // Non-url requests are not gated by the web-search toggle in this
        // release — the engine still approves shell/read/write/etc. like
        // the previous ApproveAll policy did.
        Assert.Equal(PermissionRequestResultKind.Approved, result.Kind);
    }

    // The SDK's request records are not trivially constructible (their
    // mandatory Url/Command/etc. properties use init-only setters on
    // internal types), so we synthesize instances via the uninitialized
    // path and only rely on the subclass identity for pattern matching.
    private static PermissionRequestUrl CreateUrlRequest() =>
        (PermissionRequestUrl)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(PermissionRequestUrl));

    private static PermissionRequest CreateShellRequest() =>
        (PermissionRequest)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(PermissionRequestShell));
}
