using System.Collections.ObjectModel;
using OneWare.Essentials.ToolEngine;

namespace OneWare.Essentials.Services;

public interface IToolService
{
    /// <summary>
    /// Registers a tool. At least one execution strategy must be attached separately via
    /// <see cref="RegisterStrategy"/>, before or after this call - the Settings entry for the tool is
    /// built as soon as both are known, regardless of order.
    /// </summary>
    void Register(ToolContext description);

    /// <summary>
    /// Unregisters a tool by context.
    /// </summary>
    void Unregister(ToolContext description);

    /// <summary>
    /// Unregisters a tool by key.
    /// </summary>
    void Unregister(string toolKey);

    /// <summary>
    /// Returns all registered tools.
    /// </summary>
    ObservableCollection<ToolContext> GetAllTools();

    /// <summary>
    /// Returns the global tool configuration.
    /// </summary>
    ToolConfiguration GetGlobalToolConfiguration();

    /// <summary>
    /// Registers an execution strategy for a tool.
    /// </summary>
    public void RegisterStrategy(string toolKey, IToolExecutionStrategy strategy);

    /// <summary>
    /// Unregisters an execution strategy by key.
    /// </summary>
    public void UnregisterStrategy(string strategyKey);

    /// <summary>
    /// Returns all strategies for a tool.
    /// </summary>
    public IReadOnlyList<IToolExecutionStrategy> GetStrategies(string toolKey);

    /// <summary>
    /// Returns strategy keys for a tool.
    /// </summary>
    public string[] GetStrategyKeys(string toolKey);

    /// <summary>
    /// Returns the active strategy for a tool.
    /// </summary>
    IToolExecutionStrategy GetStrategy(string toolKey);

    /// <summary>
    /// Returns the strategy registered for a tool under a specific strategy key, bypassing the tool's
    /// configured strategy setting entirely. Used to force a specific call onto a specific strategy
    /// (see <see cref="OneWare.Essentials.ToolEngine.ToolCommand.ForcedStrategyKey"/>).
    /// </summary>
    /// <returns>The matching strategy, or <c>null</c> if no such strategy is registered for the tool.</returns>
    IToolExecutionStrategy? TryGetStrategy(string toolKey, string strategyKey);

    /// <summary>
    /// Returns the effective strategy configuration for a tool: the plugin-declared defaults from its
    /// <see cref="ToolContext.StrategyConfiguration"/>, with any user-set overrides applied on top.
    /// A strategy implementation resolves this itself (e.g. via <c>ContainerLocator</c>, the same way
    /// <c>NativeStrategy</c> resolves <c>IChildProcessService</c>) using the tool key it was asked to run,
    /// and reads whichever keys it recognizes by convention (e.g. "docker.image").
    /// </summary>
    IReadOnlyDictionary<string, string> GetStrategyConfiguration(string toolKey);

    /// <summary>
    /// Sets a user override for a single strategy configuration key on a tool, taking precedence over the
    /// owning plugin's declared default for that key. Other keys are left untouched.
    /// </summary>
    void SetStrategyConfigurationValue(string toolKey, string configKey, string value);
}
