using System.Threading;
using System.Threading.Tasks;
using MessagingService.ExternalDTOs; // Your DTO namespace

namespace MessagingService.Interfaces
{
    public interface IStandardAdsGrpcClient
    {
        /// <summary>
        /// Gets basic details for a standard ad by its ID.
        /// </summary>
        /// <param name="adId">The ID of the ad to retrieve.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The StandardAdDTO if found, otherwise null.</returns>
        Task<StandardAdDTO?> GetAdByIdAsync(string adId, CancellationToken cancellationToken = default);

        // Add other methods here if needed later, e.g.:
        // Task<bool> ValidateAdExistsAsync(string adId, CancellationToken cancellationToken = default);
    }
}