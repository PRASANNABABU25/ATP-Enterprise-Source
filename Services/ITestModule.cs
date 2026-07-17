using System.Threading;
using System.Threading.Tasks;
using atp_enterprise_app_wpf.Models;

namespace atp_enterprise_app_wpf.Services
{
    public interface ITestModule
    {
        /// <summary>
        /// Executes the test asynchronously.
        /// </summary>
        /// <param name="testDefinition">The standard configuration for the test.</param>
        /// <param name="cancellationToken">Cancellation token to allow graceful abort.</param>
        /// <returns>A TestRunResult containing the measured values and evaluation outcome.</returns>
        Task<TestRunResult> ExecuteAsync(TestDefinition testDefinition, CancellationToken cancellationToken);
    }
}
