using System.Threading;
using System.Threading.Tasks;

namespace Baryonyx.Health
{
    // Optional screen diagnostics; server synchronization does not need device requirements.
    public interface IHealthRequirementProvider
    {
        Task<HealthRequirementState> GetRequirementsAsync(CancellationToken token);
        Task<bool> OpenSettingsAsync(
            HealthSettingsDestination destination,
            CancellationToken token
        );
    }
}
